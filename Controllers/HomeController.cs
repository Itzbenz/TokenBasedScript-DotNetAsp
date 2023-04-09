using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TokenBasedScript.Data;
using TokenBasedScript.Models;
using TokenBasedScript.Services;

namespace TokenBasedScript.Controllers;

public class HomeController : Controller
{
    private readonly MvcContext _context;
    private readonly IGiveUser _giveUser;
    private readonly ILogger<HomeController> _logger;

    public HomeController(MvcContext context, ILogger<HomeController> logger, IGiveUser giveUser)
    {
        _logger = logger;
        _context = context;
        _giveUser = giveUser;
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

        var scriptExecutions = _context.ScriptExecutions
            .Where(s => s.User == user)
            .Include(item => item.Statuses)
            .OrderByDescending(item => item.DateModified)
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

        var scriptExecution = _context.ScriptExecutions
            .Where(s => s.Id == id)
            .Include(item => item.Statuses)
            .FirstOrDefault();
        if (scriptExecution is null || (scriptExecution.User != user && !user.IsAdmin))//if user is not admin and not the owner of the script
        {
            return BadRequest();
        }

        

        return View(scriptExecution);
    }

    [Authorize(Policy = "LoggedIn")]
    public async Task<IActionResult> Index()
    {
        User? user = await _giveUser.GetUser();
        return View(user);
    }

    [HttpGet("privacy")]
    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet("terms")]
    public IActionResult Terms()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
    }
}