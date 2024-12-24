using System.Collections.Generic;
using NetCord.Rest;

namespace DisCatSharp.Interactivity;

public static class InteractivityHelpers {
    public static List<Page> Recalculate(this List<Page> pages) {
        List<Page> recalulatedPages = new(pages.Count);
        var pageCount = 1;
        foreach (var page in pages) {
            var tempPage = new Page();
            var replaceEmbed = page.Embed.WithFooter(new EmbedFooterProperties {
                Text = $"Page {pageCount}/{pages.Count}"
            });
            tempPage.Embed = replaceEmbed;
            recalulatedPages.Add(tempPage);
            pageCount++;
        }

        return recalulatedPages;
    }
}