﻿using Reddit;
using Reddit.Controllers;
using Reddit.Exceptions;

namespace EconomyBot;

public class RedditImageProvider : IImageProvider {
    private RedditClient reddit;

    public RedditImageProvider() {
        reddit = new RedditClient(Constants.redditappid, Constants.redditrefreshtoken,
            Constants.redditappsecret);
    }

    public async Task<string> getRandomImage() {
        // 70% chance of sfw, 30% chance of nsfw
        Subreddit sub;
        sub = reddit.Subreddit(new Random().NextDouble() < 0.7 ? "wholesomeyuri" : "actualyuri");


        return await getImgFromSub(sub);
    }

    public async Task<string> getImageFromSub(string sub) {
        var subreddit = reddit.Subreddit(sub);


        return await getImgFromSub(subreddit);
    }

    private async Task<string> getImgFromSub(Subreddit sub) {
        try {
            while (true) {
                var posts = sub.Posts.Hot;
                var index = new Random().Next(posts.Count);
                var post = posts[index];
                var imgjson = post.Listing.Preview;
                if (imgjson == null) {
                    continue;
                }

                var url = imgjson["images"]?[0]?["source"]?.Value<string?>("url");
                if (url != null) {
                    // send the image
                    return url;
                }

                if (post.Listing.Preview != null && url != null) {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is RedditGatewayTimeoutException or RedditServiceUnavailableException) {
            throw; // just throw anyway
        }

        throw new NullReferenceException("You've managed to return a null image even though it shouldn't be possible.");
    }
}