using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Domain.Repositories;
using UrlShortener.Infrastructure.Repositories;

namespace UrlShortener.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IShortUrlRepository, ShortUrlRepository>();
        services.AddScoped<IClickEventRepository, ClickEventRepository>();
        return services;
    }
}