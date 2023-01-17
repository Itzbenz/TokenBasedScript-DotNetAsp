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

    public IActionResult Index()
    {
        //check if logged in to OAuth
        // Claims email
        if(HttpContext.User.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
        {
            //find in database
            var user = _context.Users.FirstOrDefault(u => u.Snowflake == HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (user == null)
            {
                //make user
                user = new User
                {
                    Snowflake = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
                    Email = HttpContext.User.FindFirstValue(ClaimTypes.Email),
                    UserName = HttpContext.User.FindFirstValue(ClaimTypes.Name),
                    CreatedDate = DateTime.Now,
                    LastLogin = DateTime.Now,
                    TokenLeft = 0
                }; 
                _context.Users.Add(user);
            }
            //assign user
            user.LastLogin = DateTime.Now;
            _context.SaveChanges();
            //assign user to session
            
        }
        //if not logged in, redirect to login page
        return View();
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
