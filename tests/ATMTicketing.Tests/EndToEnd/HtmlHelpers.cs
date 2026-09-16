using System.Text.RegularExpressions;

namespace ATMTicketing.Tests.EndToEnd;

internal static partial class HtmlHelpers
{
    /// <summary>Pulls the antiforgery hidden-input value out of a rendered Razor form so
    /// integration tests can round-trip it on the following POST, exactly as a browser would.</summary>
    public static string ExtractAntiForgeryToken(string html)
    {
        // Two steps so attribute order inside the <input> tag doesn't matter: first find
        // the whole tag by its distinctive field name, then pull value="..." out of it.
        var tagMatch = AntiForgeryInputTagRegex().Match(html);
        if (!tagMatch.Success)
        {
            throw new InvalidOperationException("Could not find the __RequestVerificationToken input in the response HTML.");
        }

        var valueMatch = ValueAttributeRegex().Match(tagMatch.Value);
        if (!valueMatch.Success)
        {
            throw new InvalidOperationException("Found the __RequestVerificationToken input but it had no value attribute.");
        }

        return valueMatch.Groups[1].Value;
    }

    /// <summary>ASP.NET Core's RedirectResult sometimes writes an absolute Location header
    /// and sometimes a relative one depending on the action; normalize to a path+query string
    /// so tests can assert on it consistently either way.</summary>
    public static string PathOf(Uri location) => location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;

    [GeneratedRegex("""<input\b[^>]*__RequestVerificationToken[^>]*>""")]
    private static partial Regex AntiForgeryInputTagRegex();

    [GeneratedRegex("value=\"([^\"]*)\"")]
    private static partial Regex ValueAttributeRegex();
}
