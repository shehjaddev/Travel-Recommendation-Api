using System.ComponentModel.DataAnnotations;

namespace Strativ.Api.Models;

public class RecommendationRequest
{
    [Required] public double CurrentLatitude { get; set; }
    [Required] public double CurrentLongitude { get; set; }
    [Required] public string DestinationDistrict { get; set; } = string.Empty;
    [Required] public DateOnly TravelDate { get; set; }
}