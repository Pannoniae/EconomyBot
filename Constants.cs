using SpotifyAPI.Web;

namespace EconomyBot;

public static class Constants {
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
        token = File.ReadAllText("token");
        var apikeystuff = File.ReadAllText("googletoken");
        (apikey, apikey_huggingface) = apikeystuff.Split('\n') switch { var a => (a[0], a[1]) };
        var spotifytokenstuff = File.ReadAllText("spotifytoken");
        (spotifytoken, spotifytoken2) =
            spotifytokenstuff.Split('\n') switch { var a => (a[0], a[1]) };
        spotifytoken = spotifytoken.Trim();
        spotifytoken2 = spotifytoken2.Trim();
        var redditStuff = File.ReadAllText("reddittoken");
        // totally not a gross hack to do some deconstructing
        (redditappid, redditrefreshtoken, redditappsecret) =
            redditStuff.Split('\n') switch { var a => (a[0], a[1], a[2]) };
        redditappid = redditappid.Trim();
        redditrefreshtoken = redditrefreshtoken.Trim();
        redditappsecret = redditappsecret.Trim();
    }

    public static IToken getSpotifyToken() {
        throw new NotImplementedException();
    }
}