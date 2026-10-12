using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Models;

namespace UrlShortener.Domain.Repositories;

public interface IShortUrlRepository
{
    Task AddAsync(ShortUrl shortUrl, CancellationToken cancellationToken);

    Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShortUrlSummary>> ListAsync(CancellationToken cancellationToken);
}