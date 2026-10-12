using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Exceptions;
using UrlShortener.Domain.Repositories;
using UrlShortener.Service.Contracts;

namespace UrlShortener.Service.Services;

public sealed class UrlShortenerOptions
{
    public string PublicBaseUrl { get; set; } = "http://localhost:5080";
}

public sealed class InvalidDestinationUrlException(string message) : Exception(message);

public sealed class UrlShortenerService(
    IShortUrlRepository shortUrlRepository,
    IClickEventRepository clickEventRepository,
    IOptions<UrlShortenerOptions> options,
    TimeProvider timeProvider,
    ILogger<UrlShortenerService> logger) : IUrlShortenerService
{
    private const string CodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    public async Task<ShortUrlResponse> CreateAsync(string destinationUrl, CancellationToken cancellationToken)
    {
        try
        {
            if (!Uri.TryCreate(destinationUrl, UriKind.Absolute, out var destination)
                || (destination.Scheme != Uri.UriSchemeHttp && destination.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrEmpty(destination.UserInfo))
            {
                throw new InvalidDestinationUrlException("Only absolute HTTP and HTTPS URLs without embedded credentials are allowed.");
            }

            for (var attempt = 0; attempt < 3; attempt++)
            {
                var shortUrl = ShortUrl.Create(
                    destination.AbsoluteUri,
                    RandomNumberGenerator.GetString(CodeAlphabet, 8),
                    timeProvider.GetUtcNow());
                try
                {
                    await shortUrlRepository.AddAsync(shortUrl, cancellationToken);
                    return ToResponse(shortUrl, 0);
                }
                catch (ShortCodeConflictException) when (attempt < 2)
                {
                    logger.LogInformation("Retrying short code generation after a uniqueness conflict.");
                }
            }

            throw new InvalidOperationException("A unique short code could not be generated.");
        }
        catch (InvalidDestinationUrlException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create a short URL.");
            throw;
        }
    }

    public async Task<ShortUrlResponse?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            var shortUrl = await shortUrlRepository.GetByCodeAsync(code, cancellationToken);
            return shortUrl is null
                ? null
                : ToResponse(shortUrl, await clickEventRepository.GetTotalCountAsync(shortUrl.Id, cancellationToken));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to retrieve short URL details for {Code}.", code);
            throw;
        }
    }

    public async Task<IReadOnlyList<ShortUrlResponse>> ListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var links = await shortUrlRepository.ListAsync(cancellationToken);
            return links.Select(link => new ShortUrlResponse(
                link.Id,
                link.Code,
                link.OriginalUrl,
                BuildShortUrl(link.Code),
                link.CreatedAtUtc,
                link.ExpiresAtUtc,
                link.IsActive,
                link.ClickCount)).ToArray();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to list short URLs.");
            throw;
        }
    }

    public async Task<UrlAnalyticsResponse?> GetAnalyticsAsync(
        string code,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var shortUrl = await shortUrlRepository.GetByCodeAsync(code, cancellationToken);
            if (shortUrl is null)
            {
                return null;
            }

            var dailyCounts = await clickEventRepository.GetDailyCountsAsync(
                shortUrl.Id,
                fromUtc.ToUniversalTime(),
                toUtc.ToUniversalTime(),
                cancellationToken);
            var totalClicks = await clickEventRepository.GetTotalCountAsync(shortUrl.Id, cancellationToken);
            return new UrlAnalyticsResponse(
                shortUrl.Code,
                totalClicks,
                dailyCounts.Select(count => new DailyClickResponse(count.Date, count.Clicks)).ToArray());
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to retrieve analytics for short URL {Code}.", code);
            throw;
        }
    }

    public async Task<string?> ResolveRedirectAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            var shortUrl = await shortUrlRepository.GetByCodeAsync(code, cancellationToken);
            if (shortUrl is null || !shortUrl.IsAvailableAt(timeProvider.GetUtcNow()))
            {
                return null;
            }

            try
            {
                await clickEventRepository.AddAsync(
                    ClickEvent.Create(shortUrl.Id, timeProvider.GetUtcNow()),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Click event could not be recorded for {Code}; redirect will continue.", code);
            }

            return shortUrl.OriginalUrl;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to resolve short URL {Code}.", code);
            throw;
        }
    }

    private ShortUrlResponse ToResponse(ShortUrl shortUrl, long clickCount)
    {
        try
        {
            return new ShortUrlResponse(
                shortUrl.Id,
                shortUrl.Code,
                shortUrl.OriginalUrl,
                BuildShortUrl(shortUrl.Code),
                shortUrl.CreatedAtUtc,
                shortUrl.ExpiresAtUtc,
                shortUrl.IsActive,
                clickCount);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to map short URL {Code} to its response.", shortUrl.Code);
            throw;
        }
    }

    private string BuildShortUrl(string code)
    {
        try
        {
            return $"{options.Value.PublicBaseUrl.TrimEnd('/')}/{code}";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to construct a public URL for code {Code}.", code);
            throw;
        }
    }
}