using Microsoft.EntityFrameworkCore;
using ZFome.Domain.SharedKernel;

namespace ZFome.Infrastructure.Persistence.Write;

/// <summary>
/// Classe de convenções para aplicar índices relacionados a entidades com escopo de locatário (tenant) no modelo do Entity Framework Core.
/// </summary>
internal static class TenantModelConventions
{
    public static void ApplyTenantIndexes(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(type => typeof(ITenantScopedEntity).IsAssignableFrom(type.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).HasIndex(nameof(ITenantScopedEntity.TenantId));
        }
    }
}