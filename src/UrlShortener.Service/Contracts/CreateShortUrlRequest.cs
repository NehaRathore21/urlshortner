using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Service.Contracts;

/// <summary>Represents a request to create a short URL.</summary>
public sealed record CreateShortUrlRequest
{
    /// <summary>The absolute HTTP or HTTPS destination URL.</summary>
    [Required, MaxLength(2048)]
    public required string Url { get; init; }
}