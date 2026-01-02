using Strativ.Api.BackgroundServices;
using Strativ.Api.Controllers;
using Strativ.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddHostedService<CacheWarmupService>();

builder.Services.AddScoped<IDistrictsService, DistrictsService>();
builder.Services.AddScoped<IWeatherService, WeatherService>();

builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();