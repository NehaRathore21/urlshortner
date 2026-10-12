using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using UrlShortener.Data;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Exceptions;
using UrlShortener.Domain.Models;
using UrlShortener.Domain.Repositories;

namespace UrlShortener.Infrastructure.Repositories;

public sealed class ShortUrlRepository(
    UrlShortenerDbContext dbContext,
    ILogger<ShortUrlRepository> logger) : IShortUrlRepository
{
    public async Task AddAsync(ShortUrl shortUrl, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.ShortUrls.AddAsync(shortUrl, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && postgresException.ConstraintName == "IX_short_urls_code")
        {
            dbContext.Entry(shortUrl).State = EntityState.Detached;
            logger.LogWarning(exception, "Generated short code {Code} already exists.", shortUrl.Code);
            throw new ShortCodeConflictException(exception);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to persist a short URL.");
            throw;
        }
    }

    public async Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.ShortUrls
                .AsNoTracking()
                .FirstOrDefaultAsync(shortUrl => shortUrl.Code == code, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to retrieve short URL with code {Code}.", code);
            throw;
        }
    }

    public async Task<IReadOnlyList<ShortUrlSummary>> ListAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.ShortUrls
                .AsNoTracking()
                .OrderByDescending(shortUrl => shortUrl.CreatedAtUtc)
                .Select(shortUrl => new ShortUrlSummary(
                    shortUrl.Id,
                    shortUrl.Code,
                    shortUrl.OriginalUrl,
                    shortUrl.CreatedAtUtc,
                    shortUrl.ExpiresAtUtc,
                    shortUrl.IsActive,
                    dbContext.ClickEvents.LongCount(clickEvent => clickEvent.ShortUrlId == shortUrl.Id)))
                .Take(100)
                .ToListAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to list short URLs.");
            throw;
        }
    }
}