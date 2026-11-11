using System.Collections;
using EconomyBot.Logging;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using SpotifyAPI.Web;

namespace EconomyBot;

public static class Constants {
    
    private static readonly Logger logger = Logger.getClassLogger("Constants");
    
    public static ulong szerepjatek = 886722112310632499;
    public static ulong server = 828296966324224020;

    public static string? token;

    /// Whether to ask discord for the privileged intents (server members, message content). On by default; set
    /// "privilegedIntents": false in the config to run without them while the bot isn't approved for them - asking
    /// for an intent we don't have makes discord refuse the connection outright.
    /// Without them: no text commands (slash ones still work), no join log/role, no deletion log, no transcripts.
    public static bool privilegedIntents = true;

    /// BOT_PROFILE=dev makes the bot read config.dev.json and guilds.dev.json, so a test bot can run from the same
    /// folder without touching the real bot's files.
    public static readonly string? profile = Environment.GetEnvironmentVariable("BOT_PROFILE") is { Length: > 0 } p
        ? p
        : null;

    /// "config.json" normally, "config.dev.json" under BOT_PROFILE=dev.
    public static string profileFile(string name) =>
        profile == null ? $"{name}.json" : $"{name}.{profile}.json";
    public static string? redditappid;
    public static string? redditrefreshtoken;
    public static string? redditappsecret;
    public static string? spotifytoken;
    public static string? spotifytoken2;

    public static void init() {

        JObject json;
        try {
            json = JObject.Parse(File.ReadAllText(profileFile("config")));
        }
        catch (FileNotFoundException) {
            logger.error($"The bot doesn't work without a {profileFile("config")} file, please create one.");
            return;
        }

        privilegedIntents = json["privilegedIntents"]?.Value<bool>() ?? true;

        token = json["token"]?.Value<string>();
        if (token is null) {
            logger.warn("Discord API token not found.");
        }
        (spotifytoken, spotifytoken2) = (json["spotify1"]?.Value<string>(), json["spotify2"]?.Value<string>());
        if (spotifytoken is null || spotifytoken2 is null) {
            logger.warn("Spotify token not found.");
        }
        // totally not a gross hack to do some deconstructing
        (redditappid, redditrefreshtoken, redditappsecret) =
            (json["reddit1"]?.Value<string>(), json["reddit2"]?.Value<string>(), json["reddit3"]?.Value<string>());
        if (redditappid is null || redditrefreshtoken is null || redditappsecret is null) {
            logger.warn("Reddit tokens not found.");
        }

        logger.info("Constants setup!");
    }

    public static IToken getSpotifyToken() {
        throw new NotImplementedException();
    }
}