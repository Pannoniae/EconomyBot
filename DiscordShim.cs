using System.Runtime.CompilerServices;
using NetCord;
using NetCord.Gateway;
using NetCord.JsonModels;
using NetCord.Rest;

namespace EconomyBot;

public static class DiscordShim {

    public static GatewayClient client = Program.client;

    public static async Task<List<RestMessage>> getMessages(ulong eGuildId, ulong eChannelId, IReadOnlyList<ulong> eMessageIds) {
        var guild = client.Cache.Guilds[eGuildId];
        var channel = guild.Channels[eChannelId];
        var messages = new List<RestMessage>();
        foreach (var id in eMessageIds) {
            messages.Add(await client.Rest.GetMessageAsync(eChannelId, id));
        }
        return messages;
    }

    public static async Task<RestMessage> getMessage(ulong eGuildId, ulong eChannelId, ulong eMessageId) {
        return await client.Rest.GetMessageAsync(eChannelId, eMessageId);
    }

    public static IGuildChannel getChannel(ulong eGuildId, ulong eChannelId) {
        var guild = client.Cache.Guilds[eGuildId];
        return guild.Channels[eChannelId];
    }

    public static async Task sendMessage(IGuildChannel channel, string message) {
        await client.Rest.SendMessageAsync(channel.Id, message);
    }

    public static Task SendMessageAsync(this IGuildChannel channel, string message) {
        return sendMessage(channel, message);
    }

