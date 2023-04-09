using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TokenBasedScript.Data;

public class MvcContextFactory : IDesignTimeDbContextFactory<MvcContext>
{
    public MvcContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();
        var optionsBuilder = new DbContextOptionsBuilder<MvcContext>();
        var connectionString = configuration.GetConnectionString("MvcContext");
        Debug.Assert(connectionString != null, nameof(connectionString) + " != null");
        if (connectionString.StartsWith("Data Source="))
            optionsBuilder.UseSqlite(connectionString);
        else
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

        return new MvcContext(optionsBuilder.Options);
    }

}