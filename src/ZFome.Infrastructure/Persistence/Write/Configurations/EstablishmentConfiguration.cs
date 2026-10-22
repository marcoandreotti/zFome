using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZFome.Domain.Tenancy;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

/// <summary>
/// Classe de configuração para a entidade Establishment, definindo o mapeamento entre a classe e a tabela do banco de dados, incluindo chaves primárias, propriedades, índices e relacionamentos.
/// </summary>
internal sealed class EstablishmentConfiguration : IEntityTypeConfiguration<Establishment>
{
    public void Configure(EntityTypeBuilder<Establishment> builder)
    {
        builder.ToTable("establishment", DatabaseSchemas.Tenancy);
        builder.HasKey(establishment => establishment.Id).HasName("pk_establishment");
        builder.Property(establishment => establishment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(establishment => establishment.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(establishment => establishment.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(100).HasDefaultValue("America/Sao_Paulo").IsRequired();
        builder.Property(establishment => establishment.TradeName).HasColumnName("trade_name").HasMaxLength(150).IsRequired();
        builder.Property(establishment => establishment.Slug)
            .HasColumnName("slug")
            .HasMaxLength(100)
            .HasConversion(slug => slug.Value, value => new Slug(value))
            .IsRequired();
        builder.Property(establishment => establishment.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(establishment => establishment.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(establishment => establishment.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(establishment => establishment.OnboardingCompletedAt).HasColumnName("onboarding_completed_at");
        builder.HasIndex(establishment => establishment.Slug).IsUnique().HasDatabaseName("ux_establishment_slug");
        builder.HasAlternateKey(establishment => new { establishment.TenantId, establishment.Id });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(establishment => establishment.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(establishment => establishment.Theme, theme =>
        {
            theme.ToTable("establishment_theme", DatabaseSchemas.Tenancy);
            theme.WithOwner().HasForeignKey("establishment_id");
            theme.Property(value => value.PrimaryColor).HasColumnName("primary_color").HasMaxLength(20);
            theme.Property(value => value.SecondaryColor).HasColumnName("secondary_color").HasMaxLength(20);
            theme.Property(value => value.BackgroundColor).HasColumnName("background_color").HasMaxLength(20);
            theme.Property(value => value.TextColor).HasColumnName("text_color").HasMaxLength(20);
            theme.Property(value => value.FontFamily).HasColumnName("font_family").HasMaxLength(100);
            theme.Property(value => value.LogoUrl).HasColumnName("logo_url").HasMaxLength(500);
            theme.Property(value => value.FaviconUrl).HasColumnName("favicon_url").HasMaxLength(500);
        });
    }
}
