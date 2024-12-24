using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using ConcurrentCollections;

using DisCatSharp.Common.Utilities;
using DisCatSharp.Entities;
using DisCatSharp.EventArgs;

using Microsoft.Extensions.Logging;

namespace DisCatSharp.Interactivity.EventHandling;

/// <summary>
///     EventWaiter is a class that serves as a layer between the InteractivityExtension
///     and the GatewayClient to listen to an event and check for matches to a predicate.
/// </summary>
internal class ReactionCollector : IDisposable
{
	private GatewayClient _client;

	private AsyncEvent<GatewayClient, MessageReactionAddEventArgs> _reactionAddEvent;
	private AsyncEventHandler<GatewayClient, MessageReactionAddEventArgs> _reactionAddHandler;

	private AsyncEvent<GatewayClient, MessageReactionsClearEventArgs> _reactionClearEvent;
	private AsyncEventHandler<GatewayClient, MessageReactionsClearEventArgs> _reactionClearHandler;

	private AsyncEvent<GatewayClient, MessageReactionRemoveEventArgs> _reactionRemoveEvent;
	private AsyncEventHandler<GatewayClient, MessageReactionRemoveEventArgs> _reactionRemoveHandler;

	private ConcurrentHashSet<ReactionCollectRequest> _requests;

	/// <summary>
	///     Creates a new EventWaiter object.
	/// </summary>
	/// <param name="client">Your GatewayClient</param>
	public ReactionCollector(GatewayClient client)
	{
		this._client = client;
		var tinfo = this._client.GetType().GetTypeInfo();

		this._requests = [];

		// Grabbing all three events from client
		var handler = tinfo.DeclaredFields.First(x => x.FieldType == typeof(AsyncEvent<GatewayClient, MessageReactionAddEventArgs>));

		this._reactionAddEvent = (AsyncEvent<GatewayClient, MessageReactionAddEventArgs>)handler.GetValue(this._client);
		this._reactionAddHandler = this.HandleReactionAdd;
		this._reactionAddEvent.Register(this._reactionAddHandler);

		handler = tinfo.DeclaredFields.First(x => x.FieldType == typeof(AsyncEvent<GatewayClient, MessageReactionRemoveEventArgs>));

		this._reactionRemoveEvent = (AsyncEvent<GatewayClient, MessageReactionRemoveEventArgs>)handler.GetValue(this._client);
		this._reactionRemoveHandler = this.HandleReactionRemove;
		this._reactionRemoveEvent.Register(this._reactionRemoveHandler);

		handler = tinfo.DeclaredFields.First(x => x.FieldType == typeof(AsyncEvent<GatewayClient, MessageReactionsClearEventArgs>));

		this._reactionClearEvent = (AsyncEvent<GatewayClient, MessageReactionsClearEventArgs>)handler.GetValue(this._client);
		this._reactionClearHandler = this.HandleReactionClear;
		this._reactionClearEvent.Register(this._reactionClearHandler);
	}

