using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ATMTicketing.Tests.EndToEnd;

/// <summary>
/// Drives the full "log in as admin, create an ATM, log a ticket against it, see it in the
/// list" user journey through real HTTP requests against the real app pipeline — the closest
/// this sandbox (no SQL Server, no browser) can get to a genuine end-to-end test while still
/// exercising real routing, Identity cookie auth, RBAC, antiforgery, and the DataTables/REST
/// JSON endpoints the actual UI calls.
/// </summary>
public class AuthenticatedFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthenticatedFlowTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<HttpClient> LoginAsAsync(CustomWebApplicationFactory factory, string email, string password)
    {
        var client = factory.CreateFlowClient(allowAutoRedirect: false);
        var loginPage = await client.GetAsync("/Account/Login");
        var token = HtmlHelpers.ExtractAntiForgeryToken(await loginPage.Content.ReadAsStringAsync());

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        }));

        // A successful login redirects away from the login page (to Dashboard/Index, which
        // the default route collapses to "/" — so assert "not still on the login page"
        // rather than the exact target path).
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "valid credentials should redirect off the login page");
        response.Headers.Location!.OriginalString.Should().NotContain("/Account/Login");

        return client;
    }

    /// <summary>Creates a fresh user with the given role through the real admin /User/Create
    /// form (not a DB seed), then logs in as them — so RBAC tests exercise the exact same
    /// role-assignment path an administrator would use in production.</summary>
    private static async Task<HttpClient> CreateAndLoginAsAsync(CustomWebApplicationFactory factory, string role, string namePrefix)
    {
        using var adminClient = await LoginAsAsync(factory, "admin@atmticketing.local", "Admin@12345");

        var createForm = await adminClient.GetAsync("/User/Create");
        var token = HtmlHelpers.ExtractAntiForgeryToken(await createForm.Content.ReadAsStringAsync());
        var email = $"{namePrefix.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";

        var createResponse = await adminClient.PostAsync("/User/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = namePrefix,
            ["Email"] = email,
            ["Role"] = role,
            ["Password"] = "Passw0rd!23",
            ["__RequestVerificationToken"] = token
        }));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Redirect, $"creating a {role} user should succeed");

        return await LoginAsAsync(factory, email, "Passw0rd!23");
    }

    [Fact]
    public async Task FullTicketWorkflow_LoginCreateAtmCreateTicketAndSeeItInTheList()
    {
        using var client = await LoginAsAsync(_factory, "admin@atmticketing.local", "Admin@12345");

        // Dashboard loads for an authenticated admin.
        var dashboard = await client.GetAsync("/Dashboard/Index");
        dashboard.StatusCode.Should().Be(HttpStatusCode.OK);

        // Dashboard KPI API returns real aggregated data from the DB, not a stub.
        var summary = await client.GetFromJsonAsync<JsonElement>("/api/dashboard/summary");
        summary.GetProperty("totalTickets").GetInt32().Should().BeGreaterThanOrEqualTo(0);

        // Create a brand-new ATM through the real form -> controller -> service -> EF pipeline.
        // RegionId=1 / VendorId=1 rely on DbInitializer's fixed seed order (North region,
        // VEN-001 vendor) against this test's freshly seeded database.
        var createAtmForm = await client.GetAsync("/Atm/Create");
        createAtmForm.StatusCode.Should().Be(HttpStatusCode.OK);
        var atmToken = HtmlHelpers.ExtractAntiForgeryToken(await createAtmForm.Content.ReadAsStringAsync());

        var uniqueCode = $"E2E{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var createAtmResponse = await client.PostAsync("/Atm/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AtmCode"] = uniqueCode,
            ["AtmName"] = "E2E Test ATM",
            ["BankName"] = "E2E Bank",
            ["RegionId"] = "1",
            ["Zone"] = "Zone A",
            ["State"] = "Delhi",
            ["City"] = "New Delhi",
            ["Address"] = "123 Test Street",
            ["VendorId"] = "1",
            ["AtmType"] = "1",
            ["Status"] = "1",
            ["__RequestVerificationToken"] = atmToken
        }));
        createAtmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var atmResult = await createAtmResponse.Content.ReadFromJsonAsync<JsonElement>();
        atmResult.GetProperty("succeeded").GetBoolean().Should().BeTrue(atmResult.ToString());

        // Look the new ATM up by code via the same search endpoint the ticket-creation
        // autocomplete uses, to get its generated Id.
        var atmSearch = await client.GetFromJsonAsync<JsonElement>($"/Atm/Search?term={uniqueCode}");
        atmSearch.GetArrayLength().Should().Be(1);
        var newAtmId = atmSearch[0].GetProperty("id").GetInt32();

        // Log a ticket against it through the real Ticket/Create form.
        var createTicketForm = await client.GetAsync("/Ticket/Create");
        createTicketForm.StatusCode.Should().Be(HttpStatusCode.OK);
        var ticketToken = HtmlHelpers.ExtractAntiForgeryToken(await createTicketForm.Content.ReadAsStringAsync());

        const string ticketDescription = "End-to-end test ticket: cash dispenser jammed";
        var createTicketResponse = await client.PostAsync("/Ticket/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AtmId"] = newAtmId.ToString(),
            ["IncidentType"] = "Cash Jam",
            ["CategoryId"] = "1",
            ["Priority"] = "1", // Critical
            ["Description"] = ticketDescription,
            ["ContactPerson"] = "QA Bot",
            ["ContactNumber"] = "9999999999",
            ["__RequestVerificationToken"] = ticketToken
        }));

        createTicketResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var ticketDetailsPath = HtmlHelpers.PathOf(createTicketResponse.Headers.Location!);
        ticketDetailsPath.Should().StartWith("/Ticket/Details/");
        var ticketId = ticketDetailsPath.Split('/').Last();

        // Follow the redirect and confirm the created ticket's details page renders with the right data.
        var detailsResponse = await client.GetAsync(createTicketResponse.Headers.Location);
        detailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();
        detailsHtml.Should().Contain(ticketDescription);
        detailsHtml.Should().Contain(uniqueCode);
        detailsHtml.Should().Contain("Critical");

        // And confirm it shows up through the DataTables-backed endpoint the Tickets list page uses.
        var listResponse = await client.PostAsync("/Ticket/GetData", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Draw"] = "1", ["Start"] = "0", ["Length"] = "25"
        }));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listJson = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        listJson.GetProperty("data").EnumerateArray()
            .Any(t => t.GetProperty("id").GetInt32().ToString() == ticketId)
            .Should().BeTrue("the just-created ticket should appear in the same paged list the UI renders");
    }

    [Fact]
    public async Task StatusChange_ManualReassign_AndEdit_AllWorkOverRealHttp()
    {
        // Regression coverage for three separately-reported issues: status updates and
        // reassignment both depend on the X-CSRF-TOKEN header fix (see CustomWebApplicationFactory
        // notes / site.js), and Edit didn't exist as a feature at all before this test was added.
        using var client = await LoginAsAsync(_factory, "admin@atmticketing.local", "Admin@12345");

        var createTicketForm = await client.GetAsync("/Ticket/Create");
        var createToken = HtmlHelpers.ExtractAntiForgeryToken(await createTicketForm.Content.ReadAsStringAsync());
        var createResponse = await client.PostAsync("/Ticket/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AtmId"] = "1", // ATM-DEL-001, seeded by DbInitializer
            ["IncidentType"] = "Printer Failure",
            ["CategoryId"] = "1",
            ["Priority"] = "3", // Medium
            ["Description"] = "Receipt printer out of paper",
            ["ContactPerson"] = "QA Bot",
            ["ContactNumber"] = "9999999999",
            ["__RequestVerificationToken"] = createToken
        }));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var ticketId = HtmlHelpers.PathOf(createResponse.Headers.Location!).Split('/').Last();

        // --- Status change ---
        var detailsHtml = await (await client.GetAsync($"/Ticket/Details/{ticketId}")).Content.ReadAsStringAsync();
        var statusResponse = await HtmlHelpers.PostWithCsrfAsync(client, "/Ticket/UpdateStatus", detailsHtml, new Dictionary<string, string>
        {
            ["TicketId"] = ticketId,
            ["NewStatusId"] = "3", // In Progress
            ["Notes"] = "Started work"
        });
        var statusResult = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        statusResult.GetProperty("succeeded").GetBoolean().Should().BeTrue(statusResult.ToString());

        var afterStatusHtml = await (await client.GetAsync($"/Ticket/Details/{ticketId}")).Content.ReadAsStringAsync();
        afterStatusHtml.Should().Contain("In Progress");

        // --- Manual reassign: the fixed endpoint must list the seeded engineer even though
        // they have no prior ticket assignments in this fresh database. ---
        var engineersJson = await client.GetFromJsonAsync<JsonElement>($"/Ticket/AssignableEngineers?ticketId={ticketId}");
        engineersJson.GetArrayLength().Should().BeGreaterThan(0, "at least the seeded field engineer should be assignable");
        var engineerId = engineersJson[0].GetProperty("id").GetString();

        var assignResponse = await HtmlHelpers.PostWithCsrfAsync(client, "/Ticket/Assign", afterStatusHtml, new Dictionary<string, string>
        {
            ["TicketId"] = ticketId,
            ["EngineerId"] = engineerId!
        });
        var assignResult = await assignResponse.Content.ReadFromJsonAsync<JsonElement>();
        assignResult.GetProperty("succeeded").GetBoolean().Should().BeTrue(assignResult.ToString());

        var afterAssignHtml = await (await client.GetAsync($"/Ticket/Details/{ticketId}")).Content.ReadAsStringAsync();
        afterAssignHtml.Should().Contain("Amit Verma"); // the seeded engineer's full name

        // --- Edit: change priority and confirm it took effect + the SLA note is on the timeline. ---
        var editForm = await client.GetAsync($"/Ticket/Edit/{ticketId}");
        editForm.StatusCode.Should().Be(HttpStatusCode.OK);
        var editToken = HtmlHelpers.ExtractAntiForgeryToken(await editForm.Content.ReadAsStringAsync());

        var editResponse = await client.PostAsync("/Ticket/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = ticketId,
            ["IncidentType"] = "Printer Failure",
            ["CategoryId"] = "1",
            ["Priority"] = "1", // Critical
            ["Description"] = "Receipt printer out of paper — escalated to full failure",
            ["ContactPerson"] = "QA Bot",
            ["ContactNumber"] = "9999999999",
            ["__RequestVerificationToken"] = editToken
        }));
        editResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var afterEditHtml = await (await client.GetAsync($"/Ticket/Details/{ticketId}")).Content.ReadAsStringAsync();
        afterEditHtml.Should().Contain("escalated to full failure");
        afterEditHtml.Should().Contain("Critical");
    }

    [Fact]
    public async Task CallCenterAgent_CanEscalateATicketTheyCreated()
    {
        // Regression test: Escalate used to be nested inside the Team-Lead-only "Assign /
        // Reassign" card and gated by the CanAssignTickets policy, so a Call Center Agent —
        // who per spec must be able to escalate tickets — could neither see nor use it.
        using var agentClient = await CreateAndLoginAsAsync(_factory, "CallCenterAgent", "Agent");

        var createForm = await agentClient.GetAsync("/Ticket/Create");
        createForm.StatusCode.Should().Be(HttpStatusCode.OK, "a Call Center Agent must be able to reach the ticket creation form");
        var createToken = HtmlHelpers.ExtractAntiForgeryToken(await createForm.Content.ReadAsStringAsync());

        var createResponse = await agentClient.PostAsync("/Ticket/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AtmId"] = "1",
            ["IncidentType"] = "Network Failure",
            ["CategoryId"] = "1",
            ["Priority"] = "2", // High
            ["Description"] = "Link down at branch",
            ["ContactPerson"] = "QA Bot",
            ["ContactNumber"] = "9999999999",
            ["__RequestVerificationToken"] = createToken
        }));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var ticketId = HtmlHelpers.PathOf(createResponse.Headers.Location!).Split('/').Last();

        var detailsHtml = await (await agentClient.GetAsync($"/Ticket/Details/{ticketId}")).Content.ReadAsStringAsync();
        detailsHtml.Should().Contain("id=\"escalateForm\"", "the Escalate form should render for a Call Center Agent");

        var escalateResponse = await HtmlHelpers.PostWithCsrfAsync(agentClient, "/Ticket/Escalate", detailsHtml, new Dictionary<string, string>
        {
            ["ticketId"] = ticketId,
            ["reason"] = "Customer is a VIP branch"
        });
        var result = await escalateResponse.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("succeeded").GetBoolean().Should().BeTrue(result.ToString());

        var afterHtml = await (await agentClient.GetAsync($"/Ticket/Details/{ticketId}")).Content.ReadAsStringAsync();
        afterHtml.Should().Contain("Escalated");
    }

    [Fact]
    public async Task TeamLead_CanCloseATicket()
    {
        // Regression test: the server-side CanWorkTickets policy already allowed Team Lead to
        // close tickets, but the Close form only rendered for FieldEngineer/Administrator, so
        // a Team Lead had permission with no UI path to use it.
        using var teamLeadClient = await CreateAndLoginAsAsync(_factory, "TeamLead", "Lead");

        using var adminClient = await LoginAsAsync(_factory, "admin@atmticketing.local", "Admin@12345");
        var createForm = await adminClient.GetAsync("/Ticket/Create");
        var createToken = HtmlHelpers.ExtractAntiForgeryToken(await createForm.Content.ReadAsStringAsync());
        var createResponse = await adminClient.PostAsync("/Ticket/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AtmId"] = "1",
            ["IncidentType"] = "Cash Out",
            ["CategoryId"] = "1",
            ["Priority"] = "2",
            ["Description"] = "ATM out of cash",
            ["ContactPerson"] = "QA Bot",
            ["ContactNumber"] = "9999999999",
            ["__RequestVerificationToken"] = createToken
        }));
        var ticketId = HtmlHelpers.PathOf(createResponse.Headers.Location!).Split('/').Last();

        var detailsHtml = await (await teamLeadClient.GetAsync($"/Ticket/Details/{ticketId}")).Content.ReadAsStringAsync();
        detailsHtml.Should().Contain("id=\"closeForm\"", "the Close form should render for a Team Lead, matching the CanWorkTickets server policy");

        var closeResponse = await HtmlHelpers.PostWithCsrfAsync(teamLeadClient, "/Ticket/Close", detailsHtml, new Dictionary<string, string>
        {
            ["TicketId"] = ticketId,
            ["ResolutionNotes"] = "Cash replenished by vendor",
            ["ClosureRemarks"] = "Confirmed working with branch"
        });
        var result = await closeResponse.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("succeeded").GetBoolean().Should().BeTrue(result.ToString());

        var afterHtml = await (await teamLeadClient.GetAsync($"/Ticket/Details/{ticketId}")).Content.ReadAsStringAsync();
        afterHtml.Should().Contain("Closed");
    }

    [Fact]
    public async Task JwtAuth_TokenIssuedOnLogin_WorksAgainstTheRestApi()
    {
        using var client = _factory.CreateFlowClient();

        var tokenResponse = await client.PostAsJsonJson("/api/auth/token", new { Email = "admin@atmticketing.local", Password = "Admin@12345" });
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = tokenJson.GetProperty("accessToken").GetString();
        accessToken.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var myTicketsResponse = await client.GetAsync("/api/tickets/my");

        myTicketsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonAdmin_CannotReachUserAdministration_GetsForbidden()
    {
        using var client = await LoginAsAsync(_factory, "engineer1@atmticketing.local", "Engineer@12345");

        var response = await client.GetAsync("/User/Index");

        // ASP.NET Core Identity's cookie handler turns a role-authorization failure into a
        // redirect to AccessDeniedPath rather than a bare 403, so assert on that redirect.
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        HtmlHelpers.PathOf(response.Headers.Location!).Should().StartWith("/Account/AccessDenied");
    }
}
