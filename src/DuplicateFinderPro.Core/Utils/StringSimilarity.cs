using System.Text;
using System.Text.RegularExpressions;

namespace DuplicateFinderPro.Core.Utils;

/// <summary>
/// Fuzzy string helpers used by the name-similarity detector.
/// </summary>
public static partial class StringSimilarity
{
    // Tokens frequently added by copy operations / downloaders that shouldn't
    // affect whether two names describe the same underlying content.
    [GeneratedRegex(@"\b(copy|copie|kopie|kopya|duplicate|final|new|edit|edited)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NoiseWords();

    [GeneratedRegex(@"[\(\[\{]?\s*(copy|\d+)\s*[\)\]\}]?$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingCounter();

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex NonAlphanumeric();

    /// <summary>
    /// Produces a canonical form of a file name (without extension) so that
    /// "Movie (1).mkv", "Movie - Copy.mkv" and "movie.mkv" collapse together.
    /// </summary>
    public static string Normalize(string fileNameWithoutExtension)
    {
        var s = fileNameWithoutExtension.ToLowerInvariant().Trim();
        s = TrailingCounter().Replace(s, string.Empty);
        s = NoiseWords().Replace(s, string.Empty);
        s = NonAlphanumeric().Replace(s, " ");
        return s.Trim();
    }

    /// <summary>Classic Levenshtein edit distance.</summary>
    public static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    /// <summary>Similarity ratio in [0,1]; 1 = identical, 0 = completely different.</summary>
    public static double Ratio(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1.0;
        var maxLen = Math.Max(a.Length, b.Length);
        if (maxLen == 0) return 1.0;
        return 1.0 - (double)Levenshtein(a, b) / maxLen;
    }
}
