using System.Security.Claims;
using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using TokenBasedScript.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using TokenBasedScript.Models;
using TokenBasedScript.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;


var builder = WebApplication.CreateBuilder(args);

Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:API:Secret"];

// Add services to the container.
builder.Services.AddControllersWithViews();

if (builder.Environment.IsDevelopment())
    builder.Services.AddDbContext<MvcContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("MvcContext"))
    );
else
    builder.Services.AddDbContext<MvcContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("ProductionMvcContext")));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim(ClaimTypes.Role, "Admin"));
    options.AddPolicy("LoggedIn", policy => policy.RequireAuthenticatedUser());
});
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    // This lambda determines whether user consent for non-essential 
    // cookies is needed for a given request.
    options.CheckConsentNeeded = context => true;

    options.MinimumSameSitePolicy = SameSiteMode.None;
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = DiscordAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/signin";
        options.LogoutPath = "/signout";
    })
    .AddDiscord(options =>
    {
        options.ClientId = builder.Configuration["Discord:Client:ID"];
        options.ClientSecret = builder.Configuration["Discord:Client:Secret"];
        options.AccessDeniedPath = "/";
        options.Scope.Add("identify");
        options.Scope.Add("email");
        options.Events = new OAuthEvents()
        {
            OnTicketReceived = async context =>
            {
                //Get EF context
                var db = context.HttpContext.RequestServices.GetRequiredService<MvcContext>();
                var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var snowflake = context.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = db.Users.FirstOrDefault(u => u.Snowflake == snowflake);
                if (user == null)
                {
                    //make user
                    user = new User
                    {
                        Snowflake = snowflake,
                        Email = context.Principal.FindFirstValue(ClaimTypes.Email),
                        EmailConfirmed = context.Principal.FindFirstValue(ClaimTypes.Email) != null,
                        UserName = context.Principal.FindFirstValue(ClaimTypes.Name),
                        TokenLeft = 0
                    };
                    db.Users.Add(user);
                }
                else
                {
                    //update user
                    user.Email = context.Principal.FindFirstValue(ClaimTypes.Email);
                    user.EmailConfirmed = context.Principal.FindFirstValue(ClaimTypes.Email) != null;
                    user.UserName = context.Principal.FindFirstValue(ClaimTypes.Name);
                }

                var identity = context.Principal?.Identity as ClaimsIdentity;
                //check if id equal admin id on config
                if (snowflake == config["Discord:User:Admin:ID"] || user.IsAdmin)
                {
                    //add admin role
                    identity?.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
                    user.IsAdmin = true;
                }

                //user role
                identity?.AddClaim(new Claim(ClaimTypes.Role, "User"));

                db.SaveChanges();
            }
        };
    });

builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

builder.Services.AddScoped<IGiveUser, GiveUser>();
builder.Services.AddHostedService<NikeBrtService>();
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MvcContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHttpsRedirection();
}


app.UseStaticFiles();
app.UseCookiePolicy();
app.UseRouting();


app.UseAuthentication();
app.UseAuthorization();

app.UseStatusCodePagesWithReExecute("/Error/Error{0}");
app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.Lax
});
app.MapControllerRoute(
    "default",
    "{controller=Home}/{action=Index}/{id?}");

app.Run();