using Microsoft.AspNetCore.Identity;
using ZFome.Application.Abstractions.Identity;

namespace ZFome.Infrastructure.Identity;

/// <summary>
/// Implementação do serviço de hashing de senhas usando o PasswordHasher do ASP.NET Core Identity.
/// </summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    // Instância do PasswordHasher do ASP.NET Core Identity usada para gerar e verificar hashes de senha.
    private readonly PasswordHasher<object> hasher = new();

    // Objeto usado como parâmetro para o PasswordHasher, pois ele não depende de um usuário específico.
    private readonly object user = new();

    // Gera um hash da senha fornecida usando o PasswordHasher do ASP.NET Core Identity.
    public string Hash(string password) => hasher.HashPassword(user, password);

    // Verifica se a senha fornecida corresponde ao hash armazenado usando o PasswordHasher do ASP.NET Core Identity.
    public bool Verify(string passwordHash, string password) =>
        hasher.VerifyHashedPassword(user, passwordHash, password) != PasswordVerificationResult.Failed;
}