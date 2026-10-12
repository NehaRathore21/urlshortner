namespace UrlShortener.Domain.Entities;

public sealed class ShortUrl
{
    private ShortUrl()
    {
    }

    public long Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string OriginalUrl { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public bool IsActive { get; private set; }

    public ICollection<ClickEvent> ClickEvents { get; private set; } = new List<ClickEvent>();

    public static ShortUrl Create(
        string originalUrl,
        string code,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? expiresAtUtc = null)
    {
        if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("A valid absolute HTTP or HTTPS URL is required.", nameof(originalUrl));
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length > 32)
        {
            throw new ArgumentException("A short code between 1 and 32 characters is required.", nameof(code));
        }

        if (expiresAtUtc is not null && expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException("Expiry must be later than creation time.", nameof(expiresAtUtc));
        }

        return new ShortUrl
        {
            Code = code,
            OriginalUrl = uri.AbsoluteUri,
            CreatedAtUtc = createdAtUtc.ToUniversalTime(),
            ExpiresAtUtc = expiresAtUtc?.ToUniversalTime(),
            IsActive = true
        };
    }

    public bool IsAvailableAt(DateTimeOffset timestampUtc) =>
        IsActive && (ExpiresAtUtc is null || ExpiresAtUtc > timestampUtc);
}