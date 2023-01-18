namespace TokenBasedScript.Services;

using System.Security.Claims;
using TokenBasedScript.Data;
using TokenBasedScript.Models;

public interface IGiveUser{
    Task<User?> GetUser();
}

public class GiveUser : IGiveUser{
    private MvcContext _context;
    private HttpContext _httpContext;
    public GiveUser(MvcContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContext = httpContextAccessor.HttpContext;
    }

   public async Task<User?> GetUser()
    {
        
        //check our userManager
        //var suser = await _signInManager.UserManager.GetUserAsync(HttpContext.User);
        //if (suser != null) return suser;
        //if (!HttpContext.User.HasClaim(c => c.Type == ClaimTypes.NameIdentifier)) return null;
        //find in database

        var user = _context.Users.FirstOrDefault(u => u.Snowflake == _httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (user == null)
        {
            //make user
            user = new User
            {
                Snowflake = _httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
                Email = _httpContext.User.FindFirstValue(ClaimTypes.Email),
                EmailConfirmed = _httpContext.User.FindFirstValue(ClaimTypes.Email) != null,
                UserName = _httpContext.User.FindFirstValue(ClaimTypes.Name),
                TokenLeft = 0
            }; 
            _context.Users.Add(user);
        }
        _context.SaveChangesAsync();
        //await _signInManager.SignInAsync(user, true);
        return user;
    }
    

}