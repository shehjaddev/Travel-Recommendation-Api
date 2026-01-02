using Strativ.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

builder.Services.AddScoped<IDistrictsService, DistrictsService>();

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

app.Run();