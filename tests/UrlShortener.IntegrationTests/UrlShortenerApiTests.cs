using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using UrlShortener.Data;
using UrlShortener.Service.Contracts;

namespace UrlShortener.IntegrationTests;

public sealed class UrlShortenerApiTests
{
    [Fact]
    public async Task CreateDetailsRedirectAndAnalytics_WorkTogether()
    {
        await using var postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("urlshortener_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await postgres.StartAsync();

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = postgres.GetConnectionString()
                })));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<UrlShortenerDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var createResponse = await client.PostAsJsonAsync("/api/urls", new
        {
            url = "https://example.com/products/mobile"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ShortUrlResponse>();
        Assert.NotNull(created);
        Assert.Equal("https://example.com/products/mobile", created.OriginalUrl);
        Assert.Equal(0, created.ClickCount);

        using var detailsResponse = await client.GetAsync($"/api/urls/{created.Code}");
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        using var redirectResponse = await client.GetAsync($"/{created.Code}");
        Assert.Equal(HttpStatusCode.Redirect, redirectResponse.StatusCode);
        Assert.Equal("https://example.com/products/mobile", redirectResponse.Headers.Location?.ToString());

        using var analyticsResponse = await client.GetAsync($"/api/urls/{created.Code}/analytics");
        Assert.Equal(HttpStatusCode.OK, analyticsResponse.StatusCode);
        var analytics = await analyticsResponse.Content.ReadFromJsonAsync<UrlAnalyticsResponse>();
        Assert.NotNull(analytics);
        Assert.Equal(1, analytics.TotalClicks);
        Assert.Single(analytics.DailyClicks);
        Assert.Equal(1, analytics.DailyClicks[0].Clicks);
    }

    [Fact]
    public async Task Create_RejectsUnsupportedUrlScheme()
    {
        await using var postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("urlshortener_validation_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await postgres.StartAsync();

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = postgres.GetConnectionString()
                })));

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/urls", new { url = "javascript:alert(1)" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}