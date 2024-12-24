using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using DisCatSharp.Entities;
using DisCatSharp.Exceptions;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace DisCatSharp.Net.Serialization;

/// <summary>
///     Represents discord json.
/// </summary>
public static class DiscordJson
{
	/// <summary>
	///     Gets the serializer.
	/// </summary>
	private static readonly JsonSerializer s_serializer = JsonSerializer.CreateDefault(new()
	{
		ContractResolver = s_contractResolver,
	});

	internal static readonly OptionalJsonContractResolver s_contractResolver = new();

	/// <summary>Serializes the specified object to a JSON string.</summary>
	/// <param name="value">The object to serialize.</param>
	/// <returns>A JSON string representation of the object.</returns>
	public static string SerializeObject(object value)
		=> SerializeObjectInternal(value, null!, s_serializer);

	/// <summary>
	///     Deserializes the specified JSON string to an object.
	/// </summary>
	/// <typeparam name="T">The type</typeparam>
	/// <param name="json">The received json.</param>
	/// <param name="discord">The discord client.</param>
	public static T DeserializeObject<T>(string? json, BaseDiscordClient? discord) where T : ObservableApiObject
		=> DeserializeObjectInternal<T>(json, discord);

	/// <summary>
	///     Deserializes the specified JSON string to an object of the type <see cref="IEnumerable{T}" />.
	/// </summary>
	/// <typeparam name="T">The enumerable type.</typeparam>
	/// <param name="json">The received json.</param>
	/// <param name="discord">The discord client.</param>
	public static T DeserializeIEnumerableObject<T>(string? json, BaseDiscordClient? discord) where T : IEnumerable<ObservableApiObject>
		=> DeserializeIEnumerableObjectInternal<T>(json, discord);

	/// <summary>Populates an object with the values from a JSON node.</summary>
	/// <param name="value">The token to populate the object with.</param>
	/// <param name="target">The object to populate.</param>
	public static void PopulateObject(JToken value, object target)
	{
		using var reader = value.CreateReader();
		s_serializer.Populate(reader, target);
	}

	/// <summary>
	///     Converts this token into an object, passing any properties through extra
	///     <see cref="Newtonsoft.Json.JsonConverter" />s if needed.
	/// </summary>
	/// <param name="token">The token to convert</param>
	/// <typeparam name="T">Type to convert to</typeparam>
	/// <returns>The converted token</returns>
	public static T ToDiscordObject<T>(this JToken token)
		=> token.ToObject<T>(s_serializer)!;

	/// <summary>
	///     Serializes the object.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="type">The type.</param>
	/// <param name="jsonSerializer">The json serializer.</param>
	private static string SerializeObjectInternal(object value, Type type, JsonSerializer jsonSerializer)
	{
		var stringWriter = new StringWriter(new(), CultureInfo.InvariantCulture);
		using (var jsonTextWriter = new JsonTextWriter(stringWriter))
		{
			jsonTextWriter.Formatting = jsonSerializer.Formatting;
			jsonSerializer.Serialize(jsonTextWriter, value, type);
		}

		return stringWriter.ToString();
	}

	/// <summary>
	///     Handles <see cref="DiscordJson" /> errors.
	/// </summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The error event args.</param>
	/// <param name="discord">The discord client.</param>
	private static void DiscordJsonErrorHandler(object? sender, ErrorEventArgs e, BaseDiscordClient? discord)
	{
	}

	/// <summary>
	///     Deserializes the specified JSON string to an object.
	/// </summary>
	/// <typeparam name="T">The type</typeparam>
	/// <param name="json">The received json.</param>
	/// <param name="discord">The discord client.</param>
	private static T DeserializeObjectInternal<T>(string? json, BaseDiscordClient? discord) where T : ObservableApiObject
	{
		ArgumentNullException.ThrowIfNull(json, nameof(json));

		var obj = JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings
		{
			ContractResolver = s_contractResolver,
			Error = (s, e) => DiscordJsonErrorHandler(s, e, discord)
		})!;

		if (discord is null)
			return obj;

		obj.Discord = discord;

		if (!discord.Configuration.ReportMissingFields || !obj.AdditionalProperties.Any())
			return obj;

		return obj;
	}

	/// <summary>
	///     Deserializes the specified JSON string to an object of the type <see cref="IEnumerable{T}" />.
	/// </summary>
	/// <typeparam name="T">The enumerable type.</typeparam>
	/// <param name="json">The received json.</param>
	/// <param name="discord">The discord client.</param>
	private static T DeserializeIEnumerableObjectInternal<T>(string? json, BaseDiscordClient? discord) where T : IEnumerable<ObservableApiObject>
	{
		ArgumentNullException.ThrowIfNull(json, nameof(json));

		var obj = JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings
		{
			ContractResolver = s_contractResolver,
			Error = (s, e) => DiscordJsonErrorHandler(s, e, discord)
		})!;

		if (discord is null)
			return obj;

		foreach (var ob in obj)
			ob.Discord = discord;

		if (!discord.Configuration.ReportMissingFields || !obj.Any(x => x.AdditionalProperties.Any()))
			return obj;
		return obj;
	}
}
