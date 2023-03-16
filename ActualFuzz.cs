using FuzzierSharp;
using FuzzierSharp.PreProcess;

namespace EconomyBot;

/// <summary>
/// Fuzzy string matching wrapper, we are using this for two reasons.
/// 1. There is a chance of me having to replace the library used because it looks like a buggy mess.
/// 2. The documentation is awful. There is no structured documentation, parameters aren't even properly named.
/// Sidenote: fuck the library's author for not exposing the actual 0-to-1 double values for the algorithms.
/// What you get is an integer percentage between 0 and 100 which incredibly loses information. Stupid design.
/// </summary>
public class ActualFuzz {
    
    /// <summary>
    /// Returns fuzzy matching for <paramref name="needle"/> in <paramref name="haystack"/>.
    /// </summary>
    /// <param name="needle">The string to be searched for in <paramref name="haystack"/></param>
    /// <param name="haystack">The string to be searched in.</param>
    /// <returns>An integer value between 0 and 100. 0 indicates no similarity, 100 is an exact substring match.</returns>
    public static int partialFuzz(string needle, string haystack) {
        // please, don't garble non-English text, thanks 
        return Fuzz.PartialRatio(needle, haystack, StandardPreprocessors.CaseInsensitive);
    }
    
    /// <summary>
    /// Returns the maximum similarity between any of the <paramref name="needle"/> values and <paramref name="haystack"/>.
    /// </summary>
    /// <param name="needle">The list of strings to be searched for in <paramref name="haystack"/></param>
    /// <param name="haystack">The string to be searched in.</param>
    /// <returns>An integer value between 0 and 100. 0 indicates no similarity, 100 is an exact substring match.</returns>
    public static int partialFuzz(IEnumerable<string> needle, string haystack) {
        // please, don't garble non-English text, thanks
        return needle.Max(n => Fuzz.PartialRatio(n, haystack, StandardPreprocessors.CaseInsensitive));
    }
}