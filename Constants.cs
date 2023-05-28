using Newtonsoft.Json.Linq;
using NLog;
using SpotifyAPI.Web;

namespace EconomyBot;

public static class Constants {
    
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    
    public static ulong szerepjatek = 886722112310632499;
    public static ulong server = 828296966324224020;

    public static string? token;
    public static string? redditappid;
    public static string? redditrefreshtoken;
    public static string? redditappsecret;
    public static string? apikey;
    public static string? apikey_huggingface;
    public static string? spotifytoken;
    public static string? spotifytoken2;

    public static void init() {
        var json = JObject.Parse(File.ReadAllText("config.json"));

        token = json["token"]?.Value<string>();
        if (token is null) {
            logger.Warn("Discord API token not found.");
        }
        (apikey, apikey_huggingface) = (json["google"]?.Value<string>(), json["huggingface"]?.Value<string>());
        if (apikey is null || apikey_huggingface is null) {
            logger.Warn("Google/Perspective tokens not found.");
        }
        (spotifytoken, spotifytoken2) = (json["spotify1"]?.Value<string>(), json["spotify2"]?.Value<string>());
        if (spotifytoken is null || spotifytoken2 is null) {
            logger.Warn("Spotify token not found.");
        }
        // totally not a gross hack to do some deconstructing
        (redditappid, redditrefreshtoken, redditappsecret) =
            (json["reddit1"]?.Value<string>(), json["reddit2"]?.Value<string>(), json["reddit3"]?.Value<string>());
        if (redditappid is null || redditrefreshtoken is null || redditappsecret is null) {
            logger.Warn("Reddit tokens not found.");
        }
        logger.Info("Constants setup!");
    }

    public static IToken getSpotifyToken() {
        throw new NotImplementedException();
    }
}