using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.EventArgs;

namespace Lavalink4NET.NetCord;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Clients;

internal sealed class GatewayClientWrapper : GatewayClientWrapperBase, IDiscordClientWrapper, IDisposable
{
    private readonly DiscordClient _client;

    private readonly TaskCompletionSource<bool> _ready = new();

    public GatewayClientWrapper(DiscordClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;

        _client.VoiceStateUpdated += HandleVoiceStateUpdateAsync;
        _client.VoiceServerUpdated += HandleVoiceServerUpdateAsync;
        _client.Ready += HandleReady;
    }

    private async Task HandleReady(DiscordClient sender, ReadyEventArgs readyEventArgs) {
        _ready.TrySetResult(true);
    }

    public void Dispose()
    {
        _client.VoiceStateUpdated -= HandleVoiceStateUpdateAsync;
        _client.VoiceServerUpdated -= HandleVoiceServerUpdateAsync;
    }

    public override async ValueTask<ClientInformation> WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _ready.Task
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        var shardCount = _client.ShardCount;

        return new ClientInformation("NetCord", _client.CurrentUser.Id, shardCount);
    }

    protected override DiscordClient GetClient(ulong guildId) => _client;

    protected override bool TryGetGuild(ulong guildId, [MaybeNullWhen(false)] out DiscordGuild guild)
    {
        return _client.Guilds.TryGetValue(guildId, out guild);
    }
}
