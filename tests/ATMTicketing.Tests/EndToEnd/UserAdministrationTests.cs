using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ATMTicketing.Tests.EndToEnd;

/// <summary>Own isolated factory (not shared with AuthenticatedFlowTests) so the ticket
/// activity those tests create against the seeded engineer doesn't affect the delete-guard
/// assertions here.</summary>
public class UserAdministrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UserAdministrationTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<HttpClient> LoginAsAdminAsync(CustomWebApplicationFactory factory)
    {
        var client = factory.CreateFlowClient(allowAutoRedirect: false);
        var loginPage = await client.GetAsync("/Account/Login");
        var token = HtmlHelpers.ExtractAntiForgeryToken(await loginPage.Content.ReadAsStringAsync());

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "admin@atmticketing.local",
            ["Password"] = "Admin@12345",
            ["__RequestVerificationToken"] = token
        }));
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        return client;
    }

    [Fact]
    public async Task Admin_CannotDeleteOwnAccount()
    {
        using var client = await LoginAsAdminAsync(_factory);
        var indexHtml = await (await client.GetAsync("/User/Index")).Content.ReadAsStringAsync();
        var adminId = ExtractSeededAdminId(indexHtml);

        var deleteResponse = await PostWithCsrfAsync(client, "/User/Delete", indexHtml, new Dictionary<string, string>
        {
            ["id"] = adminId
        });

        var result = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("succeeded").GetBoolean().Should().BeFalse();
        result.GetProperty("errors")[0].GetString().Should().Contain("own account");
    }

    /// <summary>Mirrors what site.js does for every AJAX POST: attach the antiforgery token
    /// from the page's meta tag as the X-CSRF-TOKEN header, since these calls build their own
    /// request body rather than serializing a Razor &lt;form&gt;'s hidden field.</summary>
    private static async Task<HttpResponseMessage> PostWithCsrfAsync(
        HttpClient client, string url, string pageHtmlWithToken, Dictionary<string, string> formData)
    {
        var token = HtmlHelpers.ExtractMetaAntiForgeryToken(pageHtmlWithToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(formData)
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Admin_CanDeleteAFreshUnusedUser()
    {
        using var client = await LoginAsAdminAsync(_factory);

        // Create a throwaway user with no ticket history through the real Create form.
        var createForm = await client.GetAsync("/User/Create");
        var createToken = HtmlHelpers.ExtractAntiForgeryToken(await createForm.Content.ReadAsStringAsync());
        var email = $"throwaway-{Guid.NewGuid():N}@test.local";

        var createResponse = await client.PostAsync("/User/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Throwaway User",
            ["Email"] = email,
            ["Role"] = "CallCenterAgent",
            ["Password"] = "Passw0rd!23",
            ["__RequestVerificationToken"] = createToken
        }));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var indexPage = await client.GetAsync("/User/Index");
        var indexHtml = await indexPage.Content.ReadAsStringAsync();
        indexHtml.Should().Contain(email);
        var userId = ExtractUserIdByEmail(indexHtml, email);

        var deleteResponse = await PostWithCsrfAsync(client, "/User/Delete", indexHtml, new Dictionary<string, string>
        {
            ["id"] = userId
        });
        var result = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("succeeded").GetBoolean().Should().BeTrue();

        var indexAfter = await client.GetAsync("/User/Index");
        (await indexAfter.Content.ReadAsStringAsync()).Should().NotContain(email);
    }

    private static string ExtractSeededAdminId(string html) => ExtractUserIdByEmail(html, "admin@atmticketing.local");

    private static string ExtractUserIdByEmail(string html, string email)
    {
        // The topbar's account-menu button also prints the logged-in user's email/username
        // (see _Layout.cshtml), which appears earlier in the document than the User
        // Administration table — so searching from the very start of the page can match that
        // instead of the actual table row when the email is the currently-logged-in admin's
        // own. Anchor the search inside <tbody> to always land on the real row.
        var tbodyStart = html.IndexOf("<tbody>", StringComparison.Ordinal);
        tbodyStart.Should().BeGreaterThan(-1, "the user list table should have a <tbody>");

        var rowStart = html.IndexOf(email, tbodyStart, StringComparison.Ordinal);
        rowStart.Should().BeGreaterThan(-1, $"'{email}' should be listed in the table body");
        return ExtractNearestEditId(html, rowStart);
    }

    private static string ExtractNearestEditId(string html, int fromIndex)
    {
        const string marker = "/User/Edit/";
        var idx = html.IndexOf(marker, fromIndex, StringComparison.Ordinal);
        idx.Should().BeGreaterThan(-1);
        var start = idx + marker.Length;
        var end = html.IndexOfAny(['"', '\''], start);
        return html[start..end];
    }
}