	/// <summary>
	///     Disposes this EventWaiter
	/// </summary>
	public void Dispose()
	{
		this._client = null;

		this._reactionAddEvent.Unregister(this._reactionAddHandler);
		this._reactionRemoveEvent.Unregister(this._reactionRemoveHandler);
		this._reactionClearEvent.Unregister(this._reactionClearHandler);

		this._reactionAddEvent = null;
		this._reactionAddHandler = null;
		this._reactionRemoveEvent = null;
		this._reactionRemoveHandler = null;
		this._reactionClearEvent = null;
		this._reactionClearHandler = null;

		this._requests.Clear();
		this._requests = null;
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Collects the async.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <returns>A Task.</returns>
	public async Task<ReadOnlyCollection<Reaction>> CollectAsync(ReactionCollectRequest request)
	{
		this._requests.Add(request);
		var result = (ReadOnlyCollection<Reaction>)null;

		try
		{
			await request.Tcs.Task.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			this._client.Logger.LogError(InteractivityEvents.InteractivityCollectorError, ex, "Exception occurred while collecting reactions");
		}
		finally
		{
			result = new(new HashSet<Reaction>(request.Collected).ToList());
			request.Dispose();
			this._requests.TryRemove(request);
		}

		return result;
	}

	/// <summary>
	///     Handles the reaction add.
	/// </summary>
	/// <param name="client">The client.</param>
	/// <param name="eventArgs">The event's arguments.</param>
	/// <returns>A Task.</returns>
	private Task HandleReactionAdd(GatewayClient client, MessageReactionAddEventArgs eventArgs)
	{
		// foreach request add
		foreach (var req in this._requests)
			if (req.Message.Id == eventArgs.Message.Id)
			{
				if (req.Collected.Any(x => x.Emoji == eventArgs.Emoji && x.Users.Any(y => y.Id == eventArgs.User.Id)))
				{
					var reaction = req.Collected.First(x => x.Emoji == eventArgs.Emoji && x.Users.Any(y => y.Id == eventArgs.User.Id));
					req.Collected.TryRemove(reaction);
					reaction.Users.Add(eventArgs.User);
					req.Collected.Add(reaction);
				}
				else
					req.Collected.Add(new()
					{
						Emoji = eventArgs.Emoji,
						Users = [eventArgs.User]
					});
			}

		return Task.CompletedTask;
	}

	/// <summary>
	///     Handles the reaction remove.
	/// </summary>
	/// <param name="client">The client.</param>
	/// <param name="eventArgs">The event's arguments.</param>
	/// <returns>A Task.</returns>
	private Task HandleReactionRemove(GatewayClient client, MessageReactionRemoveEventArgs eventArgs)
	{
		// foreach request remove
		foreach (var req in this._requests)
			if (req.Message.Id == eventArgs.Message.Id)
				if (req.Collected.Any(x => x.Emoji == eventArgs.Emoji && x.Users.Any(y => y.Id == eventArgs.User.Id)))
				{
					var reaction = req.Collected.First(x => x.Emoji == eventArgs.Emoji && x.Users.Any(y => y.Id == eventArgs.User.Id));
					req.Collected.TryRemove(reaction);
					reaction.Users.TryRemove(eventArgs.User);
					if (reaction.Users.Count > 0)
						req.Collected.Add(reaction);
				}

		return Task.CompletedTask;
	}

	/// <summary>
	///     Handles the reaction clear.
	/// </summary>
	/// <param name="client">The client.</param>
	/// <param name="eventArgs">The event's arguments.</param>
	/// <returns>A Task.</returns>
	private Task HandleReactionClear(GatewayClient client, MessageReactionsClearEventArgs eventArgs)
	{
		// foreach request add
		foreach (var req in this._requests)
			if (req.Message.Id == eventArgs.Message.Id)
				req.Collected.Clear();
		return Task.CompletedTask;
	}

	~ReactionCollector()
	{
		this.Dispose();
	}
}

/// <summary>
///     The reaction collect request.
/// </summary>
public class ReactionCollectRequest : IDisposable
{
	internal ConcurrentHashSet<Reaction> Collected;
	internal CancellationTokenSource Ct;
	internal RestMessage Message;
	internal TaskCompletionSource<Reaction> Tcs;
	internal TimeSpan Timeout;

	/// <summary>
	///     Initializes a new instance of the <see cref="ReactionCollectRequest" /> class.
	/// </summary>
	/// <param name="msg">The msg.</param>
	/// <param name="timeout">The timeout.</param>
	public ReactionCollectRequest(RestMessage msg, TimeSpan timeout)
	{
		this.Message = msg;
		this.Collected = [];
		this.Timeout = timeout;
		this.Tcs = new();
		this.Ct = new(this.Timeout);
		this.Ct.Token.Register(() => this.Tcs.TrySetResult(null));
	}

	/// <summary>
	///     Disposes the.
	/// </summary>
	public void Dispose()
	{
		GC.SuppressFinalize(this);
		this.Ct.Dispose();
		this.Tcs = null;
		this.Message = null;
		this.Collected?.Clear();
		this.Collected = null;
	}

	~ReactionCollectRequest()
	{
		this.Dispose();
	}
}

/// <summary>
///     The reaction.
/// </summary>
public class Reaction
{
	/// <summary>
	///     Gets the emoji.
	/// </summary>
	public EmojiProperties Emoji { get; internal set; }

	/// <summary>
	///     Gets the users.
	/// </summary>
	public ConcurrentHashSet<User> Users { get; internal set; }

	/// <summary>
	///     Gets the total.
	/// </summary>
	public int Total => this.Users.Count;
}
