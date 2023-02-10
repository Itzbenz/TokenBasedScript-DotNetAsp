using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TokenBasedScript.Data;
using TokenBasedScript.Models;
using TokenBasedScript.Services;

namespace TokenBasedScript.Controllers;
[Authorize(Policy = "AdminOnly")]
public class AdminController : Controller
{
    private readonly IConfiguration _config;

    private readonly MvcContext _context;
    private readonly IGiveUser _giveUser;
    private readonly ILogger<AdminController> _logger;
    
    public AdminController(MvcContext context, ILogger<AdminController> logger, IGiveUser giveUser, IConfiguration config)
    {
        _logger = logger;
        _context = context;
        _giveUser = giveUser;
        _config = config;
    }

    [HttpPost]
    public async Task<IActionResult> AddTokenAsync(int? amount)
    {
        User? user = await _giveUser.GetUser();
        if (user != null)
        {
            user.TokenLeft += amount ?? 1;
            await _context.SaveChangesAsync();
        }

        //go back home
        return Redirect(Request.Headers["Referer"].ToString());
        
    }
    
    [HttpPost]
    public async Task<IActionResult> RemoveTokenAsync(int? amount)
    {
        User? user = await _giveUser.GetUser();
        if (user == null) return RedirectToAction("Index", "Home");
        user.TokenLeft -= amount ?? 1;
        await _context.SaveChangesAsync();

        //go back home
        return Redirect(Request.Headers["Referer"].ToString());
    }
    
}