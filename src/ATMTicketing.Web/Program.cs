using System.Text;
using ATMTicketing.Application.Interfaces.Services;
using ATMTicketing.Domain.Entities;
using ATMTicketing.Infrastructure;
using ATMTicketing.Infrastructure.Persistence;
using ATMTicketing.Infrastructure.Persistence.Seed;
using ATMTicketing.Web.Filters;
using ATMTicketing.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Services -------------------------------------------------------------

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AuditActionFilter>();
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    // Without this, an AJAX call that hits an [Authorize]-protected action while
    // unauthenticated (401 case) or lacking the required role (403 case) gets silently
    // redirected (302) to the Login/AccessDenied HTML page instead of a clean status code.
    // The browser's XHR/fetch layer follows that redirect transparently, so the caller sees
    // a 200 response whose body is an HTML page, not the JSON error it expects — the click
    // just appears to do nothing. site.js's global ajaxError handler needs the real status
    // code to show "you don't have permission" instead of silently swallowing the failure.
    options.Events.OnRedirectToLogin = context =>
    {
        if (IsAjaxRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (IsAjaxRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

// JWT bearer is offered alongside the cookie scheme so external/mobile REST clients can
// authenticate against the same Api/* controllers without carrying a browser session.
var jwtKey = builder.Configuration["Jwt:Key"] ?? "CHANGE_THIS_DEVELOPMENT_ONLY_SIGNING_KEY_32B!!";
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "AtmTicketingSystem",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "AtmTicketingSystemApi",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdministratorOnly", p => p.RequireRole(Roles.Administrator));
    options.AddPolicy("CanManageTickets", p => p.RequireRole(Roles.Administrator, Roles.CallCenterAgent, Roles.TeamLead));
    options.AddPolicy("CanAssignTickets", p => p.RequireRole(Roles.Administrator, Roles.TeamLead));
    // Call Center Agent escalates tickets they raised; Team Lead owns "escalation management" per spec.
    options.AddPolicy("CanEscalateTickets", p => p.RequireRole(Roles.Administrator, Roles.CallCenterAgent, Roles.TeamLead));
    options.AddPolicy("CanWorkTickets", p => p.RequireRole(Roles.Administrator, Roles.FieldEngineer, Roles.TeamLead));
    options.AddPolicy("CanViewReports", p => p.RequireRole(Roles.Administrator, Roles.OperationsManager, Roles.TeamLead));
});

// Plain fetch/$.ajax calls (every AJAX-posted action in this app — Delete buttons, status
// updates, etc.) can't render a Razor <form> hidden field, so they send the antiforgery
// token via this header instead (see wwwroot/js/site.js's global ajaxSetup). The token is
// still accepted from a form field too when one exists (Create/Edit modals with asp-action).
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ---- Seed database on startup ---------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    await DbInitializer.SeedAsync(context, userManager, roleManager, logger);
}

// ---- Middleware pipeline ----------------------------------------------------

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

// jQuery sets X-Requested-With on every $.post/$.ajax call by default (all of this app's
// state-changing buttons — Close, Escalate, Assign, Delete, status updates); the Accept
// check is a fallback for any client that asks for JSON explicitly instead.
static bool IsAjaxRequest(HttpRequest request) =>
    request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
    request.Headers.Accept.Any(a => a is not null && a.Contains("application/json", StringComparison.OrdinalIgnoreCase));

// Makes the top-level Program class accessible to WebApplicationFactory<Program>
// in the integration test project (see tests/ATMTicketing.Tests/EndToEnd).
public partial class Program;
