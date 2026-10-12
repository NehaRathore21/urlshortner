namespace UrlShortener.Service.Contracts;

/// <summary>Represents click analytics for a short URL.</summary>
public sealed record UrlAnalyticsResponse(
    string Code,
    long TotalClicks,
    IReadOnlyList<DailyClickResponse> DailyClicks);