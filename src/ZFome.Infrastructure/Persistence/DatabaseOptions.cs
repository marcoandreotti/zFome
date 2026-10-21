namespace ZFome.Infrastructure.Persistence;

/// <summary>
/// Representa as opções de configuração do banco de dados, incluindo a string de conexão necessária para estabelecer a comunicação com o banco de dados.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public required string ConnectionString { get; init; }
}