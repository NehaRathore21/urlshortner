namespace UrlShortener.Service.Contracts;

/// <summary>Represents a managed short URL.</summary>
public sealed record ShortUrlResponse(
    long Id,
    string Code,
    string OriginalUrl,
    string ShortUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    bool IsActive,
    long ClickCount);