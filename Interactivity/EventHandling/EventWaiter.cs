using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentCollections;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace DisCatSharp.Interactivity.EventHandling;

/// <summary>
///     Contains arguments passed to an asynchronous event.
/// </summary>
public class AsyncEventArgs : EventArgs {
    /// <summary>
    ///     <para>Gets or sets whether this event was handled.</para>
    ///     <para>Setting this to true will prevent other handlers from running.</para>
    /// </summary>
    public bool Handled { get; set; } = false;
}

/// <summary>
///     ABC for <see cref="AsyncEvent{TSender, TArgs}" />, allowing for using instances thereof without knowing the
///     underlying instance's type parameters.
/// </summary>
public abstract class AsyncEvent {
    /// <summary>
    ///     Prevents a default instance of the <see cref="AsyncEvent" /> class from being created.
    /// </summary>
    /// <param name="name">The name.</param>
    private protected AsyncEvent(string name) {
        this.Name = name;
    }

    /// <summary>
    ///     Gets the name of this event.
    /// </summary>
    public string Name { get; }
}

/// <summary>
///     Defines the behaviour for throwing exceptions from
///     <see cref="AsyncEvent{TSender, TArgs}.InvokeAsync(TSender, TArgs, AsyncEventExceptionMode)" />.
/// </summary>
[Flags]
public enum AsyncEventExceptionMode {
    /// <summary>
    ///     Defines that no exceptions should be thrown. Only exception handlers will be used.
    /// </summary>
    IgnoreAll = 0,

    /// <summary>
    ///     Defines that only fatal (i.e. non-<see cref="AsyncEventTimeoutException{TSender, TArgs}" />) exceptions
    ///     should be thrown.
    /// </summary>
    ThrowFatal = 1,

    /// <summary>
    ///     Defines that only non-fatal (i.e. <see cref="AsyncEventTimeoutException{TSender, TArgs}" />) exceptions
    ///     should be thrown.
    /// </summary>
    ThrowNonFatal = 2,

    /// <summary>
    ///     Defines that all exceptions should be thrown. This is equivalent to combining <see cref="ThrowFatal" /> and
    ///     <see cref="ThrowNonFatal" /> flags.
    /// </summary>
    ThrowAll = ThrowFatal | ThrowNonFatal,

    /// <summary>
    ///     Defines that only fatal (i.e. non-<see cref="AsyncEventTimeoutException{TSender, TArgs}" />) exceptions
    ///     should be handled by the specified exception handler.
    /// </summary>
    HandleFatal = 4,

    /// <summary>
    ///     Defines that only non-fatal (i.e. <see cref="AsyncEventTimeoutException{TSender, TArgs}" />) exceptions
    ///     should be handled by the specified exception handler.
    /// </summary>
    HandleNonFatal = 8,

    /// <summary>
    ///     Defines that all exceptions should be handled by the specified exception handler. This is equivalent to
    ///     combining <see cref="HandleFatal" /> and <see cref="HandleNonFatal" /> flags.
    /// </summary>
    HandleAll = HandleFatal | HandleNonFatal,

    /// <summary>
    ///     Defines that all exceptions should be thrown and handled by the specified exception handler. This is
    ///     equivalent to combining <see cref="HandleAll" /> and <see cref="ThrowAll" /> flags.
    /// </summary>
    ThrowAllHandleAll = ThrowAll | HandleAll,

    /// <summary>
    ///     Default mode, equivalent to <see cref="HandleAll" />.
    /// </summary>
    Default = HandleAll
}

/// <summary>
///     Handles any exception raised by an <see cref="AsyncEvent{TSender, TArgs}" /> or its handlers.
/// </summary>
/// <typeparam name="TSender">Type of the object that dispatches this event.</typeparam>
/// <typeparam name="TArgs">Type of the object which holds arguments for this event.</typeparam>
/// <param name="asyncEvent">Asynchronous event which threw the exception.</param>
/// <param name="exception">Exception that was thrown</param>
/// <param name="handler">Handler which threw the exception.</param>
/// <param name="sender">Object which dispatched the event.</param>
/// <param name="eventArgs">Arguments with which the event was dispatched.</param>
public delegate void AsyncEventExceptionHandler<TSender, TArgs>(AsyncEvent<TSender, TArgs> asyncEvent, Exception exception, AsyncEventHandler<TSender, TArgs> handler, TSender sender, TArgs eventArgs)
    where TArgs : AsyncEventArgs;

