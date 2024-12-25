using DisCatSharp;

namespace Lavalink4NET.DisCatSharp;

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class HostBuilderExtensions
{
    public static IHostBuilder UseLavalink(this IHostBuilder hostBuilder)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        return hostBuilder.ConfigureServices(static (_, services) => services.AddLavalink());
    }

    public static IHostBuilder UseLavalink(this IHostBuilder hostBuilder, Action<AudioServiceOptions>? configure, DiscordClient client)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        return hostBuilder.ConfigureServices((_, services) =>
        {
            services.AddLavalink();
            services.AddSingleton(client);

            if (configure is not null)
            {
                services.Configure(configure);
            }
        });
    }
}
