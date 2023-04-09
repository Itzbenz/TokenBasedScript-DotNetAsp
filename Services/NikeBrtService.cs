using System.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TokenBasedScript.Data;
using TokenBasedScript.Models;

namespace TokenBasedScript.Services;

internal class Orders
{
    public Dictionary<long, NikeOrder> data;
    public string? message;
}


//should make this a webhook listener
public class NikeBrtService : BackgroundService
{
    private static readonly HttpClient Client = new HttpClient();

    public static bool Running = false;
    private static bool _online = false;
    private readonly ILogger _logger;
    private IServiceProvider Services { get; }
    private DateTime _lastClean, _lastRefund = DateTime.Now;

    public NikeBrtService(IServiceProvider services, ILogger<NikeBrtService> logger)
    {
        Services = services;
        _logger = logger;
    }

   

    public static bool Online
    {
        get => Running && _online;
        set => _online = value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
  
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetService<MvcContext>();
        var appConfig = scope.ServiceProvider.GetService<IAppConfigService>();
        if (context == null)
            throw new Exception("Could not get MvcContext from DI");
        if (appConfig == null)
            throw new Exception("Could not get AppConfigService");
        var url = appConfig.Get<string>(Settings.ScriptNikeBrtBackendUrl);
        if (url == null)
            throw new Exception("Nike Brt Script Queue URL is not set in configuration");
        _logger.LogInformation("Nike Brt Script Queue is running");
        _logger.LogInformation("Nike Brt Script Queue URL: {Url}", url);
        Online = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            Running = true;
            await Task.Delay(Online ? 1000 : 5000, stoppingToken);
            _logger.LogDebug("Querying Nike Brt Script Queue");

            try
            {
                url = appConfig.Get<string>(Settings.ScriptNikeBrtBackendUrl) ?? "";
                await Work(context, appConfig, url, stoppingToken);
                if (!Online)
                {
                    _logger.LogInformation("Nike Brt Script is online");
                }

                Online = true;
            }
            catch (HttpRequestException e)
            {
                if (Online) _logger.LogError("Error interacting with NikeBrt Backend: {Message}", e.Message);
                Online = false;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error executing NikeBrt Service");
                Online = false;
            }

            try
            {
                await WorkRefund(context, stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while refunding failed NikeBrt orders");
            }
        }

        Online = false;
        Running = false;
    }

    //TODO cache this
    private async Task Work(MvcContext context, IAppConfigService appConfig,  string url, CancellationToken stoppingToken)
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
                var transaction =
                    await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, stoppingToken);
                try
                {
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
                        if ((order.status == "success" || Math.Abs(order.progressFloat - 1) < 0.01) && !scriptExecution.IsFinished)
                        {
                            scriptExecution.IsSuccess = true;
                            scriptExecution.IsFinished = true;
                            context.ScriptExecutions.Update(scriptExecution);
                            //delete order
                            var deleteResponse = await Client.DeleteAsync(url + "/orders/" + id, stoppingToken);
                        }
                        else if (order.failed && !scriptExecution.IsFinished)
                        {
                            scriptExecution.IsSuccess = false;
                            scriptExecution.IsFinished = true;
                            //resolve user

                            //refund user
                            if (scriptExecution.User != null)
                            {
                                _logger.LogInformation("Refunded user {Id} for NikeBrt {NikeBrtId}",
                                    scriptExecution.User.Id, id);
                            }
                            else
                            {
                                _logger.LogWarning("NikeBrt {Id} has no user associated", id);
                            }

                            context.ScriptExecutions.Update(scriptExecution);
                            var deleteResponse = await Client.DeleteAsync(url + "/orders/" + id, stoppingToken);
                        }


                        //update status
                        var lastStatus = scriptExecution.Statuses.LastOrDefault();
                        if (Math.Abs(order.progressFloat - scriptExecution.Progress) > 0.01)
                        {
                            scriptExecution.Progress = order.progressFloat;
                            context.ScriptExecutions.Update(scriptExecution);
                        }

                        if ((order.status != null && lastStatus?.Message != order.status))
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
                    await transaction.CommitAsync(stoppingToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error while updating NikeBrt orders");
                    await transaction.RollbackAsync(stoppingToken);
                    throw e;
                }
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
            await using var transaction =
                await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, stoppingToken);
            try
            {
                //clean stalled executions
                var stalledExecutions = context.ScriptExecutions
                    .Where(x => !x.IsFinished && x.ScriptName == "NikeBRT")
                    .ToList();

                foreach (var scriptExecution in stalledExecutions)
                {
                    if (scriptExecution.IsFinished)
                    {
                        _logger.LogWarning("NikeBrt {Id} is already finished", scriptExecution.Id);
                    }

                    if (ids.Contains(scriptExecution.Id)) continue;
                    scriptExecution.IsFinished = true;
                    scriptExecution.IsSuccess = false;
                    await context.SaveChangesAsync(stoppingToken);
                }


                await transaction.CommitAsync(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while cleaning stalled NikeBrt executions");
                await transaction.RollbackAsync(stoppingToken);
            }
        }
    }


    private async Task WorkRefund(MvcContext context, CancellationToken stoppingToken)
    {
        if (DateTime.Now - _lastRefund < TimeSpan.FromMinutes(1)) return; 
        _lastRefund = DateTime.Now;
        await using var transaction =
            await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, stoppingToken);
        try
        {
            _logger.LogInformation("Refunding Failed Execution of NikeBrt");
            var scriptToBeRefunded = context.ScriptExecutions
                .Where(x => x.TokenUsed > 0 && !x.IsSuccess && x.IsFinished && x.User != null)
                .Include(x => x.User)
                .AsNoTracking()
                .ToList();
            if (scriptToBeRefunded.Count == 0)
            {
                return;
            }
            
            foreach (var script in scriptToBeRefunded)
            {
                if (script.User == null) continue;
                //add TokenLeft
                await context.Users.Where(x => x.Id == script.User.Id).ExecuteUpdateAsync(s => 
                    s.SetProperty(x => x.TokenLeft, x => x.TokenLeft + script.TokenUsed), cancellationToken: stoppingToken);

                //set to 0
                await context.ScriptExecutions.Where(x => x.Id == script.Id).ExecuteUpdateAsync(s => 
                    s.SetProperty(x => x.TokenUsed, 0), cancellationToken: stoppingToken);
                
                await context.SaveChangesAsync(stoppingToken);
            }
            
            await transaction.CommitAsync(stoppingToken);
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(stoppingToken);
        }
    }
}