using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UrlShortener.Data;

namespace UrlShortener.Service.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(UrlShortenerDbContext dbContext, ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            var databaseAvailable = await dbContext.Database.CanConnectAsync(cancellationToken);
            return databaseAvailable
                ? Ok(new { status = "healthy" })
                : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unhealthy" });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Health check could not connect to PostgreSQL.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unhealthy" });
        }
    }
}