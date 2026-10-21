using Microsoft.EntityFrameworkCore;
using ZFome.Infrastructure.Persistence.Write.Configurations;

namespace ZFome.Infrastructure.Persistence.Write;

/// <summary>
/// Representa o contexto do banco de dados para operações de escrita no ZFome, utilizando o Entity Framework Core.
/// </summary>
/// <param name="options"></param>
public sealed class ZFomeDbContext(DbContextOptions<ZFomeDbContext> options) : DbContext(options)
{
    public DbSet<ZFome.Domain.Tenancy.Tenant> Tenants => Set<ZFome.Domain.Tenancy.Tenant>();
    public DbSet<ZFome.Domain.Tenancy.Establishment> Establishments => Set<ZFome.Domain.Tenancy.Establishment>();
    public DbSet<ZFome.Domain.Identity.AdministrativeUser> AdministrativeUsers => Set<ZFome.Domain.Identity.AdministrativeUser>();
    public DbSet<ZFome.Domain.Identity.PlatformUser> PlatformUsers => Set<ZFome.Domain.Identity.PlatformUser>();
    public DbSet<ZFome.Domain.Identity.AuthenticationChallenge> AuthenticationChallenges => Set<ZFome.Domain.Identity.AuthenticationChallenge>();
    public DbSet<ZFome.Domain.Identity.AdministrativeSession> AdministrativeSessions => Set<ZFome.Domain.Identity.AdministrativeSession>();
    public DbSet<ZFome.Domain.Operations.ServiceTable> ServiceTables => Set<ZFome.Domain.Operations.ServiceTable>();
    public DbSet<ZFome.Domain.Operations.BusinessHours> BusinessHours => Set<ZFome.Domain.Operations.BusinessHours>();
    public DbSet<ZFome.Domain.Operations.ServiceScheduleException> ServiceScheduleExceptions => Set<ZFome.Domain.Operations.ServiceScheduleException>();
    public DbSet<ZFome.Domain.Operations.ServicePause> ServicePauses => Set<ZFome.Domain.Operations.ServicePause>();
    public DbSet<ZFome.Domain.Ordering.OrderSchedulingPolicy> OrderSchedulingPolicies => Set<ZFome.Domain.Ordering.OrderSchedulingPolicy>();
    public DbSet<ZFome.Domain.Catalog.Category> Categories => Set<ZFome.Domain.Catalog.Category>();
    public DbSet<ZFome.Domain.Catalog.Product> Products => Set<ZFome.Domain.Catalog.Product>();
    public DbSet<ZFome.Domain.Catalog.Additional> Additionals => Set<ZFome.Domain.Catalog.Additional>();
    public DbSet<ZFome.Domain.Catalog.AdditionalGroup> AdditionalGroups => Set<ZFome.Domain.Catalog.AdditionalGroup>();
    public DbSet<ZFome.Domain.Catalog.OfferUnavailability> OfferUnavailabilities => Set<ZFome.Domain.Catalog.OfferUnavailability>();
    public DbSet<ZFome.Domain.Customers.Customer> Customers => Set<ZFome.Domain.Customers.Customer>();
    public DbSet<ZFome.Domain.Ordering.Order> Orders => Set<ZFome.Domain.Ordering.Order>();
    public DbSet<ZFome.Domain.Delivery.DeliveryRegion> DeliveryRegions => Set<ZFome.Domain.Delivery.DeliveryRegion>();
    public DbSet<ZFome.Domain.Promotions.Coupon> Coupons => Set<ZFome.Domain.Promotions.Coupon>();
    public DbSet<ZFome.Domain.Payments.PaymentMethod> PaymentMethods => Set<ZFome.Domain.Payments.PaymentMethod>();
    public DbSet<ZFome.Domain.Payments.Payment> Payments => Set<ZFome.Domain.Payments.Payment>();
    public DbSet<ZFome.Domain.Payments.PaymentIdempotency> PaymentIdempotencies => Set<ZFome.Domain.Payments.PaymentIdempotency>();
    public DbSet<ZFome.Domain.Ordering.PublicOrderRequest> PublicOrderRequests => Set<ZFome.Domain.Ordering.PublicOrderRequest>();
    public DbSet<ZFome.Domain.Reviews.Review> Reviews => Set<ZFome.Domain.Reviews.Review>();
    public DbSet<ZFome.Domain.Reviews.ReviewRequest> ReviewRequests => Set<ZFome.Domain.Reviews.ReviewRequest>();
    public DbSet<ZFome.Domain.Reviews.ReviewModerationHistory> ReviewModerationHistories => Set<ZFome.Domain.Reviews.ReviewModerationHistory>();
    internal DbSet<ReviewRatingSummaryRecord> ReviewRatingSummaries => Set<ReviewRatingSummaryRecord>();
    internal DbSet<OutboxMessageRecord> OutboxMessages => Set<OutboxMessageRecord>();
    internal DbSet<OutboxConsumerReceiptRecord> OutboxConsumerReceipts => Set<OutboxConsumerReceiptRecord>();
    internal DbSet<NotificationTemplateRecord> NotificationTemplates => Set<NotificationTemplateRecord>();
    internal DbSet<NotificationConsentRecord> NotificationConsents => Set<NotificationConsentRecord>();
    internal DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();
    internal DbSet<NotificationAttemptRecord> NotificationAttempts => Set<NotificationAttemptRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ZFomeDbContext).Assembly);
        modelBuilder.ApplyTenantIndexes();
        base.OnModelCreating(modelBuilder);
    }
}
