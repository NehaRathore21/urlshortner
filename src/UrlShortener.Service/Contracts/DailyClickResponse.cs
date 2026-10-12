namespace UrlShortener.Service.Contracts;

/// <summary>Represents the number of clicks recorded for one UTC date.</summary>
public sealed record DailyClickResponse(DateOnly DateUtc, long Clicks);