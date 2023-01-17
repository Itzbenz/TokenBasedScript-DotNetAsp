using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

using TokenBasedScript.Data;
using TokenBasedScript.Models;

namespace TokenBasedScript.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly MvcContext _context;

    public HomeController(MvcContext context, ILogger<HomeController> logger)
    {
        _logger = logger;
        _context = context;
    }
    [HttpGet("AddToken")]
    public IActionResult BuyToken()
    {
        User? user = GetUser();
        if (user != null)
        {
            user.TokenLeft++;
            _context.SaveChanges();
        }
        //go back home
        return RedirectToAction("Index", "Home");
    }
    
    [HttpGet("UseToken")]
    public IActionResult UseToken()
    {
        User? user = GetUser();
        if (user != null)
        {
            user.TokenLeft--;
            _context.SaveChanges();
        }
        //go back home
        return RedirectToAction("Index", "Home");
    }
  
    private User? GetUser()
    {
        if (!HttpContext.User.HasClaim(c => c.Type == ClaimTypes.NameIdentifier)) return null;
        //find in database
        var user = _context.Users.FirstOrDefault(u => u.Snowflake == HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (user == null)
        {
            //make user
            user = new User
            {
                Snowflake = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
                Email = HttpContext.User.FindFirstValue(ClaimTypes.Email),
                EmailConfirmed = HttpContext.User.FindFirstValue(ClaimTypes.Email) != null,
                UserName = HttpContext.User.FindFirstValue(ClaimTypes.Name),
                TokenLeft = 0
            }; 
            _context.Users.Add(user);
        }
        _context.SaveChanges();
        return user;
    }
    public IActionResult Index()
    {
       
       User? user = GetUser();
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
