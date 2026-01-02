using Microsoft.AspNetCore.Mvc;
using Strativ.Api.Models;
using Strativ.Api.Services;

namespace Strativ.Api.Controllers;

[ApiController]
[Route("api/v1/recommendation")]
public class RecommendationController : ControllerBase
{
    private readonly IWeatherService _weatherService;

    public RecommendationController(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    [HttpPost]
    public async Task<ActionResult<RecommendationResponse>> Post([FromBody] RecommendationRequest request)
    {
        var response = await _weatherService.GetRecommendationAsync(request);
        return Ok(response);
    }
}