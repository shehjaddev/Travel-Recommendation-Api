namespace Strativ.Api.Models.Meteo;

public class MeteoLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Elevation { get; set; }
    public int UtcOffsetSeconds { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public string TimezoneAbbreviation { get; set; } = string.Empty;

    public HourlyData Hourly { get; set; } = new();
}

public class HourlyData
{
    public List<string> Time { get; set; } = new();
    public List<double?> Temperature_2m { get; set; } = new();
    public List<double?> Pm2_5 { get; set; } = new();
}