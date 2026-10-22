using System.Data.Common;
using Microsoft.Extensions.Options;
using Npgsql;
using ZFome.Application.Abstractions.Persistence;

namespace ZFome.Infrastructure.Persistence.Read;

/// <summary>
/// Representa uma fábrica de conexões de leitura para o banco de dados PostgreSQL usando Npgsql.
/// </summary>
public sealed class NpgsqlReadConnectionFactory(IOptions<DatabaseOptions> options) : IReadConnectionFactory
{
    private readonly string connectionString = options.Value.ConnectionString;

    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}