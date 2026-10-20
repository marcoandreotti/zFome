using Microsoft.EntityFrameworkCore;
using OrderHub.Infrastructure.Persistence.Write.Configurations;

namespace OrderHub.Infrastructure.Persistence.Write;

/// <summary>
/// Representa o contexto do banco de dados para operações de escrita no OrderHub, utilizando o Entity Framework Core.
/// </summary>
/// <param name="options"></param>
public sealed class OrderHubDbContext(DbContextOptions<OrderHubDbContext> options) : DbContext(options)
{
    public DbSet<OrderHub.Domain.Tenancy.Tenant> Tenants => Set<OrderHub.Domain.Tenancy.Tenant>();
    public DbSet<OrderHub.Domain.Tenancy.Establishment> Establishments => Set<OrderHub.Domain.Tenancy.Establishment>();
    public DbSet<OrderHub.Domain.Identity.AdministrativeUser> AdministrativeUsers => Set<OrderHub.Domain.Identity.AdministrativeUser>();
    public DbSet<OrderHub.Domain.Identity.PlatformUser> PlatformUsers => Set<OrderHub.Domain.Identity.PlatformUser>();
    public DbSet<OrderHub.Domain.Identity.AuthenticationChallenge> AuthenticationChallenges => Set<OrderHub.Domain.Identity.AuthenticationChallenge>();
    public DbSet<OrderHub.Domain.Identity.AdministrativeSession> AdministrativeSessions => Set<OrderHub.Domain.Identity.AdministrativeSession>();
    public DbSet<OrderHub.Domain.Operations.ServiceTable> ServiceTables => Set<OrderHub.Domain.Operations.ServiceTable>();
    public DbSet<OrderHub.Domain.Operations.BusinessHours> BusinessHours => Set<OrderHub.Domain.Operations.BusinessHours>();
    public DbSet<OrderHub.Domain.Operations.ServiceScheduleException> ServiceScheduleExceptions => Set<OrderHub.Domain.Operations.ServiceScheduleException>();
    public DbSet<OrderHub.Domain.Operations.ServicePause> ServicePauses => Set<OrderHub.Domain.Operations.ServicePause>();
    public DbSet<OrderHub.Domain.Ordering.OrderSchedulingPolicy> OrderSchedulingPolicies => Set<OrderHub.Domain.Ordering.OrderSchedulingPolicy>();
    public DbSet<OrderHub.Domain.Catalog.Category> Categories => Set<OrderHub.Domain.Catalog.Category>();
    public DbSet<OrderHub.Domain.Catalog.Product> Products => Set<OrderHub.Domain.Catalog.Product>();
    public DbSet<OrderHub.Domain.Catalog.Additional> Additionals => Set<OrderHub.Domain.Catalog.Additional>();
    public DbSet<OrderHub.Domain.Catalog.AdditionalGroup> AdditionalGroups => Set<OrderHub.Domain.Catalog.AdditionalGroup>();
    public DbSet<OrderHub.Domain.Catalog.OfferUnavailability> OfferUnavailabilities => Set<OrderHub.Domain.Catalog.OfferUnavailability>();
    public DbSet<OrderHub.Domain.Customers.Customer> Customers => Set<OrderHub.Domain.Customers.Customer>();
    public DbSet<OrderHub.Domain.Ordering.Order> Orders => Set<OrderHub.Domain.Ordering.Order>();
    public DbSet<OrderHub.Domain.Delivery.DeliveryRegion> DeliveryRegions => Set<OrderHub.Domain.Delivery.DeliveryRegion>();
    public DbSet<OrderHub.Domain.Promotions.Coupon> Coupons => Set<OrderHub.Domain.Promotions.Coupon>();
    public DbSet<OrderHub.Domain.Payments.PaymentMethod> PaymentMethods => Set<OrderHub.Domain.Payments.PaymentMethod>();
    public DbSet<OrderHub.Domain.Payments.Payment> Payments => Set<OrderHub.Domain.Payments.Payment>();
    public DbSet<OrderHub.Domain.Payments.PaymentIdempotency> PaymentIdempotencies => Set<OrderHub.Domain.Payments.PaymentIdempotency>();
    public DbSet<OrderHub.Domain.Ordering.PublicOrderRequest> PublicOrderRequests => Set<OrderHub.Domain.Ordering.PublicOrderRequest>();
    public DbSet<OrderHub.Domain.Reviews.Review> Reviews => Set<OrderHub.Domain.Reviews.Review>();
    public DbSet<OrderHub.Domain.Reviews.ReviewRequest> ReviewRequests => Set<OrderHub.Domain.Reviews.ReviewRequest>();
    public DbSet<OrderHub.Domain.Reviews.ReviewModerationHistory> ReviewModerationHistories => Set<OrderHub.Domain.Reviews.ReviewModerationHistory>();
    internal DbSet<ReviewRatingSummaryRecord> ReviewRatingSummaries => Set<ReviewRatingSummaryRecord>();
    internal DbSet<OutboxMessageRecord> OutboxMessages => Set<OutboxMessageRecord>();
    internal DbSet<OutboxConsumerReceiptRecord> OutboxConsumerReceipts => Set<OutboxConsumerReceiptRecord>();
    internal DbSet<NotificationTemplateRecord> NotificationTemplates => Set<NotificationTemplateRecord>();
    internal DbSet<NotificationConsentRecord> NotificationConsents => Set<NotificationConsentRecord>();
    internal DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();
    internal DbSet<NotificationAttemptRecord> NotificationAttempts => Set<NotificationAttemptRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderHubDbContext).Assembly);
        modelBuilder.ApplyTenantIndexes();
        base.OnModelCreating(modelBuilder);
    }
}
