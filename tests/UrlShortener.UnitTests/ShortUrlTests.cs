using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Exceptions;
using UrlShortener.Domain.Models;
using UrlShortener.Domain.Repositories;
using UrlShortener.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UrlShortener.UnitTests;

public sealed class ShortUrlTests
{
    [Fact]
    public async Task CreateAsync_RetriesWhenGeneratedCodeConflicts()
    {
        var repository = new ConflictOnceShortUrlRepository();
        var service = new UrlShortenerService(
            repository,
            new EmptyClickEventRepository(),
            Options.Create(new UrlShortenerOptions { PublicBaseUrl = "https://short.example" }),
            TimeProvider.System,
            NullLogger<UrlShortenerService>.Instance);

        var result = await service.CreateAsync("https://example.com", CancellationToken.None);

        Assert.Equal(2, repository.Attempts);
        Assert.Equal("https://short.example/" + result.Code, result.ShortUrl);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://user:password@example.com/path")]
    [InlineData("example.com/path")]
    public void Create_RejectsUnsupportedOrUnsafeDestinations(string url)
    {
        Assert.Throws<ArgumentException>(() => ShortUrl.Create(url, "abc12345", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_NormalizesDestinationAndTimestampsToUtc()
    {
        var createdAt = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.FromHours(2));
        var expiresAt = createdAt.AddDays(1);

        var shortUrl = ShortUrl.Create("https://example.com/path", "abc12345", createdAt, expiresAt);

        Assert.Equal("https://example.com/path", shortUrl.OriginalUrl);
        Assert.Equal(TimeSpan.Zero, shortUrl.CreatedAtUtc.Offset);
        Assert.Equal(TimeSpan.Zero, shortUrl.ExpiresAtUtc!.Value.Offset);
        Assert.True(shortUrl.IsActive);
    }

    [Fact]
    public void IsAvailableAt_ReturnsFalseAtExpiryBoundary()
    {
        var now = DateTimeOffset.UtcNow;
        var shortUrl = ShortUrl.Create("https://example.com", "abc12345", now, now.AddMinutes(5));

        Assert.True(shortUrl.IsAvailableAt(now.AddMinutes(4)));
        Assert.False(shortUrl.IsAvailableAt(now.AddMinutes(5)));
    }

    private sealed class ConflictOnceShortUrlRepository : IShortUrlRepository
    {
        public int Attempts { get; private set; }

        public Task AddAsync(ShortUrl shortUrl, CancellationToken cancellationToken)
        {
            Attempts++;
            if (Attempts == 1)
            {
                throw new ShortCodeConflictException(new InvalidOperationException("Unique code conflict."));
            }

            return Task.CompletedTask;
        }

        public Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult<ShortUrl?>(null);

        public Task<IReadOnlyList<ShortUrlSummary>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ShortUrlSummary>>(Array.Empty<ShortUrlSummary>());
    }

    private sealed class EmptyClickEventRepository : IClickEventRepository
    {
        public Task AddAsync(ClickEvent clickEvent, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<DailyClickCount>> GetDailyCountsAsync(
            long shortUrlId,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DailyClickCount>>(Array.Empty<DailyClickCount>());

        public Task<long> GetTotalCountAsync(long shortUrlId, CancellationToken cancellationToken) =>
            Task.FromResult(0L);
    }
}