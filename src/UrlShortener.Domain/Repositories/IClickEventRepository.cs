using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Models;

namespace UrlShortener.Domain.Repositories;

public interface IClickEventRepository
{
    Task AddAsync(ClickEvent clickEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyClickCount>> GetDailyCountsAsync(
        long shortUrlId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<long> GetTotalCountAsync(long shortUrlId, CancellationToken cancellationToken);
}