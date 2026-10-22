namespace ZFome.Domain.Exceptions;

/// <summary>
/// Representa as exceções de domínio da aplicação.
/// </summary>
/// <param name="message"></param>
public class DomainException(string message) : Exception(message);