namespace ZFome.Application.Abstractions.Identity;

/// <summary>
/// Representa um serviço de hashing de senhas que fornece métodos para gerar um hash de senha e verificar se uma senha corresponde a um hash armazenado.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}