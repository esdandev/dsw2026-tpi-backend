using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dsw2026Tpi.Data.Identity;

public class AuthenticationDbContext: IdentityDbContext
{
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();
    public AuthenticationDbContext(DbContextOptions<AuthenticationDbContext> options)
            : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(b => { 
            b.ToTable("ApplicationUsers");
            b.HasIndex(user => user.Dni)
                .IsUnique()
                .HasDatabaseName("UX_ApplicationUsers_Dni");
            b.Property(user => user.FirstName).HasMaxLength(100);
            b.Property(user => user.LastName).HasMaxLength(100);
        });
        builder.Entity<IdentityUser>(b => { b.ToTable("Users"); });
        builder.Entity<IdentityRole>(b => { b.ToTable("Roles"); });
        builder.Entity<IdentityUserRole<string>>(b => { b.ToTable("UsersRoles"); });
        builder.Entity<IdentityUserClaim<string>>(b => { b.ToTable("UsersClaims"); });
        builder.Entity<IdentityUserLogin<string>>(b => { b.ToTable("UsersLogins"); });
        builder.Entity<IdentityRoleClaim<string>>(b => { b.ToTable("RolesClaims"); });
        builder.Entity<IdentityUserToken<string>>(b => { b.ToTable("UsersTokens"); });
        builder.Entity<RevokedToken>(b =>
        {
            b.ToTable("RevokedTokens");
            b.HasKey(token => token.Jti);
            b.Property(token => token.Jti).HasMaxLength(64);
            b.HasIndex(token => token.ExpiresAtUtc);
        });
    }

    public override int SaveChanges()
    {
        ApplyTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
