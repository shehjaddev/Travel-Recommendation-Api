# Travel Recommendation API

A .NET 8 REST API that helps users find the best districts in Bangladesh for travel based on **temperature** and **air quality**.

The API:

- **Ranks the coolest and cleanest 10 districts** in Bangladesh using Open-Meteo weather and air quality data.
- Provides a **travel recommendation** for a user’s trip based on their current location, destination district, and travel date.

---

## Tech Stack

- **Runtime / Framework**: .NET 8 (`net8.0`)
- **Project type**: ASP.NET Core Web API
- **API documentation / UI**: Swagger (Swashbuckle)
- **HTTP client**: `HttpClient` (built-in)
- **JSON**: `System.Text.Json`
- **Container**: Docker + `docker-compose`
- **Testing**: xUnit (via `Strativ.Api.Tests`)

---

## Project Structure

```text
StrativAssignment/
├─ Strativ.Api/               # Main Web API project
│  ├─ Controllers/            # API controllers
│  ├─ Services/               # Weather/air-quality and domain services
│  ├─ Data/
│  │  └─ bd-districts.json    # Districts with latitude/longitude
│  ├─ BackgroundServices/     # Cache warmup & related background tasks
│  ├─ Constants/              # Cache key and other constants
│  ├─ Properties/
│  │  └─ launchSettings.json
│  └─ Strativ.Api.csproj
├─ Strativ.Api.Tests/         # Test project
│  ├─ TestData/               # Sample external API responses
│  └─ *.cs
├─ docker-compose.yml
└─ StrativAssignment.sln
```

---

## Domain Logic Overview

### 1. Top 10 Coolest & Cleanest Districts

- Uses `bd-districts.json` to get **all districts** with `latitude` and `longitude`.
- For each district, calls **Open-Meteo Weather API** to fetch:
  - Hourly temperature for the **next 7 days**.
  - Specifically extracts **temperature at 14:00 / 2 PM** for each day.
  - Computes the **7-day average temperature at 2 PM**.
- Calls **Open-Meteo Air Quality API** to fetch:
  - PM2.5 (fine particulate matter) values.
  - Computes an average PM2.5 over the same 7-day period (or nearest supported resolution).
- **Ranking logic**:
  - Primary sort key: **lower average temperature** (cooler is better).
  - Tie-breaker: **lower PM2.5** (cleaner is better).
- Returns the **top 10 districts** with their metrics.

To help meet the **≤ 500 ms** response requirement, the implementation:

- Uses background **cache warmup** to avoid sequential external calls.
- Caches district-level computed data for a period so multiple requests don’t re-hit the third-party APIs unnecessarily.

### 2. Travel Recommendation

- Input:
  - Current location: `latitude`, `longitude`
  - Destination: district name (or ID)
  - Travel date (date only)
- For the **travel date at 14:00 (2 PM)**:
  - Compares current location vs destination:
    - Temperature at 2 PM.
    - PM2.5.
- Result:
  - Returns `"Recommended"` if **destination is cooler AND has better air quality** (both conditions met).
  - Otherwise returns `"Not Recommended"`.
- Always includes a **short human-readable reason**, for example:
  - `"Your destination is 3°C cooler and has significantly better air quality. Enjoy your trip!"`
  - `"Your destination is hotter and has worse air quality than your current location. It’s better to stay where you are."`

---

## External APIs

### 1. Districts Data

- Source:  
  `https://raw.githubusercontent.com/strativ-dev/technical-screening-test/main/bd-districts.json`
- This file is **vendored into the project** as `Strativ.Api/Data/bd-districts.json` and copied to the output at build time.

### 2. Open-Meteo APIs

Base docs: https://open-meteo.com/en/docs

Used endpoints (conceptually):

- **Weather Forecast API**  
  Used to fetch hourly temperature, especially at `14:00`, for the next 7 days.

- **Air Quality API**  
  Used to fetch PM2.5 levels over the same horizon.

Notes:

- Open-Meteo does **not require an API key**.
- Requests are constructed with district or user-provided coordinates and the needed variables:
  - Weather: `hourly=temperature_2m`
  - Air quality: `hourly=pm2_5` (or equivalent depending on their current spec)

If Open-Meteo changes its contract, the service layer is the single point to update.

---

## Prerequisites

- **.NET SDK 8.0+**  
  Download from: https://dotnet.microsoft.com/en-us/download
- **Optional**:
  - Docker & Docker Compose (for containerized run)
  - `curl` or a REST client (Postman, VS Code REST, etc.)

---

## How to Run (Locally with .NET)

From the repo root:

```bash
# Restore dependencies
dotnet restore

# Run the API
dotnet run --project Strativ.Api
```

By default (per `launchSettings.json`), the app listens on:

- `http://localhost:5100`

Swagger UI will be available at:

- `http://localhost:5100/swagger`

---

## How to Run with Docker

From the repo root:

```bash
# Build and run using docker-compose
docker compose up --build
```

The service will be exposed at:

- `http://localhost:8080`

(As defined in `docker-compose.yml`)

Swagger UI in Docker:

- `http://localhost:8080/swagger`

To stop:

```bash
docker compose down
```

---

## Configuration

This project primarily relies on:

