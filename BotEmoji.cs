using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace EconomyBot;

public class BotEmoji {

    public static string FromName(GatewayClient client, string emoji) {
        if (client == null)
            throw new ArgumentNullException(nameof(client), "Client cannot be null.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name), "Name cannot be empty or null.");

        if (s_unicodeEmojis.TryGetValue(name, out var unicodeEntity))
            return new()
            {
                Discord = client,
                Name = unicodeEntity
            };

        if (includeGuilds)
        {
            var allEmojis = client.Guilds.Values
                .SelectMany(xg => xg.Emojis.Values); // save cycles - don't order

            var ek = name.AsSpan().Slice(1, name.Length - 2);
            foreach (var emoji in allEmojis)
                if (emoji.Name.AsSpan().SequenceEqual(ek))
                    return emoji;
        }

        if (includeApplication)
        {
            var ek = name.AsSpan().Slice(1, name.Length - 2);
            foreach (var emoji in client.Emojis.Values)
                if (emoji.Name.AsSpan().SequenceEqual(ek))
                    return emoji;
        }

        throw new ArgumentException("Invalid emoji name specified.", nameof(name));
    }
}