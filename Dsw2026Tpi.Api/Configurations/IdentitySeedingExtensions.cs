using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.Api.Configurations;
using Dsw2026Tpi.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Serilog;

namespace Dsw2026Tpi.Api.Configurations;

public static class IdentitySeedingExtensions
{
    public static async Task SeedRolesAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in new[] { Roles.Administrator, Roles.Patient })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    public static async Task SeedAdminAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var email = configuration["Seed:Admin:Email"];
        var password = configuration["Seed:Admin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Log.Warning("Seed de administrador omitido: configurá 'Seed:Admin:Email' y 'Seed:Admin:Password'.");
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        const long dni = 99999999;

        var admin = await userManager.FindByEmailAsync(email);

        if (admin != null)
            return;

        admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Dni = dni,
            Deleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, password);

        if (!result.Succeeded)
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(admin, Roles.Administrator);
    }
    public static async Task SeedIdentityAsync(this WebApplication app)
    {
        await app.SeedRolesAsync();
        await app.SeedAdminAsync();
    }
}