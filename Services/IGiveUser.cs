namespace TokenBasedScript.Services;

using System.Security.Claims;
using TokenBasedScript.Data;
using TokenBasedScript.Models;

public interface IGiveUser
{
    Task<User?> GetUser();
}

public class GiveUser : IGiveUser
{
    private MvcContext _context;
    private HttpContext _httpContext;

    public GiveUser(MvcContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContext = httpContextAccessor.HttpContext!;
    }

    public Task<User?> GetUser()
    {
        //check our userManager
        //var suser = await _signInManager.UserManager.GetUserAsync(HttpContext.User);
        //if (suser != null) return suser;
        if (!_httpContext.User.HasClaim(c => c.Type == ClaimTypes.NameIdentifier)) return Task.FromResult<User?>(null);
        //find in database
        
        var user = _context.Users.FirstOrDefault(u =>
            u.Snowflake == _httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));

        //await _signInManager.SignInAsync(user, true);
        return Task.FromResult(user);
    }
}