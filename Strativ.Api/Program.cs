using Strativ.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

builder.Services.AddScoped<IDistrictsService, DistrictsService>();
builder.Services.AddScoped<IWeatherService, WeatherService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Debug endpoint to verify districts load correctly
app.MapGet("/debug/districts", async (IDistrictsService service) =>
{
    var districts = await service.GetDistrictsAsync();
    return Results.Ok(new
    {
        Count = districts.Count,
        Sample = districts.Take(3).Select(d => new { d.Name, d.Lat, d.Long })
    });
});

// Debug endpoint to verify top10 load correctly
app.MapGet("/debug/top10", async (IWeatherService service) =>
{
    var (top10, fromCache) = await service.GetTop10DistrictsAsync();
    
    return Results.Ok(new
    {
        Count = top10.Count,
        Data = top10,
        FromCache = fromCache
    });
});

app.Run();