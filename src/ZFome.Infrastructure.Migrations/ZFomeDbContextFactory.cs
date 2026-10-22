using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ZFome.Infrastructure.Persistence.Write;

namespace ZFome.Infrastructure.Migrations;

public sealed class ZFomeDbContextFactory : IDesignTimeDbContextFactory<ZFomeDbContext>
{
    public ZFomeDbContext CreateDbContext(string[] args)
    {
        var connectionString = GetConnectionString(args);
        var options = new DbContextOptionsBuilder<ZFomeDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ZFomeDbContextFactory).Assembly.FullName))
            .Options;

        return new ZFomeDbContext(options);
    }

    private static string GetConnectionString(string[] args)
    {
        var index = Array.IndexOf(args, "--connection");
        if (index >= 0 && index < args.Length - 1 && !string.IsNullOrWhiteSpace(args[index + 1]))
        {
            return args[index + 1];
        }

        var connectionString = Environment.GetEnvironmentVariable("Database__ConnectionString");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        throw new InvalidOperationException(
            "Pass the PostgreSQL connection string after --connection or set Database__ConnectionString.");
    }
}
