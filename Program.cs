using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TokenBasedScript.Data;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddControllersWithViews();

if (builder.Environment.IsDevelopment())
    builder.Services.AddDbContext<MvcContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("MvcContext"))
    );
else
    builder.Services.AddDbContext<MvcContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("ProductionMvcContext")));


builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/signin";
        options.LogoutPath = "/signout";
    })
    .AddDiscord(options =>
    {
        options.ClientId = "1064116627366481961";
        options.ClientSecret = "wEtnRkdogXKVW2y8xbwGnPxl-rcpsnoC";
        options.Scope.Add("identify");
        options.Scope.Add("email");
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MvcContext>();
    db.Database.Migrate();
}
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");


app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();
app.UseAuthentication();
app.UseStatusCodePagesWithReExecute("/Error/Error{0}");
app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.Lax
});
app.MapControllerRoute(
    "default",
    "{controller=Home}/{action=Index}/{id?}");

app.Run();