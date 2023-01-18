using Microsoft.AspNetCore.Authentication;
using TokenBasedScript.Data;
using TokenBasedScript.Extensions;

namespace TokenBasedScript.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
[Route("/")]
public class AuthenticationController : Controller
{
    
    private readonly MvcContext _context;

    public AuthenticationController(MvcContext context)
    {
        _context = context;
        
    }
    


    [HttpGet("~/signin")]
    public async Task<IActionResult> SignIn([FromQuery] string ReturnUrl = "/"){
        var providers = await HttpContext.GetExternalProvidersAsync();
        //if only 1 provider, redirect to it
        if (providers.Count() == 1) {
            return await SignIn(providers.First().Name, ReturnUrl);
        }
        return View("SignIn", providers);
    }

    [HttpPost("~/signin")]
    public async Task<IActionResult> SignIn([FromForm] string provider="Discord", [FromForm] string ReturnUrl = "/")
    {
        // Note: the "provider" parameter corresponds to the external
        // authentication provider choosen by the user agent.
        if (string.IsNullOrWhiteSpace(provider))
        {
            return BadRequest();
        }

        if (!await HttpContext.IsProviderSupportedAsync(provider))
        {
            return BadRequest();
        }

        // Instruct the middleware corresponding to the requested external identity
        // provider to redirect the user agent to its own authorization endpoint.
        // Note: the authenticationScheme parameter must match the value configured in Startup.cs
        return Challenge(new AuthenticationProperties { RedirectUri = "/login?ReturnUrl=" + ReturnUrl }, provider);
    }

    [HttpGet("~/signout")]
    [HttpPost("~/signout")]
    public async Task<IActionResult> SignOutCurrentUserAsync()
    {
        // Instruct the cookies middleware to delete the local cookie created
        // when the user agent is redirected from the external identity provider
        // after a successful authentication flow (e.g Google or Facebook).
        //await _signInManager.SignOutAsync();
        return SignOut(new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme);
    }


    [HttpGet("~/login")]
    public async Task<IActionResult> LoginAsync([FromQuery] string ReturnUrl = "/")
    {
        //check claim
        var snowflake = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (snowflake == null) return RedirectToAction("Index", "Home");
        //find user
        var user = _context.Users.FirstOrDefault(u => u.Snowflake == snowflake);
        if (user == null)
        {
            //make user
            user = new Models.User
            {
                Snowflake = snowflake,
                Email = User.FindFirstValue(ClaimTypes.Email),
                EmailConfirmed = HttpContext.User.FindFirstValue(ClaimTypes.Email) != null,
                UserName = HttpContext.User.FindFirstValue(ClaimTypes.Name),
                TokenLeft = 0
            };
            _context.Users.Add(user);
            _context.SaveChanges();
        }
        //sign in
        //await _signInManager.SignInAsync(user, true);
        return Redirect(ReturnUrl);
    }
}