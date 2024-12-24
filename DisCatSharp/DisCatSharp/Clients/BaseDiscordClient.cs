using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

using DisCatSharp.Attributes;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.Exceptions;
using DisCatSharp.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

using Newtonsoft.Json.Linq;

namespace DisCatSharp;

/// <summary>
///     Represents a common base for various Discord Client implementations.
/// </summary>
public abstract class BaseDiscordClient : IDisposable
{
	/// <summary>
	///     Gets the lazy voice regions.
	/// </summary>
	internal Lazy<IReadOnlyDictionary<string, DiscordVoiceRegion>> VoiceRegionsLazy;

	/// <summary>
	///     Initializes this Discord API client.
	/// </summary>
	/// <param name="config">Configuration for this client.</param>
	protected BaseDiscordClient(DiscordConfiguration config)
	{
		this.Configuration = new(config);
		this.ServiceProvider = config.ServiceProvider;
		if (this.Configuration.CustomSentryDsn != null)
			SentryDsn = this.Configuration.CustomSentryDsn;
		if (this.ServiceProvider is not null)
		{
			this.Configuration.LoggerFactory ??= config.ServiceProvider.GetService<ILoggerFactory>()!;
			this.Logger = config.ServiceProvider.GetService<ILogger<BaseDiscordClient>>()!;
		}

		switch (this.Configuration.LoggerFactory)
		{
			case null when !this.Configuration.EnableSentry:
				this.Configuration.LoggerFactory = new DefaultLoggerFactory();
				this.Configuration.LoggerFactory.AddProvider(new DefaultLoggerProvider(this));
				break;
			case null when this.Configuration.EnableSentry:
			{
				var configureNamedOptions = new ConfigureNamedOptions<ConsoleLoggerOptions>(string.Empty, x =>
				{
#pragma warning disable CS0618 // Type or member is obsolete
					x.TimestampFormat = this.Configuration.LogTimestampFormat;
#pragma warning restore CS0618 // Type or member is obsolete
					x.LogToStandardErrorThreshold = this.Configuration.MinimumLogLevel;
				});
				var optionsFactory = new OptionsFactory<ConsoleLoggerOptions>([configureNamedOptions], []);
				var optionsMonitor = new OptionsMonitor<ConsoleLoggerOptions>(optionsFactory, [], new OptionsCache<ConsoleLoggerOptions>());
				/*
			var configureFormatterOptions = new ConfigureNamedOptions<ConsoleFormatterOptions>(string.Empty, x => { x.TimestampFormat = this.Configuration.LogTimestampFormat; });
			var formatterFactory = new OptionsFactory<ConsoleFormatterOptions>(new[] { configureFormatterOptions }, Enumerable.Empty<IPostConfigureOptions<ConsoleFormatterOptions>>());
			var formatterMonitor = new OptionsMonitor<ConsoleFormatterOptions>(formatterFactory, Enumerable.Empty<IOptionsChangeTokenSource<ConsoleFormatterOptions>>(), new OptionsCache<ConsoleFormatterOptions>());
			*/

				var l = new ConsoleLoggerProvider(optionsMonitor);
				this.Configuration.LoggerFactory = new LoggerFactory();
				this.Configuration.LoggerFactory.AddProvider(l);
				break;
			}
		}

		var ass = typeof(DiscordClient).GetTypeInfo().Assembly;
		var vrs = "";
		var ivr = ass.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
		if (ivr != null)
			vrs = ivr.InformationalVersion;
		else
		{
			var v = ass.GetName().Version;
			vrs = v?.ToString(3);
		}

		this.Logger ??= this.Configuration.LoggerFactory!.CreateLogger<BaseDiscordClient>();

		this.ApiClient = new(this);
		this.UserCache = new();
		this.InternalVoiceRegions = new();
		this.VoiceRegionsLazy = new(() => new ReadOnlyDictionary<string, DiscordVoiceRegion>(this.InternalVoiceRegions));

		var httphandler = new HttpClientHandler
		{
			UseCookies = false,
			AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
			UseProxy = this.Configuration.Proxy != null,
			Proxy = this.Configuration.Proxy
		};
		this.RestClient = new()
		{
			Timeout = this.Configuration.HttpTimeout
		};
		this.RestClient.DefaultRequestHeaders.TryAddWithoutValidation(CommonHeaders.USER_AGENT, Utilities.GetUserAgent());
		this.RestClient.DefaultRequestHeaders.TryAddWithoutValidation(CommonHeaders.DISCORD_LOCALE, this.Configuration.Locale);
		if (!string.IsNullOrWhiteSpace(this.Configuration.Timezone))
			this.RestClient.DefaultRequestHeaders.TryAddWithoutValidation(CommonHeaders.DISCORD_TIMEZONE, this.Configuration.Timezone);
		if (this.Configuration.Override is not null)
			this.RestClient.DefaultRequestHeaders.TryAddWithoutValidation(CommonHeaders.SUPER_PROPERTIES, this.Configuration.Override);

		var a = typeof(DiscordClient).GetTypeInfo().Assembly;

		var iv = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
		if (iv != null)
			this.VersionString = iv.InformationalVersion;
		else
		{
			var v = a.GetName().Version;
			var vs = v.ToString(3);

			if (v.Revision > 0)
				this.VersionString = $"{vs}, CI build {v.Revision}";
		}
	}

