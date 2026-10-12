using UrlShortener.Service.Contracts;

namespace UrlShortener.Service.Services;

public interface IUrlShortenerService
{
    Task<ShortUrlResponse> CreateAsync(string destinationUrl, CancellationToken cancellationToken);

    Task<ShortUrlResponse?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShortUrlResponse>> ListAsync(CancellationToken cancellationToken);

    Task<UrlAnalyticsResponse?> GetAnalyticsAsync(
        string code,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<string?> ResolveRedirectAsync(string code, CancellationToken cancellationToken);
}