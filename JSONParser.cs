using System.Text;

namespace EconomyBot; 

public class JSONParser {
    private static readonly HttpClient httpClient = new();
    public static async Task<string> getJSON(string url, string json) {
        var response = await httpClient.PostAsync(
            url,
            new StringContent(json, Encoding.UTF8, "application/json"));
        return await response.Content.ReadAsStringAsync();
    }
}