namespace UrlShortener.Domain.Entities;

public sealed class ClickEvent
{
    private ClickEvent()
    {
    }

    private ClickEvent(long shortUrlId, DateTimeOffset occurredAtUtc)
    {
        ShortUrlId = shortUrlId;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public long Id { get; private set; }

    public long ShortUrlId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public ShortUrl ShortUrl { get; private set; } = null!;

    public static ClickEvent Create(long shortUrlId, DateTimeOffset occurredAtUtc) =>
        new(shortUrlId, occurredAtUtc);
}