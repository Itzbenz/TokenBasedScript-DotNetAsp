using Microsoft.AspNetCore.Authentication;
using TokenBasedScript.Data;
using TokenBasedScript.Extensions;

namespace TokenBasedScript.Controllers;
using Microsoft.AspNetCore.Authentication.Cookies;
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
    public async Task<IActionResult> SignIn() => View("SignIn", await HttpContext.GetExternalProvidersAsync());

    [HttpPost("~/signin")]
    public async Task<IActionResult> SignIn([FromForm] string provider)
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
        return Challenge(new AuthenticationProperties { RedirectUri = "/" }, provider);
    }

    [HttpGet("~/signout")]
    [HttpPost("~/signout")]
    public IActionResult SignOutCurrentUser()
    {
        // Instruct the cookies middleware to delete the local cookie created
        // when the user agent is redirected from the external identity provider
        // after a successful authentication flow (e.g Google or Facebook).
        return SignOut(new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme);
    }
}