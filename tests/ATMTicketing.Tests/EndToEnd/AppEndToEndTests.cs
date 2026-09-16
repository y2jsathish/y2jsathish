using System.Net;
using FluentAssertions;
using Xunit;

namespace ATMTicketing.Tests.EndToEnd;

/// <summary>
/// True end-to-end coverage: boots the real Web app (Program.cs, routing, Identity, RBAC,
/// antiforgery, DataTables JSON endpoints, REST/JWT API) in-process against an isolated
/// in-memory database and drives it with plain HTTP requests, exactly as a browser or REST
/// client would. One factory per test class = one isolated seeded database per test class
/// (IClassFixture shares it across the tests within a class, which matters here since the
/// seeded admin user needs to exist for every test in AuthenticatedFlowTests).
/// </summary>
public class AnonymousAccessTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AnonymousAccessTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task RootDashboard_WithoutLogin_RedirectsToLoginPage()
    {
        using var client = _factory.CreateFlowClient(allowAutoRedirect: false);

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        HtmlHelpers.PathOf(response.Headers.Location!).Should().StartWith("/Account/Login");
    }

    [Fact]
    public async Task LoginPage_Get_ReturnsOkWithLoginForm()
    {
        using var client = _factory.CreateFlowClient();

        var response = await client.GetAsync("/Account/Login");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("__RequestVerificationToken");
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsLoginPageAgain_NotRedirect()
    {
        using var client = _factory.CreateFlowClient(allowAutoRedirect: false);
        var loginPage = await client.GetAsync("/Account/Login");
        var token = HtmlHelpers.ExtractAntiForgeryToken(await loginPage.Content.ReadAsStringAsync());

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "admin@atmticketing.local",
            ["Password"] = "WrongPassword!",
            ["__RequestVerificationToken"] = token
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK); // re-renders the form with a validation error, no redirect
    }

    [Fact]
    public async Task TicketCreatePage_WithoutLogin_RedirectsToLogin()
    {
        using var client = _factory.CreateFlowClient(allowAutoRedirect: false);

        var response = await client.GetAsync("/Ticket/Create");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("/Account/Login");
    }

    [Fact]
    public async Task JwtApi_Tickets_WithoutToken_Returns401()
    {
        using var client = _factory.CreateFlowClient();

        var response = await client.GetAsync("/api/tickets/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthToken_WithInvalidCredentials_Returns401()
    {
        using var client = _factory.CreateFlowClient();

        var response = await client.PostAsJsonJson("/api/auth/token", new { Email = "nobody@nowhere.local", Password = "wrong" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

internal static class HttpClientJsonExtensions
{
    // Minimal helper so the test project doesn't need to pull in System.Net.Http.Json
    // configuration beyond what's already implied by ASP.NET Core's shared framework.
    public static Task<HttpResponseMessage> PostAsJsonJson(this HttpClient client, string url, object body) =>
        client.PostAsync(url, new StringContent(
            System.Text.Json.JsonSerializer.Serialize(body),
            System.Text.Encoding.UTF8,
            "application/json"));
}
