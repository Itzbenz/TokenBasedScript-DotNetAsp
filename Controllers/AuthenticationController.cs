using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using TokenBasedScript.Data;
using TokenBasedScript.Extensions;

namespace TokenBasedScript.Controllers;

[Route("/")]
public class AuthenticationController : Controller
{
    private readonly IConfiguration _config;
    private readonly MvcContext _context;

    public AuthenticationController(MvcContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }


    [HttpGet("/signin")]
    public async Task<IActionResult> SignIn([FromQuery] string returnUrl = "/")
    {
        var providers = await HttpContext.GetExternalProvidersAsync();
        //if only 1 provider, redirect to it
        if (providers.Count() == 1)
        {
            return await SignIn(providers.First().Name, returnUrl);
        }

        return View("SignIn", providers);
    }

    [HttpPost("/signin")]
    public async Task<IActionResult> SignIn([FromForm] string provider = "Discord", [FromForm] string returnUrl = "/")
    {
        if (returnUrl == null) throw new ArgumentNullException(nameof(returnUrl));
        // Note: the "provider" parameter corresponds to the external
        // authentication provider chosen by the user agent.
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
        return Challenge(new AuthenticationProperties {RedirectUri = returnUrl}, provider);
    }

    [HttpGet("/signout")]
    [HttpPost("/signout")]
    public async Task<IActionResult> SignOutCurrentUserAsync()
    {
        // Instruct the cookies middleware to delete the local cookie created
        // when the user agent is redirected from the external identity provider
        // after a successful authentication flow (e.g Google or Facebook).
        //await _signInManager.SignOutAsync();
        return SignOut(new AuthenticationProperties {RedirectUri = "/"},
            CookieAuthenticationDefaults.AuthenticationScheme);
    }


    [HttpGet("/login")]
    public async Task<IActionResult> LoginAsync([FromQuery] string ReturnUrl = "/")
    {
        //sign in
        //await _signInManager.SignInAsync(user, true);
        return Redirect(ReturnUrl);
    }
}