/// <summary>
///     Implementation of asynchronous event. The handlers of such events are executed asynchronously, but sequentially.
/// </summary>
/// <typeparam name="TSender">Type of the object that dispatches this event.</typeparam>
/// <typeparam name="TArgs">Type of event argument object passed to this event's handlers.</typeparam>
/// <remarks>
///     Creates a new asynchronous event with specified name and exception handler.
/// </remarks>
/// <param name="name">Name of this event.</param>
/// <param name="maxExecutionTime">Maximum handler execution time. A value of <see cref="TimeSpan.Zero" /> means infinite.</param>
/// <param name="exceptionHandler">Delegate which handles exceptions caused by this event.</param>
public sealed class AsyncEvent<TSender, TArgs>(string name, TimeSpan maxExecutionTime, AsyncEventExceptionHandler<TSender, TArgs>? exceptionHandler) : AsyncEvent(name)
    where TArgs : AsyncEventArgs {
    /// <summary>
    ///     Gets or sets the exception handler.
    /// </summary>
    private readonly AsyncEventExceptionHandler<TSender, TArgs>? _exceptionHandler = exceptionHandler;

    /// <summary>
    ///     Gets the lock.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    ///     Gets or sets the event handlers.
    /// </summary>
    private ImmutableArray<AsyncEventHandler<TSender, TArgs>> _handlers = [];

    /// <summary>
    ///     Gets the maximum allotted execution time for all handlers. Any event which causes the handler to time out
    ///     will raise a non-fatal <see cref="AsyncEventTimeoutException{TSender, TArgs}" />.
    /// </summary>
    public TimeSpan MaximumExecutionTime { get; } = maxExecutionTime;

    /// <summary>
    ///     Registers a new handler for this event.
    /// </summary>
    /// <param name="handler">Handler to register for this event.</param>
    public void Register(AsyncEventHandler<TSender, TArgs> handler) {
        ArgumentNullException.ThrowIfNull(handler);

        lock (this._lock) {
            this._handlers = this._handlers.Add(handler);
        }
    }

    /// <summary>
    ///     Unregisters an existing handler from this event.
    /// </summary>
    /// <param name="handler">Handler to unregister from the event.</param>
    public void Unregister(AsyncEventHandler<TSender, TArgs> handler) {
        ArgumentNullException.ThrowIfNull(handler);

        lock (this._lock) {
            this._handlers = this._handlers.Remove(handler);
        }
    }

    /// <summary>
    ///     Unregisters all existing handlers from this event.
    /// </summary>
    public void UnregisterAll() => this._handlers = [];

    /// <summary>
    ///     <para>Raises this event by invoking all of its registered handlers, in order of registration.</para>
    ///     <para>All exceptions throw during invocation will be handled by the event's registered exception handler.</para>
    /// </summary>
    /// <param name="sender">Object which raised this event.</param>
    /// <param name="e">Arguments for this event.</param>
    /// <param name="exceptionMode">Defines what to do with exceptions caught from handlers.</param>
    /// <returns></returns>
    public async Task InvokeAsync(TSender sender, TArgs e, AsyncEventExceptionMode exceptionMode = AsyncEventExceptionMode.Default) {
        var handlers = this._handlers;
        if (handlers.Length == 0)
            return;

        // Collect exceptions
        List<Exception> exceptions = [];
        if ((exceptionMode & AsyncEventExceptionMode.ThrowAll) != 0)
            exceptions = new(handlers.Length * 2 /* timeout + regular */);

        // If we have a timeout configured, start the timeout task
        var timeout = this.MaximumExecutionTime > TimeSpan.Zero ? Task.Delay(this.MaximumExecutionTime) : null;
        foreach (var handler in handlers)
            try {
                // Start the handler execution
                var handlerTask = handler(sender, e);
                if (handlerTask != null && timeout != null) {
                    // If timeout is configured, wait for any task to finish
                    // If the timeout task finishes first, the handler is causing a timeout
                    var result = await Task.WhenAny(timeout, handlerTask).ConfigureAwait(false);
                    if (result == timeout) {
                        timeout = null;
                        var timeoutEx = new AsyncEventTimeoutException<TSender, TArgs>(this, handler);

                        // Notify about the timeout and complete execution
                        if ((exceptionMode & AsyncEventExceptionMode.HandleNonFatal) == AsyncEventExceptionMode.HandleNonFatal)
                            this.HandleException(timeoutEx, handler, sender, e);

                        if ((exceptionMode & AsyncEventExceptionMode.ThrowNonFatal) == AsyncEventExceptionMode.ThrowNonFatal)
                            exceptions.Add(timeoutEx);

                        await handlerTask.ConfigureAwait(false);
                    }
                }
                else if (handlerTask != null)
                    // No timeout is configured, or timeout already expired, proceed as usual
                    await handlerTask.ConfigureAwait(false);

                if (e.Handled)
                    break;
            }
            catch (Exception ex) {
                e.Handled = false;

                if ((exceptionMode & AsyncEventExceptionMode.HandleFatal) == AsyncEventExceptionMode.HandleFatal)
                    this.HandleException(ex, handler, sender, e);

                if ((exceptionMode & AsyncEventExceptionMode.ThrowFatal) == AsyncEventExceptionMode.ThrowFatal)
                    exceptions.Add(ex);
            }

        if ((exceptionMode & AsyncEventExceptionMode.ThrowAll) != 0 && exceptions.Count > 0)
            throw new AggregateException("Exceptions were thrown during execution of the event's handlers.", exceptions);
    }

    /// <summary>
    ///     Handles the exception.
    /// </summary>
    /// <param name="ex">The ex.</param>
    /// <param name="handler">The handler.</param>
    /// <param name="sender">The sender.</param>
    /// <param name="args">The args.</param>
    private void HandleException(Exception ex, AsyncEventHandler<TSender, TArgs> handler, TSender sender, TArgs args)
        => this._exceptionHandler?.Invoke(this, ex, handler, sender, args);
}

