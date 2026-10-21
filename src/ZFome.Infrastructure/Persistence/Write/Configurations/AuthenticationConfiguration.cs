using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZFome.Domain.Identity;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

internal sealed class PlatformUserConfiguration : IEntityTypeConfiguration<PlatformUser>
{
    public void Configure(EntityTypeBuilder<PlatformUser> b)
    { b.ToTable("platform_user", DatabaseSchemas.Identity); b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); b.Property(x => x.Email).HasColumnName("email").HasMaxLength(150).HasConversion(x => x.Value, x => new Email(x)); b.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(150); b.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(500); b.Property(x => x.IsActive).HasColumnName("is_active"); b.Property(x => x.PasswordChangeRequired).HasColumnName("password_change_required"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.UpdatedAt).HasColumnName("updated_at"); b.HasIndex(x => x.NormalizedEmail).IsUnique(); }
}
internal sealed class AuthenticationChallengeConfiguration : IEntityTypeConfiguration<AuthenticationChallenge>
{
    public void Configure(EntityTypeBuilder<AuthenticationChallenge> b)
    { b.ToTable("authentication_challenge", DatabaseSchemas.Identity); b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); b.Property(x => x.IdentityType).HasColumnName("identity_type").HasConversion<short>(); b.Property(x => x.IdentityId).HasColumnName("identity_id"); b.Property(x => x.TenantId).HasColumnName("tenant_id"); b.Property(x => x.CodeHash).HasColumnName("code_hash").HasMaxLength(64); b.Property(x => x.OriginHash).HasColumnName("origin_hash").HasMaxLength(64); b.Property(x => x.FailedAttempts).HasColumnName("failed_attempts"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.ExpiresAt).HasColumnName("expires_at"); b.Property(x => x.ConsumedAt).HasColumnName("consumed_at"); b.HasIndex(x => new { x.OriginHash, x.CreatedAt }); }
}
internal sealed class AdministrativeSessionConfiguration : IEntityTypeConfiguration<AdministrativeSession>
{
    public void Configure(EntityTypeBuilder<AdministrativeSession> b)
    { b.ToTable("administrative_session", DatabaseSchemas.Identity); b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); b.Property(x => x.FamilyId).HasColumnName("family_id"); b.Property(x => x.IdentityType).HasColumnName("identity_type").HasConversion<short>(); b.Property(x => x.IdentityId).HasColumnName("identity_id"); b.Property(x => x.TenantId).HasColumnName("tenant_id"); b.Property(x => x.AccessTokenHash).HasColumnName("access_token_hash").HasMaxLength(64); b.Property(x => x.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(64); b.Property(x => x.CsrfTokenHash).HasColumnName("csrf_token_hash").HasMaxLength(64); b.Property(x => x.PasswordChangeRequired).HasColumnName("password_change_required"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.AccessExpiresAt).HasColumnName("access_expires_at"); b.Property(x => x.RefreshExpiresAt).HasColumnName("refresh_expires_at"); b.Property(x => x.RevokedAt).HasColumnName("revoked_at"); b.HasIndex(x => x.AccessTokenHash).IsUnique(); b.HasIndex(x => x.RefreshTokenHash).IsUnique(); b.HasIndex(x => x.FamilyId); }
}
