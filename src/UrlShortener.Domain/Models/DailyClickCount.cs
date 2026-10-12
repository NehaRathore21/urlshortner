namespace UrlShortener.Domain.Models;

public sealed record DailyClickCount(DateOnly Date, long Clicks);