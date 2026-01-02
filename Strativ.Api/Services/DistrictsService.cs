using System.Text.Json;
using System.Text.Json.Nodes;
using Strativ.Api.Models;

namespace Strativ.Api.Services;

public class DistrictsService : IDistrictsService
{
    private const string FilePath = "Data/bd-districts.json";

    public async Task<List<District>> GetDistrictsAsync()
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, FilePath);
        var json = await File.ReadAllTextAsync(fullPath);

        var jsonObject = JsonNode.Parse(json)!;
        var districtsArray = jsonObject["districts"]!.AsArray();

        var districts = new List<District>();

        foreach (var item in districtsArray)
        {
            districts.Add(new District
            {
                Name = item!["name"]!.GetValue<string>(),
                Lat = double.Parse(item["lat"]!.GetValue<string>()),
                Long = double.Parse(item["long"]!.GetValue<string>())
            });
        }

        return districts;
    }
}