namespace UrlShortener.Domain.Models;

public sealed record ShortUrlSummary(
    long Id,
    string Code,
    string OriginalUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    bool IsActive,
    long ClickCount);