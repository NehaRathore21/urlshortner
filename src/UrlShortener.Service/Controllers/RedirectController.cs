using Microsoft.AspNetCore.Mvc;
using UrlShortener.Service.Services;

namespace UrlShortener.Service.Controllers;

[ApiController]
[Route("")]
public sealed class RedirectController(IUrlShortenerService urlShortenerService) : ControllerBase
{
    [HttpGet("{code:length(8)}")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RedirectToDestination(string code, CancellationToken cancellationToken)
    {
        var destination = await urlShortenerService.ResolveRedirectAsync(code, cancellationToken);
        return destination is null ? NotFound() : Redirect(destination);
    }
}