/// <summary>
///     ABC for <see cref="AsyncEventHandler{TSender, TArgs}" />, allowing for using instances thereof without knowing the
///     underlying instance's type parameters.
/// </summary>
public abstract class AsyncEventTimeoutException : Exception {
    /// <summary>
    ///     Prevents a default instance of the <see cref="AsyncEventTimeoutException" /> class from being created.
    /// </summary>
    /// <param name="asyncEvent">The async event.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="message">The message.</param>
    private protected AsyncEventTimeoutException(AsyncEvent asyncEvent, AsyncEventHandler<object, AsyncEventArgs>? eventHandler, string message)
        : base(message) {
        this.Event = asyncEvent;
        this.Handler = eventHandler;
    }

    /// <summary>
    ///     Gets the event the invocation of which caused the timeout.
    /// </summary>
    public AsyncEvent Event { get; }

    /// <summary>
    ///     Gets the handler which caused the timeout.
    /// </summary>
    public AsyncEventHandler<object, AsyncEventArgs>? Handler { get; }
}

/// <summary>
///     <para>
///         Thrown whenever execution of an <see cref="AsyncEventHandler{TSender, TArgs}" /> exceeds maximum time
///         allowed.
///     </para>
///     <para>This is a non-fatal exception, used primarily to inform users that their code is taking too long to execute.</para>
/// </summary>
/// <typeparam name="TSender">Type of sender that dispatched this asynchronous event.</typeparam>
/// <typeparam name="TArgs">Type of event arguments for the asynchronous event.</typeparam>
/// <remarks>
///     Creates a new timeout exception for specified event and handler.
/// </remarks>
/// <param name="asyncEvent">Event the execution of which timed out.</param>
/// <param name="eventHandler">Handler which timed out.</param>
public sealed class AsyncEventTimeoutException<TSender, TArgs>(AsyncEvent asyncEvent, AsyncEventHandler<TSender, TArgs> eventHandler)
    : AsyncEventTimeoutException(asyncEvent, eventHandler as AsyncEventHandler<object, AsyncEventArgs>, "An event handler caused the invocation of an asynchronous event to time out.")
    where TArgs : AsyncEventArgs {
    /// <summary>
    ///     Gets the event the invocation of which caused the timeout.
    /// </summary>
    public new AsyncEvent<TSender, TArgs> Event => base.Event as AsyncEvent<TSender, TArgs> ?? throw new NullReferenceException();

    /// <summary>
    ///     Gets the handler which caused the timeout.
    /// </summary>
    public new AsyncEventHandler<TSender, TArgs> Handler => base.Handler as AsyncEventHandler<TSender, TArgs> ?? throw new NullReferenceException();
}

