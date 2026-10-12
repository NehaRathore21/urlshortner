namespace UrlShortener.Domain.Exceptions;

public sealed class ShortCodeConflictException(Exception innerException)
    : Exception("The generated short code already exists.", innerException);