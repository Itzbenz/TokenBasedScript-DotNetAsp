using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using TokenBasedScript.Data;
using TokenBasedScript.Models;
using TokenBasedScript.Services;

namespace TokenBasedScript.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly MvcContext _context;
    private readonly IGiveUser _giveUser;
    public HomeController(MvcContext context, ILogger<HomeController> logger, IGiveUser giveUser)
    {
        _logger = logger;
        _context = context;
        _giveUser = giveUser;
    }

    
    [HttpGet("AddToken")]
    public async Task<IActionResult> BuyTokenAsync()
    {
        User? user = await _giveUser.GetUser();
        if (user != null)
        {
            user.TokenLeft++;
            _context.SaveChanges();
        }
        //go back home
        return RedirectToAction("Index", "Home");
    }
    
    [HttpGet("UseToken")]
    public async Task<IActionResult> UseTokenAsync()
    {
        User? user = await _giveUser.GetUser();
        if (user != null)
        {
            user.TokenLeft--;
            _context.SaveChanges();
        }
        //go back home
        return RedirectToAction("Index", "Home");
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
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
