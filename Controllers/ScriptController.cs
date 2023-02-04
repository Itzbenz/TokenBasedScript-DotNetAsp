using System.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TokenBasedScript.Data;
using TokenBasedScript.Models;
using TokenBasedScript.Services;

namespace TokenBasedScript.Controllers;
[Authorize(Policy = "LoggedIn")]
public class ScriptController  : Controller
{
    private static readonly HttpClient Client = new HttpClient();
    private readonly IConfiguration _config;

    private readonly MvcContext _context;
    private readonly IGiveUser _giveUser;
    private readonly ILogger<ScriptController> _logger;

    public ScriptController(MvcContext context, ILogger<ScriptController> logger, IGiveUser giveUser, IConfiguration config)
    {
        _logger = logger;
        _context = context;
        _giveUser = giveUser;
        _config = config;
    }

    public IActionResult Index()
    {
        return View();
    }


    [HttpGet]
    public async Task<IActionResult> NikeBrtAsync()
    {
        User? user = await _giveUser.GetUser();
        if (!NikeBrtService.Online)
        {
            ViewData["ErrorMessage"] = "NikeBRT Executor is currently offline";
        }

        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> NikeBrtAsync(string? orderEmail, string? orderNumber, string? name,
        string? telephone, string? email, string? mobilePhoneNumber, string? address, string? postCode, string? town,
        string? district)
    {
        User? user = await _giveUser.GetUser();
        if (user is not {TokenLeft: > 0}) return RedirectToAction("Index", "Home");
        if (!NikeBrtService.Online)
        {
            ViewData["ErrorMessage"] = "NikeBRT Executor is currently offline";
            return View(user);
        }

        //check for form data
        if (orderEmail == null || orderNumber == null || name == null || telephone == null || email == null ||
            mobilePhoneNumber == null || address == null || postCode == null || town == null || district == null)
        {
            var sb = new StringBuilder();
            sb.Append("Please fill in all the fields: ");
            if (orderEmail == null) sb.Append("orderEmail, ");
            if (orderNumber == null) sb.Append("orderNumber, ");
            if (name == null) sb.Append("name, ");
            if (telephone == null) sb.Append("telephone, ");
            if (email == null) sb.Append("email, ");
            if (mobilePhoneNumber == null) sb.Append("mobilePhoneNumber, ");
            if (address == null) sb.Append("address, ");
            if (postCode == null) sb.Append("postCode, ");
            if (town == null) sb.Append("town, ");
            if (district == null) sb.Append("district, ");
            
            
            ViewData["ErrorMessage"] = sb.ToString().Substring(0, sb.Length - 2);
            return View(user);
        }

        //check for valid email
        if (!Regex.IsMatch(orderEmail, @"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$"))
        {
            ViewData["ErrorMessage"] = "Please enter a valid email";
            return View(user);
        }

        string? nikeBrtBackendUrl = _config.GetValue<string>("Script:NikeBRT:Backend:URL");
        long scriptExecutionId = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        Client.DefaultRequestHeaders.Accept.Clear();
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var scriptContent = JsonConvert.SerializeObject(new
        {
            id = scriptExecutionId,
            orderEmail = orderEmail,
            orderNumber = orderNumber,
            vasBrtForm = new
            {
                name = name,
                telephone = telephone,
                email = email,
                mobilePhoneNumber = mobilePhoneNumber,
                optionsData = new
                {
                    name = name,
                    address = address,
                    postCode = postCode,
                    town = town,
                    district = district
                }
            }
        });
        var content = new StringContent(scriptContent, Encoding.UTF8, "application/json");
        var response = await Client.PostAsync(nikeBrtBackendUrl + "/orders", content);
        if (response.IsSuccessStatusCode)
        {
            var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

            user = _context.Users.FirstOrDefault(u => u.Id == user.Id);
            if (user == null) throw new Exception("User is null, before its not: " + user);
            user.TokenLeft--;
            _context.ScriptExecutions.Add(new ScriptExecution
            {
                Id = scriptExecutionId + "",
                ScriptName = "NikeBRT",
                ScriptContent = scriptContent,
                TokenUsed = 1,
                User = user,
            });
            _context.Update(user);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            ViewData["InfoMessage"] = "Queued";
            return Redirect("/ScriptStatus/" + scriptExecutionId);
        }
        else
        {
            ViewData["ErrorMessage"] = "Something went wrong";
        }

        return View(user);
        //go back home
    }
}