using Microsoft.EntityFrameworkCore;
using ZFome.Infrastructure.Migrations;

try
{
    await using var context = new ZFomeDbContextFactory().CreateDbContext(args);
    await context.Database.MigrateAsync();
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Database migration failed: {exception.Message}");
    Environment.ExitCode = 1;
}
