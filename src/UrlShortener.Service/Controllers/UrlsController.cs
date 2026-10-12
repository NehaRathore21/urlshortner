using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrlShortener.Service.Contracts;
using UrlShortener.Service.Services;

namespace UrlShortener.Service.Controllers;

[ApiController]
[Route("api/urls")]
public sealed class UrlsController(IUrlShortenerService urlShortenerService) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("create-url")]
    [ProducesResponseType<ShortUrlResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShortUrlResponse>> Create(
        [FromBody] CreateShortUrlRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await urlShortenerService.CreateAsync(request.Url, cancellationToken);
            return CreatedAtAction(nameof(GetByCode), new { code = response.Code }, response);
        }
        catch (InvalidDestinationUrlException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid URL", detail: exception.Message);
        }
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ShortUrlResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ShortUrlResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await urlShortenerService.ListAsync(cancellationToken));
    }

    [HttpGet("{code}")]
    [ProducesResponseType<ShortUrlResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShortUrlResponse>> GetByCode(string code, CancellationToken cancellationToken)
    {
        var response = await urlShortenerService.GetByCodeAsync(code, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("{code}/analytics")]
    [ProducesResponseType<UrlAnalyticsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UrlAnalyticsResponse>> GetAnalytics(
        string code,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        CancellationToken cancellationToken)
    {
        var end = toUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
        var start = fromUtc?.ToUniversalTime() ?? end.AddDays(-30);
        if (start >= end || end - start > TimeSpan.FromDays(366))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid analytics range.");
        }

        var response = await urlShortenerService.GetAnalyticsAsync(code, start, end, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
}