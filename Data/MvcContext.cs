using Microsoft.EntityFrameworkCore;
using TokenBasedScript.Models;

namespace TokenBasedScript.Data;

public class MvcContext : DbContext
{
    public MvcContext (DbContextOptions<MvcContext> options)
        : base(options)
    {
        
    }

    public DbSet<User> Users { get; set; }
}