namespace ZFome.Application.Exceptions;

public sealed class UnauthorizedException(string message = "Authentication is required.") : Exception(message);
