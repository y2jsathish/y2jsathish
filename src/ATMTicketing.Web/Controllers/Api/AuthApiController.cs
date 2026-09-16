using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ATMTicketing.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ATMTicketing.Web.Controllers.Api;

/// <summary>Issues short-lived JWTs for REST/mobile clients that cannot carry the Identity cookie.</summary>
[AllowAnonymous]
[ApiController]
[Route("api/auth")]
public class AuthApiController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;

    public AuthApiController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
    }

    public record TokenRequest(string Email, string Password);
    public record TokenResponse(string AccessToken, DateTime ExpiresAtUtc);

    [HttpPost("token")]
    public async Task<IActionResult> Token([FromBody] TokenRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var check = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var m) ? m : 60;
        var expires = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Email ?? string.Empty),
            new("FullName", user.FullName)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration["Jwt:Key"] ?? "CHANGE_THIS_DEVELOPMENT_ONLY_SIGNING_KEY_32B!!"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "AtmTicketingSystem",
            audience: _configuration["Jwt:Audience"] ?? "AtmTicketingSystemApi",
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return Ok(new TokenResponse(new JwtSecurityTokenHandler().WriteToken(token), expires));
    }
}