	/// <summary>
	///     Gets the api client.
	/// </summary>
	protected internal DiscordApiClient ApiClient { get; }

	/// <summary>
	///     Gets the sentry dsn.
	/// </summary>
	internal static string SentryDsn { get; set; } = "https://1da216e26a2741b99e8ccfccea1b7ac8@o1113828.ingest.sentry.io/4504901362515968";

	/// <summary>
	///     Gets the configuration.
	/// </summary>
	protected internal DiscordConfiguration Configuration { get; }

	/// <summary>
	///     Gets the instance of the logger for this client.
	/// </summary>
	public ILogger<BaseDiscordClient> Logger { get; internal set; }

	/// <summary>
	///     Gets the string representing the version of bot lib.
	/// </summary>
	public string VersionString { get; }

	/// <summary>
	///     Gets the bot library name.
	/// </summary>
	public string BotLibrary
		=> "DisCatSharp";

	/// <summary>
	///     Gets the current user.
	/// </summary>
	public DiscordUser CurrentUser { get; internal set; }

	/// <summary>
	///     Gets the current application.
	/// </summary>
	public DiscordApplication CurrentApplication { get; internal set; }

	/// <summary>
	///     Exposes a <see cref="HttpClient">Http Client</see> for custom operations.
	/// </summary>
	public HttpClient RestClient { get; internal set; }

	/// <summary>
	///     Gets the cached guilds for this client.
	/// </summary>
	public abstract IReadOnlyDictionary<ulong, DiscordGuild> Guilds { get; }

	/// <summary>
	///     Gets the guilds ids for this shard.
	/// </summary>
	internal List<ulong> ReadyGuildIds { get; } = [];

	/// <summary>
	///     Gets the cached users for this client.
	/// </summary>
	public ConcurrentDictionary<ulong, DiscordUser> UserCache { get; internal set; }

	/// <summary>
	///     <para>Gets the service provider.</para>
	///     <para>This allows passing data around without resorting to static members.</para>
	///     <para>Defaults to null.</para>
	/// </summary>
	internal IServiceProvider ServiceProvider { get; set; }

	/// <summary>
	///     Gets the list of available voice regions. Note that this property will not contain VIP voice regions.
	/// </summary>
	public IReadOnlyDictionary<string, DiscordVoiceRegion> VoiceRegions
		=> this.VoiceRegionsLazy.Value;

	/// <summary>
	///     Gets the list of available voice regions. This property is meant as a way to modify <see cref="VoiceRegions" />.
	/// </summary>
	protected internal ConcurrentDictionary<string, DiscordVoiceRegion> InternalVoiceRegions { get; set; }

	/// <summary>
	///     Gets the cached application emojis for this client.
	/// </summary>
	public abstract IReadOnlyDictionary<ulong, DiscordApplicationEmoji> Emojis { get; }

	/// <summary>
	///     Disposes this client.
	/// </summary>
	public abstract void Dispose();

	/// <summary>
	///     Gets the current API application.
	/// </summary>
	public async Task<DiscordApplication> GetCurrentApplicationAsync()
	{
		var tapp = await this.ApiClient.GetCurrentApplicationOauth2InfoAsync().ConfigureAwait(false);
		return new(tapp);
	}

