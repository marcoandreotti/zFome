using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Identity;
using ZFome.Domain.Identity;

namespace ZFome.Infrastructure.Identity;

public sealed class PlatformBootstrapOptions
{
    public const string SectionName = "PlatformBootstrap";
    public string Email { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
}

public sealed class PlatformBootstrapper(IAuthenticationRepository repository, IPasswordHasher passwords, IOptions<PlatformBootstrapOptions> options, TimeProvider clock)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        if (await repository.AnyPlatformUserAsync(ct))
        {
            return;
        }

        var value = options.Value;
        if (string.IsNullOrWhiteSpace(value.Email) || value.TemporaryPassword.Length < PasswordPolicy.MinimumLength)
        {
            throw new InvalidOperationException("Platform bootstrap secrets are required until the first platform user is created.");
        }

        var user = PlatformUser.Bootstrap(
            new Email(value.Email),
            passwords.Hash(value.TemporaryPassword),
            clock.GetUtcNow());
        await repository.AddPlatformUserAsync(user, ct);

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Outra instância concluiu o mesmo bootstrap entre a consulta e a gravação.
        }
    }
}
