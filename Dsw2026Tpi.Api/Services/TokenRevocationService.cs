using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Data.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dsw2026Tpi.Api.Services;

public class TokenRevocationService(AuthenticationDbContext dbContext) : ITokenRevocationService
{
    public async Task RevokeAsync(string jti, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        var alreadyRevoked = await dbContext.RevokedTokens
            .AnyAsync(token => token.Jti == jti, cancellationToken);

        if (alreadyRevoked)
            return;

        dbContext.RevokedTokens.Add(new RevokedToken
        {
            Jti = jti,
            ExpiresAtUtc = expiresAtUtc,
            RevokedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        return await dbContext.RevokedTokens
            .AnyAsync(token => token.Jti == jti, cancellationToken);
    }
}
