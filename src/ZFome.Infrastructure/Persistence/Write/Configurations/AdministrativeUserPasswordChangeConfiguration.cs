using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZFome.Domain.Identity;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

internal sealed class AdministrativeUserPasswordChangeConfiguration : IEntityTypeConfiguration<AdministrativeUser>
{
    public void Configure(EntityTypeBuilder<AdministrativeUser> builder) =>
        builder.Property(x => x.PasswordChangeRequired).HasColumnName("password_change_required").HasDefaultValue(false);
}
