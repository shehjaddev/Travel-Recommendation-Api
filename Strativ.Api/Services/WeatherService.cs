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
        bool fromCache = _cache.TryGetValue(CacheKey, out List<TopDistrictResponse> cached);

        if (fromCache)
        {
            return (cached!, true);
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
        for (int i = 0; i < times.Count; i++)
        {
            if (DateTime.Parse(times[i]).Hour == 14 && values[i].HasValue)
            {
                result.Add(values[i].Value);
            }
        }
        return result;
    }
}