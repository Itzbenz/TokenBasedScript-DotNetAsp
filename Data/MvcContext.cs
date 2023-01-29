using Microsoft.EntityFrameworkCore;
using TokenBasedScript.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

#pragma warning disable CS8618
namespace TokenBasedScript.Data;

public class MvcContext : IdentityDbContext<User>
{
    public MvcContext(DbContextOptions<MvcContext> options)
        : base(options)
    {
    }

    public new DbSet<User> Users { get; set; }
    public DbSet<ScriptExecution> ScriptExecutions { get; set; }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = new())
    {
        SyncObjectsStatePreCommit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }


    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SyncObjectsStatePreCommit();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void SyncObjectsStatePreCommit()
    {
        foreach (var auditableEntity in ChangeTracker.Entries<ITrackableEntity>())
            if (auditableEntity.State is EntityState.Added or EntityState.Modified)
            {
                var currentDate = DateTime.Now;
                auditableEntity.Entity.DateModified = currentDate;

                // populate created date and created by columns for
                // newly added record.
                if (auditableEntity.State == EntityState.Added)
                    auditableEntity.Entity.DateCreated = currentDate;
                else
                    // we also want to make sure that code is not inadvertently
                    // modifying created date and created by columns 
                    auditableEntity.Property(p => p.DateCreated).IsModified = false;
            }
    }
}