- Built-in .NET configuration (appsettings + environment variables).
- `ASPNETCORE_ENVIRONMENT`:
  - Default in `docker-compose.yml`: `Development`.
  - Default in `launchSettings.json`: `Development`.

Any additional configuration for timeouts, caching, or external API URLs can be provided via:

- `appsettings.json` / `appsettings.Development.json`
- Environment variables in container or host.

(Inspect the `Services` and `BackgroundServices` folders for specific settings that can be overridden.)

---

## API Endpoints

> The exact routes and DTO shapes are visible in Swagger once you run the project.  
> Below is a conceptual contract; adjust names to match your controllers if they differ.

### 1. Get Top 10 Best Districts

- **HTTP**: `GET`
- **Route**: `/api/districts/top10`  
  (See `DistrictsController`)

#### Query Parameters (if implemented)

- `days` _(optional, int)_ – number of forecast days to consider (default: `7`).
- `timeOfDay` _(optional, string)_ – time of day to derive temperature from (default: `"14:00"`).

#### Sample Response

```json
[
  {
    "name": "Bandarban",
    "latitude": 22.1953,
    "longitude": 92.2185,
    "averageTemperatureAt2Pm": 26.4,
    "averagePm25": 7.2,
    "rank": 1
  },
  {
    "name": "Rangamati",
    "latitude": 22.7324,
    "longitude": 92.2985,
    "averageTemperatureAt2Pm": 26.9,
    "averagePm25": 8.1,
    "rank": 2
  }
  // ... up to 10 items
]
```

---

### 2. Travel Recommendation

- **HTTP**: `POST`
- **Route**: `/api/recommendation/travel`  
  (See `RecommendationController`)

#### Request Body (example)

```json
{
  "currentLatitude": 23.7808,
  "currentLongitude": 90.2792,
  "destinationDistrict": "Bandarban",
  "travelDate": "2026-01-05"
}
```

#### Response (example)

```json
{
  "status": "Recommended",
  "reason": "Your destination is 3°C cooler and has significantly better air quality. Enjoy your trip!",
  "current": {
    "temperatureAt2Pm": 31.2,
    "pm25": 45.0
  },
  "destination": {
    "district": "Bandarban",
    "temperatureAt2Pm": 28.2,
    "pm25": 15.0
  }
}
```

Or:

```json
{
  "status": "Not Recommended",
  "reason": "Your destination is hotter and has worse air quality than your current location. It’s better to stay where you are.",
  "current": {
    "temperatureAt2Pm": 29.0,
    "pm25": 20.0
  },
  "destination": {
    "district": "Chattogram",
    "temperatureAt2Pm": 32.0,
    "pm25": 55.0
  }
}
```

---

## Testing

From the repo root:

```bash
dotnet test
```

This will run tests from the `Strativ.Api.Tests` project, including:

- **Service-level tests** for:
  - Aggregation and ranking logic of coolest + cleanest districts.
  - Travel recommendation decision logic.
- **Integration-style tests** (if added) using sample responses in `Strativ.Api.Tests/TestData`.

---

## Performance Considerations

To respect the **≤ 500 ms** response time requirement per request:

- **Caching**:  
  District-level forecast and air-quality data can be cached for a reasonable duration (60 minutes), reflecting the external API update frequency.
- **Background warmup**:  
  A background service pre-fetches and refreshes top-district data so the main endpoint primarily serves from cache.

You can tune cache duration, batch sizes, and timeout values in the relevant service or configuration classes.

---

## Why not Redis (Distributed Cache)?

For this assignment the API uses **in-memory caching** (`IMemoryCache`) instead of a distributed cache such as **Redis**:

- **Simplicity & focus**:  
  The goal is to demonstrate the domain logic (ranking districts, travel recommendation) and sensible use of caching, not full production infrastructure.
- **Single-instance assumption**:  
  The solution is intended to run as a single service instance (locally or via the provided Docker setup), where in-memory cache is sufficient.
- **No extra infrastructure requirements**:  
  Avoids adding Redis as an external dependency that reviewers would need to install/configure just to run the project.

In a real production deployment, a **distributed cache** such as Redis would be a good choice when:

- The API is scaled to **multiple instances** and cache needs to be shared across them.
- You want **more control over eviction**, persistence, or cache warming across restarts.
- You expect **high traffic** and want to reduce repeated calls to external APIs across the whole cluster, not just per instance.

The current implementation is structured so that switching from `IMemoryCache` to a distributed cache (e.g., `IDistributedCache` or a Redis-backed provider) would primarily be a configuration and wiring change.

---

## How to Extend

Some ideas for future improvements:

- Add authentication/authorization if needed.
- Introduce rate-limiting for public deployment.
- Add localization for reason messages.
- Persist cached data to a distributed cache (Redis) for scaling out.

---

## Notes for Reviewers

- This project is intentionally kept focused on:
  - Correctness of **domain logic**.
  - Sensible **performance** and **caching** choices.
  - Clean **separation of concerns** between controllers, services, and data access.
- Commit history shows the incremental approach:
  - Bootstrapping API.
  - Integrating external services.
  - Adding domain logic, caching, tests, and Docker support.

If you have any questions while reviewing, feel free to reach out.