    public static async Task SendMessageAsync(this IGuildChannel channel, MessageProperties message) {
        await client.Rest.SendMessageAsync(channel.Id, message);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jsonModel")]
    public static extern JsonChannel jsonChannel(Channel channel);

    public static ChannelType getChnType(this IGuildChannel channel) {
        var jsonChannel = DiscordShim.jsonChannel((Channel)channel);
        return jsonChannel.Type;
    }

    public static ulong? getChnParent(this IGuildChannel channel) {
        var jsonChannel = DiscordShim.jsonChannel((Channel)channel);
        return jsonChannel.ParentId;
    }
}

public readonly partial struct DiscordColor
{
#region Black and White

	/// <summary>
	///     Represents no color, or integer 0;
	/// </summary>
	public static Color None { get; } = new(0);

	/// <summary>
	///     A near-black color. Due to API limitations, the color is #010101, rather than #000000, as the latter is treated as
	///     no color.
	/// </summary>
	public static Color Black { get; } = new(0x010101);

	/// <summary>
	///     White, or #FFFFFF.
	/// </summary>
	public static Color White { get; } = new(0xFFFFFF);

	/// <summary>
	///     Gray, or #808080.
	/// </summary>
	public static Color Gray { get; } = new(0x808080);

	/// <summary>
	///     Dark gray, or #A9A9A9.
	/// </summary>
	public static Color DarkGray { get; } = new(0xA9A9A9);

	/// <summary>
	///     Light gray, or #808080.
	/// </summary>
	public static Color LightGray { get; } = new(0xD3D3D3);

	/// <summary>
	///     Very dark gray, or #666666.
	/// </summary>
	public static Color VeryDarkGray { get; } = new(0x666666);

#endregion

#region Discord branding colors

	// See https://discord.com/branding.

	/// <summary>
	///     Discord Blurple, or #5865F2.
	/// </summary>
	public static Color Blurple { get; } = new(0x5865F2);

	/// <summary>
	///     Discord Fuchsia, or #EB459E.
	/// </summary>
	public static Color Fuchsia { get; } = new(0xEB459E);

	/// <summary>
	///     Discord Green, or #57F287.
	/// </summary>
	public static Color Green { get; } = new(0x57F287);

	/// <summary>
	///     Discord Yellow, or #FEE75C.
	/// </summary>
	public static Color Yellow { get; } = new(0xFEE75C);

	/// <summary>
	///     Discord Red, or #ED4245.
	/// </summary>
	public static Color Red { get; } = new(0xED4245);

#endregion

#region Other colors

	/// <summary>
	///     Dark red, or #7F0000.
	/// </summary>
	public static Color DarkRed { get; } = new(0x7F0000);

	/// <summary>
	///     Dark green, or #007F00.
	/// </summary>
	public static Color DarkGreen { get; } = new(0x007F00);

	/// <summary>
	///     Blue, or #0000FF.
	/// </summary>
	public static Color Blue { get; } = new(0x0000FF);

	/// <summary>
	///     Dark blue, or #00007F.
	/// </summary>
	public static Color DarkBlue { get; } = new(0x00007F);

	/// <summary>
	///     Cyan, or #00FFFF.
	/// </summary>
	public static Color Cyan { get; } = new(0x00FFFF);

	/// <summary>
	///     Magenta, or #FF00FF.
	/// </summary>
	public static Color Magenta { get; } = new(0xFF00FF);

	/// <summary>
	///     Teal, or #008080.
	/// </summary>
	public static Color Teal { get; } = new(0x008080);

	// meme
	/// <summary>
	///     Aquamarine, or #00FFBF.
	/// </summary>
	public static Color Aquamarine { get; } = new(0x00FFBF);

	/// <summary>
	///     Gold, or #FFD700.
	/// </summary>
	public static Color Gold { get; } = new(0xFFD700);

	/// <summary>
	///     Goldenrod, or #DAA520.
	/// </summary>
	public static Color Goldenrod { get; } = new(0xDAA520);

	/// <summary>
	///     Azure, or #007FFF.
	/// </summary>
	public static Color Azure { get; } = new(0x007FFF);

	/// <summary>
	///     Rose, or #FF007F.
	/// </summary>
	public static Color Rose { get; } = new(0xFF007F);

	/// <summary>
	///     Spring green, or #00FF7F.
	/// </summary>
	public static Color SpringGreen { get; } = new(0x00FF7F);

	/// <summary>
	///     Chartreuse, or #7FFF00.
	/// </summary>
	public static Color Chartreuse { get; } = new(0x7FFF00);

	/// <summary>
	///     Orange, or #FFA500.
	/// </summary>
	public static Color Orange { get; } = new(0xFFA500);

	/// <summary>
	///     Purple, or #800080.
	/// </summary>
	public static Color Purple { get; } = new(0x800080);

	/// <summary>
	///     Violet, or #EE82EE.
	/// </summary>
	public static Color Violet { get; } = new(0xEE82EE);

	/// <summary>
	///     Brown, or #A52A2A.
	/// </summary>
	public static Color Brown { get; } = new(0xA52A2A);

	/// <summary>
	///     Hot pink, or #FF69B4
	/// </summary>
	public static Color HotPink { get; } = new(0xFF69B4);

	/// <summary>
	///     Lilac, or #C8A2C8.
	/// </summary>
	public static Color Lilac { get; } = new(0xC8A2C8);

	/// <summary>
	///     Cornflower blue, or #6495ED.
	/// </summary>
	public static Color CornflowerBlue { get; } = new(0x6495ED);

	/// <summary>
	///     Midnight blue, or #191970.
	/// </summary>
	public static Color MidnightBlue { get; } = new(0x191970);

	/// <summary>
	///     Wheat, or #F5DEB3.
	/// </summary>
	public static Color Wheat { get; } = new(0xF5DEB3);

	/// <summary>
	///     Indian red, or #CD5C5C.
	/// </summary>
	public static Color IndianRed { get; } = new(0xCD5C5C);

	/// <summary>
	///     Turquoise, or #30D5C8.
	/// </summary>
	public static Color Turquoise { get; } = new(0x30D5C8);

	/// <summary>
	///     Sap green, or #507D2A.
	/// </summary>
	public static Color SapGreen { get; } = new(0x507D2A);

	// meme, specifically bob ross
	/// <summary>
	///     Phthalo blue, or #000F89.
	/// </summary>
	public static Color PhthaloBlue { get; } = new(0x000F89);

	// meme, specifically bob ross
	/// <summary>
	///     Phthalo green, or #123524.
	/// </summary>
	public static Color PhthaloGreen { get; } = new(0x123524);

	/// <summary>
	///     Sienna, or #882D17.
	/// </summary>
	public static Color Sienna { get; } = new(0x882D17);

#endregion
}