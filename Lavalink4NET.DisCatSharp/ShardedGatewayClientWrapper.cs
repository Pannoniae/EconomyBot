using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.EventArgs;

namespace Lavalink4NET.NetCord;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Clients;

internal sealed class ShardedGatewayClientWrapper : GatewayClientWrapperBase, IDiscordClientWrapper, IDisposable
{
    private readonly DiscordShardedClient _client;
    private readonly TaskCompletionSource _readyTaskCompletionSource;

    public ShardedGatewayClientWrapper(DiscordShardedClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;

        _readyTaskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _client.VoiceStateUpdated += HandleVoiceStateUpdateAsync;
        _client.VoiceServerUpdated += HandleVoiceServerUpdateAsync;
        _client.Ready += HandleShardReadyAsync;
    }

    private async Task HandleShardReadyAsync(DiscordClient client, ReadyEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(eventArgs);

        _readyTaskCompletionSource.TrySetResult();
    }

    private Task HandleVoiceServerUpdateAsync(DiscordClient client, VoiceServerUpdateEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(args);

        return HandleVoiceServerUpdateAsync(client, args);
    }

    private Task HandleVoiceStateUpdateAsync(DiscordClient client, VoiceState state)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(state);

        return HandleVoiceStateUpdateAsync(client, state);
    }

    public void Dispose()
    {
        _client.VoiceStateUpdated -= HandleVoiceStateUpdateAsync;
        _client.VoiceServerUpdated -= HandleVoiceServerUpdateAsync;
    }

    public override async ValueTask<ClientInformation> WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _readyTaskCompletionSource.Task
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ClientInformation("NetCord", _client.CurrentUser.Id, _client.ShardClients.Count);
    }

    protected override DiscordClient GetClient(ulong guildId) => _client.GetShard(guildId: guildId);

    protected override bool TryGetGuild(ulong guildId, [MaybeNullWhen(false)] out DiscordGuild guild)
    {
        return GetClient(guildId).Guilds.TryGetValue(guildId, out guild);
    }
}