// source: https://blogs.msdn.microsoft.com/pfxteam/2012/02/11/building-async-coordination-primitives-part-1-asyncmanualresetevent/
/// <summary>
///     Implements an async version of a <see cref="ManualResetEvent" />
///     This class does currently not support Timeouts or the use of CancellationTokens
/// </summary>
internal class AsyncManualResetEvent {
    /// <summary>
    ///     The task completion source.
    /// </summary>
    private TaskCompletionSource<bool> _tsc;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncManualResetEvent" /> class.
    /// </summary>
    public AsyncManualResetEvent()
        : this(false) {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncManualResetEvent" /> class.
    /// </summary>
    /// <param name="initialState">If true, initial state.</param>
    public AsyncManualResetEvent(bool initialState) {
        this._tsc = new();

        if (initialState) this._tsc.TrySetResult(true);
    }

    /// <summary>
    ///     Gets a value indicating whether this is set.
    /// </summary>
    public bool IsSet => this._tsc is { Task.IsCompleted: true };

    /// <summary>
    ///     Waits the async waiter.
    /// </summary>
    public Task WaitAsync() => this._tsc.Task;

    /// <summary>
    ///     Sets the async task.
    /// </summary>
    public Task SetAsync() => Task.Run(() => this._tsc.TrySetResult(true));

    /// <summary>
    ///     Resets the async waiter.
    /// </summary>
    public void Reset() {
        while (true) {
            var tsc = this._tsc;

            if (!tsc.Task.IsCompleted || Interlocked.CompareExchange(ref this._tsc, new(), tsc) == tsc)
                return;
        }
    }
}

/// <summary>
///     Handles an asynchronous event of type <see cref="AsyncEvent{TSender, TArgs}" />. The handler will take an instance
///     of <typeparamref name="TArgs" /> as its arguments.
/// </summary>
/// <typeparam name="TSender">Type of the object that dispatches this event.</typeparam>
/// <typeparam name="TArgs">Type of the object which holds arguments for this event.</typeparam>
/// <param name="sender">Object which raised this event.</param>
/// <param name="e">Arguments for this event.</param>
/// <returns></returns>
public delegate Task AsyncEventHandler<in TSender, in TArgs>(TSender sender, TArgs e) where TArgs : AsyncEventArgs;

/// <summary>
///     EventWaiter is a class that serves as a layer between the InteractivityExtension
///     and the GatewayClient to listen to an event and check for matches to a predicate.
/// </summary>
/// <typeparam name="T"></typeparam>
internal class EventWaiter<T> : IDisposable where T : AsyncEventArgs {
    private GatewayClient _client;
    private ConcurrentHashSet<CollectRequest<T>> _collectRequests;
    private bool _disposed;
    private AsyncEvent<GatewayClient, T> _event;
    private AsyncEventHandler<GatewayClient, T> _handler;
    private ConcurrentHashSet<MatchRequest<T>> _matchRequests;

    /// <summary>
    ///     Creates a new EventWaiter object.
    /// </summary>
    /// <param name="client">Your GatewayClient</param>
    public EventWaiter(GatewayClient client) {
        this._client = client;
        var tinfo = this._client.GetType().GetTypeInfo();
        var handler = tinfo.DeclaredFields.First(x => x.FieldType == typeof(AsyncEvent<GatewayClient, T>));
        this._matchRequests = [];
        this._collectRequests = [];
        this._event = (AsyncEvent<GatewayClient, T>)handler.GetValue(this._client);
        this._handler = this.HandleEvent;
        this._event.Register(this._handler);
    }

    /// <summary>
    ///     Disposes this EventWaiter
    /// </summary>
    public void Dispose() {
        this._disposed = true;
        this._event?.Unregister(this._handler);

        this._event = null;
        this._handler = null;
        this._client = null;

        this._matchRequests?.Clear();
        this._collectRequests?.Clear();

        this._matchRequests = null;
        this._collectRequests = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Waits for a match to a specific request, else returns null.
    /// </summary>
    /// <param name="request">Request to match</param>
    /// <returns></returns>
    public async Task<T> WaitForMatchAsync(MatchRequest<T> request) {
        T result = null;
        this._matchRequests.Add(request);
        try {
            result = await request.Tcs.Task.ConfigureAwait(false);
        }
        catch (Exception ex) {
            Console.Out.WriteLine("An exception occurred while waiting for {0} {1}", typeof(T).Name, ex);
        }
        finally {
            request.Dispose();
            this._matchRequests.TryRemove(request);
        }

        return result;
    }

    /// <summary>
    ///     Collects the matches async.
    /// </summary>
    /// <param name="request">The request.</param>
    public async Task<ReadOnlyCollection<T>> CollectMatchesAsync(CollectRequest<T> request) {
        ReadOnlyCollection<T> result = null;
        this._collectRequests.Add(request);
        try {
            await request.Tcs.Task.ConfigureAwait(false);
        }
        catch (Exception ex) {
            Console.Out.WriteLine("An exception occurred while collecting from {0} {1}", typeof(T).Name, ex);
        }
        finally {
            result = new(new HashSet<T>(request.Collected).ToList());
            request.Dispose();
            this._collectRequests.TryRemove(request);
        }

        return result;
    }

    /// <summary>
    ///     Handles the event.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="eventArgs">The event's arguments.</param>
    private Task HandleEvent(GatewayClient client, T eventArgs) {
        if (!this._disposed) {
            foreach (var req in this._matchRequests)
                if (req.Predicate(eventArgs))
                    req.Tcs.TrySetResult(eventArgs);

            foreach (var req in this._collectRequests)
                if (req.Predicate(eventArgs))
                    req.Collected.Add(eventArgs);
        }

        return Task.CompletedTask;
    }

    ~EventWaiter() {
        this.Dispose();
    }
}