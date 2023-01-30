using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TokenBasedScript.Data;
using TokenBasedScript.Models;
using TokenBasedScript.Services;

namespace TokenBasedScript.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly MvcContext _context;
    private readonly IConfiguration _config;
    private readonly IGiveUser _giveUser;


    private static readonly HttpClient Client = new HttpClient();

    public HomeController(MvcContext context, ILogger<HomeController> logger, IGiveUser giveUser, IConfiguration config)
    {
        _logger = logger;
        _context = context;
        _giveUser = giveUser;
        _config = config;
    }


    [HttpGet("AddToken")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> BuyTokenAsync()
    {
        User? user = await _giveUser.GetUser();
        if (user != null)
        {
            user.TokenLeft++;
            await _context.SaveChangesAsync();
        }

        //go back home
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("UseToken")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UseTokenAsync()
    {
        User? user = await _giveUser.GetUser();
        if (user != null)
        {
            user.TokenLeft--;
            await _context.SaveChangesAsync();
        }

        //go back home
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("NikeBRTScript")]
    [Authorize(Policy = "LoggedIn")]
    public async Task<IActionResult> NikeBrtScriptAsync()
    {
        User? user = await _giveUser.GetUser();
        if (!NikeBrtService.Online)
        {
            ViewData["Error"] = "NikeBRT Executor is currently offline";
        }

        return View(user);
    }

    [HttpPost("NikeBRTScript")]
    [Authorize(Policy = "LoggedIn")]
    public async Task<IActionResult> NikeBrtScriptAsync(string? orderEmail, string? orderNumber, string? name,
        string? telephone, string? email, string? mobilePhoneNumber, string? address, string? postCode, string? town,
        string? district)
    {
        User? user = await _giveUser.GetUser();
        if (user is not {TokenLeft: > 0}) return RedirectToAction("Index", "Home");
        if (!NikeBrtService.Online)
        {
            ViewData["Error"] = "NikeBRT Executor is currently offline";
            return View(user);
        }
        
        //check for form data
        if (orderEmail == null || orderNumber == null || name == null || telephone == null || email == null ||
            mobilePhoneNumber == null || address == null || postCode == null || town == null || district == null)
        {
            ViewData["Error"] = "Please fill in all the fields";
            return View(user);
        }

        //check for valid email
        if (!Regex.IsMatch(orderEmail, @"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$"))
        {
            ViewData["Error"] = "Please enter a valid email";
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
            user.TokenLeft--;
            _context.ScriptExecutions.Add(new ScriptExecution
            {
                Id = scriptExecutionId + "",
                ScriptName = "NikeBRT",
                ScriptContent = scriptContent,
                User = user,
            });
            _context.Update(user);
            await _context.SaveChangesAsync();
            ViewData["Info"] = "Queued";
            return Redirect("/ScriptStatus/" + scriptExecutionId);
        }
        else
        {
            ViewData["Error"] = "Something went wrong";
        }

        return View(user);
        //go back home
    }

    [Authorize(Policy = "LoggedIn")]
    [HttpGet("ScriptStatus")]
    public async Task<IActionResult> ScriptStatusAsync()
    {
        User? user = await _giveUser.GetUser();
        if (user is null)
        {
            //??????????????????
            throw new Exception("User is null");
        }

        var scriptExecutions = _context.ScriptExecutions.Where(s => s.User == user).Include(item => item.Statuses)
            .ToList();
        return View(scriptExecutions);
    }

    [Authorize(Policy = "LoggedIn")]
    [HttpGet("ScriptStatus/{id}")]
    public async Task<IActionResult> ScriptStatusDetailAsync(string id)
    {
        User? user = await _giveUser.GetUser();
        if (user is null)
        {
            //??????????????????
            throw new Exception("User is null");
        }

        var scriptExecution = _context.ScriptExecutions.Where(s => s.Id == id).Include(item => item.Statuses)
            .FirstOrDefault();
        if (scriptExecution is null || scriptExecution.User != user)
        {
            return BadRequest();
        }

        return View(scriptExecution);
    }

    public async Task<IActionResult> Index()
    {
        User? user = await _giveUser.GetUser();
        return View(user);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
    }
}