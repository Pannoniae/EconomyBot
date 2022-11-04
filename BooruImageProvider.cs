using BooruSharp.Booru;

namespace EconomyBot; 

public class BooruImageProvider : IImageProvider {
    public async Task<string> getRandomYuri() {
        var booru = new DanbooruDonmai();
        var post = await booru.GetRandomPostAsync("yuri");
        return post.FileUrl.ToString();
    }
    
    public async Task<string> getRandomYaoi() {
        var booru = new DanbooruDonmai();
        var post = await booru.GetRandomPostAsync("yaoi");
        return post.FileUrl.ToString();
    }

    public Task<string> getRandomImage() {
        return getRandomYuri();
    }
}