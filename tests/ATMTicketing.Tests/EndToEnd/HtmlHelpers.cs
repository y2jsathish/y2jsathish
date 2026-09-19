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

    /// <summary>Pulls the token out of the layout's &lt;meta name="request-verification-token"&gt;
    /// tag — the one site.js reads and attaches as the X-CSRF-TOKEN header on every AJAX POST
    /// that doesn't have a Razor &lt;form&gt; to carry a hidden field. Tests hitting those
    /// endpoints (Delete/ToggleActive/etc.) need to do the same to pass antiforgery validation.</summary>
    public static string ExtractMetaAntiForgeryToken(string html)
    {
        var match = MetaAntiForgeryRegex().Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException("Could not find the request-verification-token meta tag in the response HTML.");
        }
        return match.Groups[1].Value;
    }

    /// <summary>ASP.NET Core's RedirectResult sometimes writes an absolute Location header
    /// and sometimes a relative one depending on the action; normalize to a path+query string
    /// so tests can assert on it consistently either way.</summary>
    public static string PathOf(Uri location) => location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;

    /// <summary>Mirrors what site.js does for every AJAX POST in the real app: attach the
    /// antiforgery token from the page's meta tag as the X-CSRF-TOKEN header, since these
    /// calls build their own request body rather than serializing a Razor &lt;form&gt;'s
    /// hidden field. <paramref name="pageHtmlWithToken"/> is any already-fetched page's HTML
    /// from the same authenticated session (every page under _Layout carries the meta tag).</summary>
    public static async Task<HttpResponseMessage> PostWithCsrfAsync(
        HttpClient client, string url, string pageHtmlWithToken, Dictionary<string, string> formData)
    {
        var token = ExtractMetaAntiForgeryToken(pageHtmlWithToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(formData)
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        // jQuery sets this on every $.post/$.ajax call by default; Program.cs's cookie
        // Events.OnRedirectToAccessDenied/OnRedirectToLogin key off it to return a clean
        // 401/403 instead of a redirect for exactly these requests — so tests need to send it
        // too, to exercise the same code path a real browser click does.
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return await client.SendAsync(request);
    }

    [GeneratedRegex("""<input\b[^>]*__RequestVerificationToken[^>]*>""")]
    private static partial Regex AntiForgeryInputTagRegex();

    [GeneratedRegex("value=\"([^\"]*)\"")]
    private static partial Regex ValueAttributeRegex();

    [GeneratedRegex("<meta\\s+name=\"request-verification-token\"\\s+content=\"([^\"]*)\"")]
    private static partial Regex MetaAntiForgeryRegex();
}