	/// <summary>
	///     Updates the current API application.
	/// </summary>
	/// <param name="description">The new description.</param>
	/// <param name="interactionsEndpointUrl">The new interactions endpoint url.</param>
	/// <param name="roleConnectionsVerificationUrl">The new role connections verification url.</param>
	/// <param name="customInstallUrl">The new custom install url.</param>
	/// <param name="tags">The new tags.</param>
	/// <param name="icon">The new application icon.</param>
	/// <param name="coverImage">The new application cover image.</param>
	/// <param name="flags">The new application flags. Can be only limited gateway intents.</param>
	/// <param name="installParams">The new install params.</param>
	/// <returns>The updated application.</returns>
	[DiscordDeprecated("Install params is gonna be replaced by integration types config")]
	public async Task<DiscordApplication> UpdateCurrentApplicationInfoAsync(
		Optional<string?> description,
		Optional<string?> interactionsEndpointUrl,
		Optional<string?> roleConnectionsVerificationUrl,
		Optional<string?> customInstallUrl,
		Optional<List<string>?> tags,
		Optional<Stream?> icon,
		Optional<Stream?> coverImage,
		Optional<ApplicationFlags> flags,
		[DiscordDeprecated("Replaced by Optional<DiscordIntegrationTypesConfig?>")]
		Optional<DiscordApplicationInstallParams?> installParams
	)
	{
		var iconb64 = MediaTool.Base64FromStream(icon);
		var coverImageb64 = MediaTool.Base64FromStream(coverImage);
		if (tags is { HasValue: true, Value: not null })
			if (tags.Value.Any(x => x.Length > 20))
				throw new InvalidOperationException("Tags can not exceed 20 chars.");

		DiscordApplication app = new(await this.ApiClient.ModifyCurrentApplicationInfoAsync(description, interactionsEndpointUrl, roleConnectionsVerificationUrl, customInstallUrl, tags, iconb64, coverImageb64, flags, installParams, Optional.None).ConfigureAwait(false));
		this.CurrentApplication = app;
		return app;
	}

	/// <summary>
	///     Enables user app functionality.
	/// </summary>
	/// <returns>The updated application.</returns>
	public async Task<DiscordApplication> EnableUserAppsAsync()
	{
		var currentApplication = await this.GetCurrentApplicationAsync().ConfigureAwait(false);
		var installParams = currentApplication.InstallParams;

		DiscordIntegrationTypesConfig integrationTypesConfig = new()
		{
			UserInstall = new(),
			GuildInstall = new()
			{
				OAuth2InstallParams = installParams is { Scopes: not null, Permissions: not null }
					? new()
					{
						Scopes = installParams?.Scopes,
						Permissions = installParams?.Permissions
					}
					: null
			}
		};

		var app = await this.UpdateCurrentApplicationInfoAsync(Optional.None, Optional.None, Optional.None, Optional.None, Optional.None, Optional.None, Optional.None, Optional.None, integrationTypesConfig).ConfigureAwait(false);
		return app;
	}

	/// <summary>
	///     Updates the current API application.
	/// </summary>
	/// <param name="description">The new description.</param>
	/// <param name="interactionsEndpointUrl">The new interactions endpoint url.</param>
	/// <param name="roleConnectionsVerificationUrl">The new role connections verification url.</param>
	/// <param name="customInstallUrl">The new custom install url.</param>
	/// <param name="tags">The new tags.</param>
	/// <param name="icon">The new application icon.</param>
	/// <param name="coverImage">The new application cover image.</param>
	/// <param name="flags">The new application flags. Can be only limited gateway intents.</param>
	/// <param name="integrationTypesConfig">The new integration types configuration.</param>
	/// <returns>The updated application.</returns>
	public async Task<DiscordApplication> UpdateCurrentApplicationInfoAsync(
		Optional<string?> description,
		Optional<string?> interactionsEndpointUrl,
		Optional<string?> roleConnectionsVerificationUrl,
		Optional<string?> customInstallUrl,
		Optional<List<string>?> tags,
		Optional<Stream?> icon,
		Optional<Stream?> coverImage,
		Optional<ApplicationFlags> flags,
		Optional<DiscordIntegrationTypesConfig?> integrationTypesConfig
	)
	{
		var iconb64 = MediaTool.Base64FromStream(icon);
		var coverImageb64 = MediaTool.Base64FromStream(coverImage);
		if (tags is { HasValue: true, Value: not null })
			if (tags.Value.Any(x => x.Length > 20))
				throw new InvalidOperationException("Tags can not exceed 20 chars.");

		DiscordApplication app = new(await this.ApiClient.ModifyCurrentApplicationInfoAsync(description, interactionsEndpointUrl, roleConnectionsVerificationUrl, customInstallUrl, tags, iconb64, coverImageb64, flags, Optional.None, integrationTypesConfig).ConfigureAwait(false));
		this.CurrentApplication = app;
		return app;
	}

