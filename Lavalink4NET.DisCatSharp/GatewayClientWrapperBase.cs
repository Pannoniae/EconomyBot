using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.EventArgs;
using Lavalink4NET.Protocol.Requests;

namespace Lavalink4NET.DisCatSharp;

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Clients;
using Lavalink4NET.Clients.Events;
using Lavalink4NET.Events;

internal abstract class GatewayClientWrapperBase : IDiscordClientWrapper
{
    public event AsyncEventHandler<VoiceServerUpdatedEventArgs>? VoiceServerUpdated;

    public event AsyncEventHandler<VoiceStateUpdatedEventArgs>? VoiceStateUpdated;

    public ValueTask<ImmutableArray<ulong>> GetChannelUsersAsync(ulong guildId, ulong voiceChannelId, bool includeBots = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetGuild(guildId, out var guild))
        {
            return new ValueTask<ImmutableArray<ulong>>([]);
        }

        var currentUserId = GetClient(guildId).CurrentUser.Id;

        var voiceStates = guild.VoiceStates
            .Where(x => x.Value.ChannelId == voiceChannelId)
            .Where(x => x.Value.UserId != currentUserId);

        if (!includeBots)
        {
            voiceStates = voiceStates.Where(x => x.Value.User is not { IsBot: true, });
        }

        var userIds = voiceStates.Select(x => x.Value.UserId).ToImmutableArray();
        return new ValueTask<ImmutableArray<ulong>>(userIds);
    }

    public async ValueTask SendVoiceUpdateAsync(ulong guildId, ulong? voiceChannelId, bool selfDeaf = false, bool selfMute = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await GetClient(guildId)
            .Guilds[guildId].CurrentMember
            .SetMuteAsync(selfMute)
            .ConfigureAwait(false);

        await GetClient(guildId)
            .Guilds[guildId].CurrentMember
            .SetDeafAsync(selfDeaf)
            .ConfigureAwait(false);
    }

    protected abstract bool TryGetGuild(ulong guildId, [MaybeNullWhen(false)] out DiscordGuild guild);

    protected abstract DiscordClient GetClient(ulong guildId);

    protected Task HandleVoiceServerUpdateAsync(DiscordClient client, VoiceServerUpdateEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (eventArgs.Endpoint is null)
        {
            return default;
        }

        var voiceServerUpdatedEventArgs = new VoiceServerUpdatedEventArgs(
            guildId: eventArgs.Guild.Id,
            voiceServer: new VoiceServer(eventArgs.VoiceToken, eventArgs.Endpoint));

        return VoiceServerUpdated.InvokeAsync(this, voiceServerUpdatedEventArgs).AsTask();
    }

    protected async Task HandleVoiceStateUpdateAsync(DiscordClient client, VoiceStateUpdateEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        // Retrieve previous voice state from cache
        var previousVoiceState = TryGetGuild(eventArgs.Guild.Id, out var guild)
            && guild.VoiceStates.TryGetValue(eventArgs.User.Id, out var previousVoiceStateData)
            ? new Clients.VoiceState(VoiceChannelId: previousVoiceStateData.ChannelId, SessionId: previousVoiceStateData.SessionId)
            : default;

        var currentUserId = GetClient(eventArgs.Guild.Id).CurrentUser.Id;

        var updatedVoiceState = new Clients.VoiceState(
            VoiceChannelId: eventArgs.Channel.Id,
            SessionId: eventArgs.SessionId);

        var voiceStateUpdatedEventArgs = new VoiceStateUpdatedEventArgs(
            eventArgs.Guild.Id,
            eventArgs.User.Id,
            eventArgs.User.Id == currentUserId,
            updatedVoiceState,
            previousVoiceState);

        await VoiceStateUpdated
            .InvokeAsync(this, voiceStateUpdatedEventArgs)
            .ConfigureAwait(false);
    }

    public abstract ValueTask<ClientInformation> WaitForReadyAsync(CancellationToken cancellationToken = default);
}
