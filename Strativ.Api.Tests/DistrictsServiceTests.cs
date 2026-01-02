using System.Linq;
using System.Threading.Tasks;
using Strativ.Api.Services;
using Xunit;

namespace Strativ.Api.Tests;

public class DistrictsServiceTests
{
    [Fact(DisplayName = "DistrictsService successfully loads exactly 64 districts from local JSON")]
    public async Task GetDistrictsAsync_Returns64Districts()
    {
        // Arrange
        var service = new DistrictsService();

        // Act
        var districts = await service.GetDistrictsAsync();

        // Assert
        Assert.Equal(64, districts.Count);
        Assert.Contains(districts, d => d.Name == "Dhaka");
        Assert.Contains(districts, d => d.Name == "Bandarban");
        Assert.All(districts, d => Assert.InRange(d.Lat, 20.0, 27.0)); // Bangladesh latitude range
        Assert.All(districts, d => Assert.InRange(d.Long, 88.0, 93.0)); // Bangladesh longitude range
    }
}