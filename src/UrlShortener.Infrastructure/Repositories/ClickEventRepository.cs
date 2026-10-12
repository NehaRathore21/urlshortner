using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UrlShortener.Data;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Models;
using UrlShortener.Domain.Repositories;

namespace UrlShortener.Infrastructure.Repositories;

public sealed class ClickEventRepository(
    UrlShortenerDbContext dbContext,
    ILogger<ClickEventRepository> logger) : IClickEventRepository
{
    public async Task AddAsync(ClickEvent clickEvent, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.ClickEvents.AddAsync(clickEvent, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to persist click event for short URL {ShortUrlId}.", clickEvent.ShortUrlId);
            throw;
        }
    }

    public async Task<IReadOnlyList<DailyClickCount>> GetDailyCountsAsync(
        long shortUrlId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var groupedCounts = await dbContext.ClickEvents
                .AsNoTracking()
                .Where(clickEvent => clickEvent.ShortUrlId == shortUrlId
                    && clickEvent.OccurredAtUtc >= fromUtc
                    && clickEvent.OccurredAtUtc < toUtc)
                .GroupBy(clickEvent => new
                {
                    clickEvent.OccurredAtUtc.Year,
                    clickEvent.OccurredAtUtc.Month,
                    clickEvent.OccurredAtUtc.Day
                })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    Clicks = group.LongCount()
                })
                .OrderBy(group => group.Year)
                .ThenBy(group => group.Month)
                .ThenBy(group => group.Day)
                .ToListAsync(cancellationToken);

            return groupedCounts
                .Select(group => new DailyClickCount(new DateOnly(group.Year, group.Month, group.Day), group.Clicks))
                .ToArray();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to retrieve click analytics for short URL {ShortUrlId}.", shortUrlId);
            throw;
        }
    }

    public async Task<long> GetTotalCountAsync(long shortUrlId, CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.ClickEvents.LongCountAsync(
                clickEvent => clickEvent.ShortUrlId == shortUrlId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to count clicks for short URL {ShortUrlId}.", shortUrlId);
            throw;
        }
    }
}