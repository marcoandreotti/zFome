using System.Data.Common;

namespace ZFome.Application.Abstractions.Persistence;

public interface IReadConnectionFactory
{
    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
