using Strativ.Api.Models;

namespace Strativ.Api.Services;

public interface IWeatherService
{
    Task<(List<TopDistrictResponse> Results, bool FromCache)> GetTop10DistrictsAsync();

    Task<RecommendationResponse> GetRecommendationAsync(RecommendationRequest request);
}