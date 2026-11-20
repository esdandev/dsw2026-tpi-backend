using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Dsw2026Tpi.Api.Configurations;

public static class ForwardedHeadersConfigurationExtensions
{
    public static IApplicationBuilder UseAppForwardedHeaders(this IApplicationBuilder app, IConfiguration configuration)
    {
        var options = BuildForwardedHeadersOptions(configuration);

        return options is null ? app : app.UseForwardedHeaders(options);
    }

    public static ForwardedHeadersOptions? BuildForwardedHeadersOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection("ForwardedHeaders");

        if (!section.GetValue<bool>("Enabled"))
            return null;

        // Solo se confían los proxies configurados; si se aceptara cualquier origen,
        // el cliente podría falsificar X-Forwarded-For y saltarse el rate limiting por IP.
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1
        };

        var knownProxies = section.GetSection("KnownProxies").Get<string[]>() ?? [];

        if (knownProxies.Length == 0)
            return options;

        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();

        foreach (var proxy in knownProxies)
        {
            if (!IPAddress.TryParse(proxy, out var address))
                throw new InvalidOperationException($"ForwardedHeaders:KnownProxies contiene una IP inválida: '{proxy}'.");

            options.KnownProxies.Add(address);
        }

        return options;
    }
}