	/// <summary>
	///     Gets a list of voice regions.
	/// </summary>
	/// <exception cref="ServerErrorException">Thrown when Discord is unable to process the request.</exception>
	public Task<IReadOnlyList<DiscordVoiceRegion>> ListVoiceRegionsAsync()
		=> this.ApiClient.ListVoiceRegionsAsync();

	/// <summary>
	///     Initializes this client. This method fetches information about current user, application, and voice regions.
	/// </summary>
	public virtual async Task InitializeAsync()
	{
		if (this.CurrentUser is null)
		{
			this.CurrentUser = await this.ApiClient.GetCurrentUserAsync().ConfigureAwait(false);
			this.UserCache.AddOrUpdate(this.CurrentUser.Id, this.CurrentUser, (id, xu) => this.CurrentUser);
		}

		if (this.Configuration.TokenType is TokenType.Bot && this.CurrentApplication is null)
			this.CurrentApplication = await this.GetCurrentApplicationAsync().ConfigureAwait(false);

		if (this.Configuration.TokenType is not TokenType.Bearer && this.InternalVoiceRegions.IsEmpty)
		{
			var vrs = await this.ListVoiceRegionsAsync().ConfigureAwait(false);
			foreach (var xvr in vrs)
				this.InternalVoiceRegions.TryAdd(xvr.Id, xvr);
		}
	}

	/// <summary>
	///     Gets the current gateway info for the provided token.
	///     <para>If no value is provided, the configuration value will be used instead.</para>
	/// </summary>
	/// <returns>A gateway info object.</returns>
	public async Task<GatewayInfo> GetGatewayInfoAsync(string? token = null)
	{
		if (this.Configuration.TokenType is not TokenType.Bot)
			throw new InvalidOperationException("Only bot tokens can access this info.");

		if (!string.IsNullOrEmpty(this.Configuration.Token))
			return await this.ApiClient.GetGatewayInfoAsync().ConfigureAwait(false);

		if (string.IsNullOrEmpty(token))
			throw new InvalidOperationException("Could not locate a valid token.");

		this.Configuration.Token = token;

		var res = await this.ApiClient.GetGatewayInfoAsync().ConfigureAwait(false);
		this.Configuration.Token = null;
		return res;
	}

	/// <summary>
	///     Gets a cached user.
	/// </summary>
	/// <param name="userId">The user id.</param>
	internal DiscordUser GetCachedOrEmptyUserInternal(ulong userId)
	{
		if (!this.TryGetCachedUserInternal(userId, out var user))
			user = new()
			{
				Id = userId,
				Discord = this
			};

		return user;
	}

	/// <summary>
	///     Tries the get a cached user.
	/// </summary>
	/// <param name="userId">The user id.</param>
	/// <param name="user">The user.</param>
	internal bool TryGetCachedUserInternal(ulong userId, [MaybeNullWhen(false)] out DiscordUser user)
		=> this.UserCache.TryGetValue(userId, out user);

	/// <summary>
	///     Updates the cached application emojis.
	/// </summary>
	/// <param name="rawEmojis">The raw emojis.</param>
	internal abstract IReadOnlyDictionary<ulong, DiscordApplicationEmoji> UpdateCachedApplicationEmojis(JArray? rawEmojis);

	/// <summary>
	///     Updates a cached application emoji.
	/// </summary>
	/// <param name="emoji">The emoji.</param>
	internal abstract DiscordApplicationEmoji UpdateCachedApplicationEmoji(DiscordApplicationEmoji emoji);
}
