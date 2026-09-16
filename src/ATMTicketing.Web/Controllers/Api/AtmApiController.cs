using ATMTicketing.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATMTicketing.Web.Controllers.Api;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ApiController]
[Route("api/atms")]
public class AtmApiController : ControllerBase
{
    private readonly IAtmService _atmService;

    public AtmApiController(IAtmService atmService)
    {
        _atmService = atmService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string term, CancellationToken ct) =>
        Ok(await _atmService.SearchAsync(term ?? string.Empty, ct));
}
