using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZFome.Domain.Identity;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

/// <summary>
/// Classe de configuração para a entidade AdministrativeUser, definindo o mapeamento para o banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class AdministrativeUserConfiguration : IEntityTypeConfiguration<AdministrativeUser>
{
    public void Configure(EntityTypeBuilder<AdministrativeUser> builder)
    {
        builder.ToTable("administrative_user", DatabaseSchemas.Identity);
        builder.HasKey(user => user.Id);
        builder.HasAlternateKey(user => new { user.TenantId, user.Id });
        builder.Property(user => user.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(user => user.TenantId).HasColumnName("tenant_id");
        builder.Property(user => user.Name).HasColumnName("name").HasMaxLength(150);
        builder.Property(user => user.Email).HasColumnName("email").HasMaxLength(150)
            .HasConversion(email => email.Value, value => new Email(value));
        builder.Property(user => user.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(150);
        builder.Property(user => user.PasswordHash).HasColumnName("password_hash").HasMaxLength(500);
        builder.Property(user => user.IsActive).HasColumnName("is_active");
        builder.Property(user => user.LastAccessAt).HasColumnName("last_access_at").HasColumnType("timestamp with time zone");
        builder.Property(user => user.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(user => user.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(user => new { user.TenantId, user.NormalizedEmail }).IsUnique().HasDatabaseName("ux_administrative_user_tenant_email");
        builder.HasOne<ZFome.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(user => user.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(user => user.RoleMemberships, roles =>
        {
            roles.ToTable("administrative_user_role", DatabaseSchemas.Identity);
            roles.WithOwner().HasForeignKey(role => role.UserId);
            roles.HasKey(role => new { role.UserId, role.Role });
            roles.Property(role => role.UserId).HasColumnName("user_id");
            roles.Property(role => role.Role).HasColumnName("role_id").HasConversion<short>();
            roles.HasOne<AdministrativeRoleDefinition>().WithMany().HasForeignKey(role => role.Role).OnDelete(DeleteBehavior.Restrict);
        });
        builder.OwnsMany(user => user.EstablishmentAccesses, accesses =>
        {
            accesses.ToTable("user_establishment_access", DatabaseSchemas.Identity);
            accesses.WithOwner()
                .HasForeignKey(access => new { access.TenantId, access.UserId })
                .HasPrincipalKey(user => new { user.TenantId, user.Id });
            accesses.HasKey(access => new { access.UserId, access.EstablishmentId });
            accesses.Property(access => access.UserId).HasColumnName("user_id");
            accesses.Property(access => access.TenantId).HasColumnName("tenant_id");
            accesses.Property(access => access.EstablishmentId).HasColumnName("establishment_id");
            accesses.Property(access => access.IsActive).HasColumnName("is_active");
            accesses.Property(access => access.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            accesses.Property(access => access.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");
            accesses.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany()
                .HasForeignKey(access => new { access.TenantId, access.EstablishmentId })
                .HasPrincipalKey(establishment => new { establishment.TenantId, establishment.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

/// <summary>
/// Representa a definição de um papel administrativo, incluindo seu identificador, código e nome, utilizado para gerenciar permissões e funções administrativas no sistema.
/// </summary>
internal sealed class AdministrativeRoleDefinition
{
    public AdministrativeRole Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Classe de configuração para a entidade AdministrativeRoleDefinition, definindo o mapeamento para o banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class AdministrativeRoleDefinitionConfiguration : IEntityTypeConfiguration<AdministrativeRoleDefinition>
{
    public void Configure(EntityTypeBuilder<AdministrativeRoleDefinition> builder)
    {
        builder.ToTable("administrative_role", DatabaseSchemas.Identity);
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).HasColumnName("id").HasConversion<short>().ValueGeneratedNever();
        builder.Property(role => role.Code).HasColumnName("code").HasMaxLength(50);
        builder.Property(role => role.Name).HasColumnName("name").HasMaxLength(100);
        builder.HasIndex(role => role.Code).IsUnique();
        builder.HasData(Enum.GetValues<AdministrativeRole>().Select(role => new AdministrativeRoleDefinition
        { Id = role, Code = role.ToString().ToUpperInvariant(), Name = role.ToString() }));
    }
}