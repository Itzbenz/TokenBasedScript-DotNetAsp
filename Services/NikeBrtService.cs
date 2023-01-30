using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Stripe;
using TokenBasedScript.Data;
using TokenBasedScript.Models;

namespace TokenBasedScript.Services;

internal class Orders
{
    public string? message;
    public Dictionary<long, NikeOrder> data;
}

public class NikeOrder
{
    /**
    id: number; //External ID

    orderNumber: string; //Nike
    orderEmail: string; // Nike
    myBRTNumber?: string;
    shipmentReference?: string;
    myBRTCode?: string;
    status?: string;
    failed?: boolean;
    postCode?: string;

    vasBrtForm?: VasBrtForm;
    */
    public string id;

    public string? orderNumber;
    public string? orderEmail;
    public string? myBRTNumber;
    public string? shipmentReference;
    public string? myBRTCode;
    public string? status;
    public bool failed;
    public string? postCode;
    public VasBrtForm? vasBrtForm;

    public class VasBrtForm
    {
        /**
     *  name: string;
    telephone: string;
    email: string;
    mobilePhoneNumber: string;

    optionsData: {
        name: string;
        address: string;
        postCode: string;
        town: string;
        district: string;
    }
     */
        public string? name;

        public string? telephone;
        public string? email;
        public string? mobilePhoneNumber;
        public OptionsData? optionsData;

        public class OptionsData
        {
            public string? name;
            public string? address;
            public string? postCode;
            public string? town;
            public string? district;
        }
    }
}

//should make this a webhook listener
public class NikeBrtService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IConfiguration _config;
    private IServiceProvider Services { get; }
    private static readonly HttpClient Client = new HttpClient();
    private DateTime _lastClean = DateTime.Now;

    public static bool Running = false;
    private static bool _online = false;

    public static bool Online
    {
        get => Running && _online;
        set => _online = value;
    }

    public NikeBrtService(IServiceProvider services, ILogger<NikeBrtService> logger, IConfiguration config)
    {
        Services = services;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var url = _config.GetValue<string>("Script:NikeBRT:Backend:URL");
        if (url == null)
            throw new Exception("Nike Brt Script Queue URL is not set in configuration");
        _logger.LogInformation("Nike Brt Script Queue is running");
        _logger.LogInformation("Nike Brt Script Queue URL: {Url}", url);
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetService<MvcContext>();
        if (context == null)
            throw new Exception("Could not get MvcContext from DI");
        Online = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            Running = true;
            await Task.Delay(Online ? 1000 : 5000, stoppingToken);
            _logger.LogDebug("Querying Nike Brt Script Queue");
            try
            {
                await Work(context, url, stoppingToken);
                if (!Online)
                {
                    _logger.LogInformation("Nike Brt Script is online");
                }
                Online = true;
            }
            catch (HttpRequestException e)
            {
                if(Online) _logger.LogError("Error interacting with NikeBrt Backend: {Message}", e.Message);
                Online = false;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error executing NikeBrt Service");
                Online = false;
            }
        }
        Online = false;
        Running = false;
    }

    private async Task Work(MvcContext context, string url, CancellationToken stoppingToken)
    {
        var ids = new List<string>();
       
            var response = await Client.GetAsync(url + "/orders", stoppingToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(stoppingToken);
                var orders = JsonConvert.DeserializeObject<Orders>(content)?.data.Values;
                if (orders != null)
                {
                    _logger.LogDebug("Found {Count} orders", orders.Count);

                    foreach (var order in orders)
                    {
                        var id = order.id;
                        ids.Add(id);
                        var scriptExecution = context.ScriptExecutions.Include(x => x.Statuses).Include(x => x.User)
                            .FirstOrDefault(x => x.Id == id);
                        if (scriptExecution == null)
                        {
                            _logger.LogWarning("NikeBrt {Id} not found in database, external intervention?", id);
                            continue;
                        }

                        //check if finished event
                        if (order.status == "success" && !scriptExecution.IsFinished)
                        {
                            scriptExecution.IsSuccess = true;
                            scriptExecution.IsFinished = true;
                            context.ScriptExecutions.Update(scriptExecution);
                        }
                        else if (order.failed && !scriptExecution.IsFinished)
                        {
                            scriptExecution.IsSuccess = false;
                            scriptExecution.IsFinished = true;
                            //resolve user

                            //refund user
                            if (scriptExecution.User != null)
                            {
                                //scriptExecution.User.TokenLeft += 1;
                                _logger.LogInformation("Refunded user {Id} for NikeBrt {NikeBrtId}",
                                    scriptExecution.User.Id, id);
                            }
                            else
                            {
                                _logger.LogWarning("NikeBrt {Id} has no user associated", id);
                            }

                            context.ScriptExecutions.Update(scriptExecution);
                        }


                        //update status
                        var lastStatus = scriptExecution.Statuses.LastOrDefault();
                        if (order.status != null && lastStatus?.Message != order.status)
                        {
                            scriptExecution.Statuses.Add(new ScriptExecution.Status
                            {
                                Message = order.status,
                            });
                            _logger.LogInformation("NikeBrt {Id} status updated to {Status}", id, order.status);
                            context.ScriptExecutions.Update(scriptExecution);
                        }
                    }


                    await context.SaveChangesAsync(stoppingToken);
                }
                else
                {
                    _logger.LogError("Failed to parse orders");
                    _logger.LogError("Response: {Response}", content);
                }
            }
        
     

        if (DateTime.Now - _lastClean > TimeSpan.FromMinutes(1))
        {
            _lastClean = DateTime.Now;
            _logger.LogInformation("Cleaning stalled executions");
            //clean stalled executions
            var stalledExecutions = context.ScriptExecutions
                .Where(x => !x.IsFinished && x.ScriptName == "NikeBRT")
                .Include(x => x.User)
                .ToList();
            foreach (var scriptExecution in stalledExecutions)
            {
                if (scriptExecution.IsFinished)
                {
                    _logger.LogWarning("NikeBrt {Id} is already finished", scriptExecution.Id);
                }
                if(ids.Contains(scriptExecution.Id)) continue;
                scriptExecution.IsFinished = true;
                scriptExecution.IsSuccess = false;
                if (scriptExecution.User != null)
                {
                    scriptExecution.User.TokenLeft += 1;
                    _logger.LogInformation("Refunded user {Id} for NikeBrt {NikeBrtId} because it was stalled",
                        scriptExecution.User.Id, scriptExecution.Id);
                }
                else
                {
                    _logger.LogWarning("NikeBrt {Id} has no user associated", scriptExecution.Id);
                }

                context.ScriptExecutions.Update(scriptExecution);
            }
            await context.SaveChangesAsync(stoppingToken);
        }
    }
}