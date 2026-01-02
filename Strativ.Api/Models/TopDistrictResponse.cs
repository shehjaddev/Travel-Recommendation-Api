namespace Strativ.Api.Models;

public class TopDistrictResponse
{
    public string Name { get; set; } = string.Empty;
    public double AverageTemperature2Pm { get; set; }
    public double AveragePm25_2Pm { get; set; }
}