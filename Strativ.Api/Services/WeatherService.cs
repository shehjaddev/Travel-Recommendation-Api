using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Strativ.Api.Models;
using Strativ.Api.Models.Meteo;

namespace Strativ.Api.Services;

public class WeatherService : IWeatherService
{
    private readonly IDistrictsService _districtsService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WeatherService> _logger;

    private const string CacheKey = "Top10Districts";
    private const string WeatherUrl = "https://api.open-meteo.com/v1/forecast";
    private const string AirQualityUrl = "https://air-quality-api.open-meteo.com/v1/air-quality";

    public WeatherService(
        IDistrictsService districtsService,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<WeatherService> logger)
    {
        _districtsService = districtsService;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(List<TopDistrictResponse> Results, bool FromCache)> GetTop10DistrictsAsync()
    {
        if (_cache.TryGetValue(CacheKey, out List<TopDistrictResponse>? cached) && cached is not null)
        {
            return (cached, true);
        }

        var districts = await _districtsService.GetDistrictsAsync();

        // Format coordinates with invariant culture (decimal point)
        var latitudes = string.Join(",", districts.Select(d => d.Lat.ToString(CultureInfo.InvariantCulture)));
        var longitudes = string.Join(",", districts.Select(d => d.Long.ToString(CultureInfo.InvariantCulture)));

        var client = _httpClientFactory.CreateClient();

        // Weather
        var weatherQuery = $"?latitude={latitudes}&longitude={longitudes}&hourly=temperature_2m&forecast_days=7&timezone=Asia%2FDhaka";
        var weatherJson = await client.GetStringAsync(WeatherUrl + weatherQuery);
        var weatherLocations = JsonSerializer.Deserialize<List<MeteoLocation>>(weatherJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        // Air Quality
        var airQuery = $"?latitude={latitudes}&longitude={longitudes}&hourly=pm2_5&forecast_days=7&timezone=Asia%2FDhaka";
        var airJson = await client.GetStringAsync(AirQualityUrl + airQuery);
        var airLocations = JsonSerializer.Deserialize<List<MeteoLocation>>(airJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var results = new List<TopDistrictResponse>(districts.Count);

        for (int i = 0; i < districts.Count; i++)
        {
            var district = districts[i];
            var tempValues = Extract2PmValues(weatherLocations[i].Hourly.Time, weatherLocations[i].Hourly.Temperature_2m);
            var pm25Values = Extract2PmValues(airLocations[i].Hourly.Time, airLocations[i].Hourly.Pm2_5);

            var avgTemp = tempValues.Any() ? Math.Round(tempValues.Average(), 1) : 999.0;
            var avgPm25 = pm25Values.Any() ? Math.Round(pm25Values.Average(), 1) : 999.0;

            results.Add(new TopDistrictResponse
            {
                Name = district.Name,
                AverageTemperature2Pm = avgTemp,
                AveragePm25_2Pm = avgPm25
            });
        }

        var top10 = results
            .OrderBy(r => r.AverageTemperature2Pm)
            .ThenBy(r => r.AveragePm25_2Pm)
            .Take(10)
            .ToList();

        _cache.Set(CacheKey, top10, TimeSpan.FromMinutes(30));

        return (top10, false);
    }

    private static List<double> Extract2PmValues(List<string> times, List<double?> values)
    {
        var result = new List<double>();
        for (int i = 0; i < times.Count && i < values.Count; i++)
        {
            if (DateTime.Parse(times[i]).Hour == 14 && values[i].HasValue)
            {
                result.Add(values[i].Value);
            }
        }
        return result;
    }

    public async Task<RecommendationResponse> GetRecommendationAsync(RecommendationRequest request)
    {
        // Validate date: within next 7 days
        var travelDate = request.TravelDate.ToDateTime(TimeOnly.MinValue);
        if (travelDate < DateTime.UtcNow.AddHours(6).Date || travelDate > DateTime.UtcNow.AddDays(7).Date)
        {
            return new RecommendationResponse
            {
                Recommendation = "Not Recommended",
                Reason = "Travel date must be within the next 7 days."
            };
        }

        var districts = await _districtsService.GetDistrictsAsync();
        var destDistrict = districts.FirstOrDefault(d => 
            string.Equals(d.Name, request.DestinationDistrict, StringComparison.OrdinalIgnoreCase));

        if (destDistrict == null)
        {
            return new RecommendationResponse
            {
                Recommendation = "Not Recommended",
                Reason = "Destination district not found."
            };
        }

        // Format coords
        var lats = $"{request.CurrentLatitude.ToString(CultureInfo.InvariantCulture)},{destDistrict.Lat.ToString(CultureInfo.InvariantCulture)}";
        var longs = $"{request.CurrentLongitude.ToString(CultureInfo.InvariantCulture)},{destDistrict.Long.ToString(CultureInfo.InvariantCulture)}";

        var client = _httpClientFactory.CreateClient();

        var dateStr = request.TravelDate.ToString("yyyy-MM-dd");
        var query = $"?latitude={lats}&longitude={longs}&daily=temperature_2m_max&start_date={dateStr}&end_date={dateStr}&timezone=Asia%2FDhaka";

        // Current location temp (index 0), destination (index 1)
        var weatherJson = await client.GetStringAsync(WeatherUrl + query);
        var weatherList = JsonSerializer.Deserialize<List<MeteoLocation>>(weatherJson,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var currentTemp = weatherList[0].Hourly.Temperature_2m?.FirstOrDefault() ?? 999;
        var destTemp = weatherList[1].Hourly.Temperature_2m?.FirstOrDefault() ?? 999;

        // PM2.5 - use daily average or hourly 2PM if available
        var airQuery = $"?latitude={lats}&longitude={longs}&hourly=pm2_5&start_date={dateStr}&end_date={dateStr}&timezone=Asia%2FDhaka";
        var airJson = await client.GetStringAsync(AirQualityUrl + airQuery);
        var airList = JsonSerializer.Deserialize<List<MeteoLocation>>(airJson,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var currentPm25 = Extract2PmValues(airList[0].Hourly.Time, airList[0].Hourly.Pm2_5).FirstOrDefault(999);
        var destPm25 = Extract2PmValues(airList[1].Hourly.Time, airList[1].Hourly.Pm2_5).FirstOrDefault(999);

        bool isCooler = destTemp < currentTemp;
        bool isCleaner = destPm25 < currentPm25;

        if (isCooler && isCleaner)
        {
            return new RecommendationResponse
            {
                Recommendation = "Recommended",
                Reason = $"Your destination is {Math.Round(currentTemp - destTemp, 1)}°C cooler and has better air quality (PM2.5 {Math.Round(currentPm25 - destPm25, 1)} μg/m³ lower). Enjoy your trip!"
            };
        }

        return new RecommendationResponse
        {
            Recommendation = "Not Recommended",
            Reason = $"Your destination is {(isCooler ? "cooler" : "hotter")} but has {(isCleaner ? "better" : "worse")} air quality than your current location. It's better to stay where you are."
        };
    }
}