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
    public static string? redditappid;
    public static string? redditrefreshtoken;
    public static string? redditappsecret;
    public static string? apikey;
    public static string? apikey_huggingface;
    public static string? apikey_cloudflareAI;
    public static string? apikey_cloudflareAIaccount;
    public static string? spotifytoken;
    public static string? spotifytoken2;
    public static string? detectlanguagetoken;

    public static void init() {

        JObject json;
        try {
            json = JObject.Parse(File.ReadAllText("config.json"));
        }
        catch (FileNotFoundException e) {
            logger.error("The bot doesn't work without a config.json file, please create one.");
            return;
        }

        token = json["token"]?.Value<string>();
        if (token is null) {
            logger.warn("Discord API token not found.");
        }
        (apikey, apikey_huggingface) = (json["google"]?.Value<string>(), json["huggingface"]?.Value<string>());
        if (apikey is null || apikey_huggingface is null) {
            logger.warn("Google/Perspective tokens not found.");
        }
        (apikey_cloudflareAI, apikey_cloudflareAIaccount) = (json["cloudflareAI"]?.Value<string>(), json["cloudflareAIaccount"]?.Value<string>());
        if (apikey_cloudflareAI is null || apikey_cloudflareAIaccount is null) {
            logger.warn("CloudFlare AI token not found.");
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

        detectlanguagetoken = json["detecttoken"].Value<string>();
        if (detectlanguagetoken is null) {
            logger.warn("Language detection API token not found.");
        }
        logger.info("Constants setup!");
    }

    public static IToken getSpotifyToken() {
        throw new NotImplementedException();
    }
}