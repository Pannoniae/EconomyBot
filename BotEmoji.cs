using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace EconomyBot;

public class BotEmoji {

    public static string FromName(GatewayClient client, string name, bool includeGuilds = true, bool includeApplication = true) {
        if (client == null)
            throw new ArgumentNullException(nameof(client), "Client cannot be null.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name), "Name cannot be empty or null.");


        if (DiscordEmoji.s_unicodeEmojis.TryGetValue(name, out string? unicodeEntity))
            return unicodeEntity;

        if (includeGuilds) {
            var allEmojis = client.Cache.Guilds.Values
                .SelectMany(xg => xg.Emojis.Values); // save cycles - don't order

            var ek = name.AsSpan().Slice(1, name.Length - 2);
            foreach (var emoji in allEmojis)
                if (emoji.Name.AsSpan().SequenceEqual(ek))
                    return emoji.ToString();
        }

        if (includeApplication) {
            var ek = name.AsSpan().Slice(1, name.Length - 2);
            foreach (var emoji in client.Rest.GetApplicationEmojisAsync(client.Id).GetAwaiter().GetResult())
                if (emoji.Name.AsSpan().SequenceEqual(ek))
                    return emoji.ToString();
        }

        throw new ArgumentException("Invalid emoji name specified.", nameof(name));
    }
}