using Moq;
using Moq.Protected;
using Strativ.Api.Models;
using Strativ.Api.Services;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class WeatherServiceTests
{
    [Fact(DisplayName = "GetTop10DistrictsAsync returns districts ordered by temperature then PM2.5 and limited to top 10")]
    public async Task GetTop10DistrictsAsync_ReturnsOrderedTopDistricts()
    {
        // Arrange
        var districts = new List<District>
        {
            new() { Name = "Dhaka", Lat = 23.81, Long = 90.41 },
            new() { Name = "Bandarban", Lat = 22.0, Long = 92.3 },
            new() { Name = "Chittagong", Lat = 22.3, Long = 91.8 }
        };

        var districtsServiceMock = new Mock<IDistrictsService>();
        districtsServiceMock
            .Setup(s => s.GetDistrictsAsync())
            .ReturnsAsync(districts);

        var handlerMock = new Mock<HttpMessageHandler>();

        var weatherJson = """
        [
          {"hourly":{"time":["2026-01-05T14:00"],"temperature_2m":[30.0]}},
          {"hourly":{"time":["2026-01-05T14:00"],"temperature_2m":[20.0]}},
          {"hourly":{"time":["2026-01-05T14:00"],"temperature_2m":[25.0]}}
        ]
        """;

        var airJson = """
        [
          {"hourly":{"time":["2026-01-05T14:00"],"pm2_5":[80.0]}},
          {"hourly":{"time":["2026-01-05T14:00"],"pm2_5":[20.0]}},
          {"hourly":{"time":["2026-01-05T14:00"],"pm2_5":[50.0]}}
        ]
        """;

        handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(weatherJson, Encoding.UTF8, "application/json")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(airJson, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);

        var httpFactoryMock = new Mock<IHttpClientFactory>();
        httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<WeatherService>.Instance;

        var service = new WeatherService(
            districtsServiceMock.Object,
            httpFactoryMock.Object,
            memoryCache,
            logger);

        // Act
        var (results, fromCache) = await service.GetTop10DistrictsAsync();

        // Assert
        Assert.False(fromCache);
        Assert.Equal(3, results.Count);
        Assert.Equal("Bandarban", results[0].Name); // coolest & cleanest
        Assert.Equal("Chittagong", results[1].Name);
        Assert.Equal("Dhaka", results[2].Name); // hottest & dirtiest
    }

    [Fact(DisplayName = "GetTop10DistrictsAsync uses cache on subsequent calls")]
    public async Task GetTop10DistrictsAsync_UsesCacheOnSecondCall()
    {
        // Arrange
        var districts = new List<District>
        {
            new() { Name = "Dhaka", Lat = 23.81, Long = 90.41 }
        };

        var districtsServiceMock = new Mock<IDistrictsService>();
        districtsServiceMock
            .Setup(s => s.GetDistrictsAsync())
            .ReturnsAsync(districts);

        var handlerMock = new Mock<HttpMessageHandler>();

        var weatherJson = """
        [
          {"hourly":{"time":["2026-01-05T14:00"],"temperature_2m":[30.0]}}
        ]
        """;

        var airJson = """
        [
          {"hourly":{"time":["2026-01-05T14:00"],"pm2_5":[80.0]}}
        ]
        """;

        handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(weatherJson, Encoding.UTF8, "application/json")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(airJson, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);

        var httpFactoryMock = new Mock<IHttpClientFactory>();
        httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<WeatherService>.Instance;

        var service = new WeatherService(
            districtsServiceMock.Object,
            httpFactoryMock.Object,
            memoryCache,
            logger);

        // Act
        var (firstResults, firstFromCache) = await service.GetTop10DistrictsAsync();
        var (secondResults, secondFromCache) = await service.GetTop10DistrictsAsync();

        // Assert
        Assert.False(firstFromCache);
        Assert.True(secondFromCache);
        Assert.Single(firstResults);
        Assert.Single(secondResults);
        Assert.Equal(firstResults[0].Name, secondResults[0].Name);
    }

    [Fact(DisplayName = "Recommendation returns 'Recommended' when destination is cooler and has better air quality at 2 PM")]
    public async Task GetRecommendationAsync_ReturnsRecommended_WhenCoolerAndCleaner()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();

        var weatherJson = """
        [
          {"hourly":{"time":["2026-01-05T14:00"],"temperature_2m":[28.0]}},
          {"hourly":{"time":["2026-01-05T14:00"],"temperature_2m":[22.0]}}
        ]
        """;

        var airJson = """
        [
          {"hourly":{"time":["2026-01-05T14:00"],"pm2_5":[80.0]}},
          {"hourly":{"time":["2026-01-05T14:00"],"pm2_5":[15.0]}}
        ]
        """;

        handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(weatherJson, Encoding.UTF8, "application/json")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(airJson, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);

        var httpFactoryMock = new Mock<IHttpClientFactory>();
        httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var districtsService = new DistrictsService();
        var memoryCacheMock = new Mock<IMemoryCache>();
        var logger = NullLogger<WeatherService>.Instance;

        var service = new WeatherService(
            districtsService,
            httpFactoryMock.Object,
            memoryCacheMock.Object,
            logger);

        var request = new RecommendationRequest
        {
            CurrentLatitude = 23.81,
            CurrentLongitude = 90.41,
            DestinationDistrict = "Bandarban",
            TravelDate = new DateOnly(2026, 1, 5)
        };

        // Act
        var response = await service.GetRecommendationAsync(request);

        // Assert
        Assert.Equal("Recommended", response.Recommendation);
        Assert.Contains("cooler", response.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("better air quality", response.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Recommendation returns 'Not Recommended' when destination is hotter and has worse air quality at 2 PM")]
    public async Task GetRecommendationAsync_ReturnsNotRecommended_WhenHotterAndWorse()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();

        var weatherJson = """
        [
          {"hourly":{"time":["2026-01-05T14:00"],"temperature_2m":[22.0]}},
          {"hourly":{"time":["2026-01-05T14:00"],"temperature_2m":[30.0]}}
        ]
        """;

        var airJson = """
        [
          {"hourly":{"time":["2026-01-05T14:00"],"pm2_5":[15.0]}},
          {"hourly":{"time":["2026-01-05T14:00"],"pm2_5":[80.0]}}
        ]
        """;

        handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(weatherJson, Encoding.UTF8, "application/json")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(airJson, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);

        var httpFactoryMock = new Mock<IHttpClientFactory>();
        httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var districtsService = new DistrictsService();
        var memoryCacheMock = new Mock<IMemoryCache>();
        var logger = NullLogger<WeatherService>.Instance;

        var service = new WeatherService(
            districtsService,
            httpFactoryMock.Object,
            memoryCacheMock.Object,
            logger);

        var request = new RecommendationRequest
        {
            CurrentLatitude = 23.81,
            CurrentLongitude = 90.41,
            DestinationDistrict = "Bandarban",
            TravelDate = new DateOnly(2026, 1, 5)
        };

        // Act
        var response = await service.GetRecommendationAsync(request);

        // Assert
        Assert.Equal("Not Recommended", response.Recommendation);
        Assert.Contains("hotter", response.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("worse air quality", response.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Recommendation returns 'Not Recommended' when travel date is outside next 7 days")]
    public async Task GetRecommendationAsync_ReturnsNotRecommended_WhenDateOutOfRange()
    {
        // Arrange
        var httpFactoryMock = new Mock<IHttpClientFactory>();
        var memoryCacheMock = new Mock<IMemoryCache>();
        var logger = NullLogger<WeatherService>.Instance;
        var districtsService = new DistrictsService();

        var service = new WeatherService(
            districtsService,
            httpFactoryMock.Object,
            memoryCacheMock.Object,
            logger);

        var pastDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        var request = new RecommendationRequest
        {
            CurrentLatitude = 23.81,
            CurrentLongitude = 90.41,
            DestinationDistrict = "Bandarban",
            TravelDate = pastDate
        };

        // Act
        var response = await service.GetRecommendationAsync(request);

        // Assert
        Assert.Equal("Not Recommended", response.Recommendation);
        Assert.Contains("Travel date must be within the next 7 days", response.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Recommendation returns 'Not Recommended' when destination district is not found")]
    public async Task GetRecommendationAsync_ReturnsNotRecommended_WhenDestinationNotFound()
    {
        // Arrange
        var httpFactoryMock = new Mock<IHttpClientFactory>();
        var memoryCacheMock = new Mock<IMemoryCache>();
        var logger = NullLogger<WeatherService>.Instance;
        var districtsService = new DistrictsService();

        var service = new WeatherService(
            districtsService,
            httpFactoryMock.Object,
            memoryCacheMock.Object,
            logger);

        var request = new RecommendationRequest
        {
            CurrentLatitude = 23.81,
            CurrentLongitude = 90.41,
            DestinationDistrict = "NonExistingDistrict",
            TravelDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
        };

        // Act
        var response = await service.GetRecommendationAsync(request);

        // Assert
        Assert.Equal("Not Recommended", response.Recommendation);
        Assert.Contains("Destination district not found", response.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Recommendation returns 'Not Recommended' when no 2 PM data is available")]
    public async Task GetRecommendationAsync_ReturnsNotRecommended_WhenNoTwoPmData()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();

        var weatherJson = """
        [
          {"hourly":{"time":["2026-01-05T13:00"],"temperature_2m":[28.0]}},
          {"hourly":{"time":["2026-01-05T13:00"],"temperature_2m":[22.0]}}
        ]
        """;

        var airJson = """
        [
          {"hourly":{"time":["2026-01-05T13:00"],"pm2_5":[80.0]}},
          {"hourly":{"time":["2026-01-05T13:00"],"pm2_5":[15.0]}}
        ]
        """;

        handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(weatherJson, Encoding.UTF8, "application/json")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(airJson, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);

        var httpFactoryMock = new Mock<IHttpClientFactory>();
        httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var districtsService = new DistrictsService();
        var memoryCacheMock = new Mock<IMemoryCache>();
        var logger = NullLogger<WeatherService>.Instance;

        var service = new WeatherService(
            districtsService,
            httpFactoryMock.Object,
            memoryCacheMock.Object,
            logger);

        var request = new RecommendationRequest
        {
            CurrentLatitude = 23.81,
            CurrentLongitude = 90.41,
            DestinationDistrict = "Bandarban",
            TravelDate = new DateOnly(2026, 1, 5)
        };

        // Act
        var response = await service.GetRecommendationAsync(request);

        // Assert
        Assert.Equal("Not Recommended", response.Recommendation);
        Assert.Contains("No 2 PM weather data available", response.Reason, StringComparison.OrdinalIgnoreCase);
    }
}