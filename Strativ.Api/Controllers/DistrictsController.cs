using Microsoft.AspNetCore.Mvc;
using Strativ.Api.Models;
using Strativ.Api.Services;

namespace Strativ.Api.Controllers;

[ApiController]
[Route("api/v1/districts")]
public class DistrictsController : ControllerBase
{
    private readonly IWeatherService _weatherService;

    public DistrictsController(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    [HttpGet("top10")]
    public async Task<ActionResult<List<TopDistrictResponse>>> GetTop10()
    {
        var (top10, _) = await _weatherService.GetTop10DistrictsAsync();
        return Ok(top10);
    }
}