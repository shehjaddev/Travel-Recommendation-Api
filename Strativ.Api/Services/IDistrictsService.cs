using Strativ.Api.Models;

namespace Strativ.Api.Services;

public interface IDistrictsService
{
    Task<List<District>> GetDistrictsAsync();
}