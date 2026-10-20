using OrderHub.Application.Onboarding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderHub.Application.Abstractions.Persistence;
using OrderHub.Infrastructure.Persistence;
using OrderHub.Infrastructure.Persistence.Read;
using OrderHub.Infrastructure.Persistence.Write;
using OrderHub.Infrastructure.Persistence.Write.Repositories;
using OrderHub.Application.Abstractions.Tenancy;
using OrderHub.Application.Abstractions.Identity;
using OrderHub.Infrastructure.Identity;
using OrderHub.Application.Abstractions.Operations;
using OrderHub.Application.Abstractions.Catalog;
using OrderHub.Application.Abstractions.Customers;
using OrderHub.Application.Abstractions.Ordering;
using OrderHub.Application.Abstractions.Promotions;
using OrderHub.Application.Abstractions.Payments;
using OrderHub.Application.Abstractions.PublicOrdering;
using OrderHub.Application.Abstractions.Delivery;
using OrderHub.Application.Identity.Authentication;
using OrderHub.Application.Abstractions.Reporting;
using Microsoft.Extensions.Hosting;
using OrderHub.Application.Abstractions.Communications;
using OrderHub.Application.Abstractions.Reviews;
using OrderHub.Infrastructure.Communications;
using Dapper;

namespace OrderHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        SqlMapper.AddTypeHandler(new DateOnlyDapperTypeHandler());

        var section = configuration.GetRequiredSection(DatabaseOptions.SectionName);
        services.AddOptions<DatabaseOptions>()
            .Bind(section)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Database connection string is required.")
            .ValidateOnStart();

        var connectionString = section[nameof(DatabaseOptions.ConnectionString)]
            ?? throw new InvalidOperationException("Database:ConnectionString is required.");

        services.AddDbContext<OrderHubDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly("OrderHub.Infrastructure.Migrations")));
        services.AddOptions<OutboxProcessingOptions>()
            .Bind(configuration.GetSection(OutboxProcessingOptions.SectionName))
            .Validate(options => options.IsValid(), "Outbox processing options are invalid.")
            .ValidateOnStart();
        services.AddScoped<IOutboxMessageStager, OutboxMessageStager>();
        services.AddScoped(provider => new OutboxMessageStore(
            provider.GetRequiredService<OrderHubDbContext>(),
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OutboxProcessingOptions>>().Value));
        services.AddHostedService<OutboxProcessingWorker>();
        services.AddOptions<NotificationDeliveryOptions>()
            .Bind(configuration.GetSection(NotificationDeliveryOptions.SectionName))
            .Validate(options => options.IsValid(), "Notification delivery options are invalid.")
            .ValidateOnStart();
        services.AddOptions<NotificationSmtpOptions>()
            .Bind(configuration.GetSection(NotificationSmtpOptions.SectionName))
            .Validate(options => options.IsValid(), "Notification SMTP options are invalid.")
            .ValidateOnStart();
        services.AddOptions<MetaWhatsAppOptions>()
            .Bind(configuration.GetSection(MetaWhatsAppOptions.SectionName))
            .Validate(options => options.IsValid(), "Meta WhatsApp options are invalid.")
            .ValidateOnStart();
        services.AddScoped<NotificationRequestOutboxStager>();
        services.AddScoped<ICustomerOrderNotificationStager, CustomerOrderNotificationStager>();
        services.AddScoped<INotificationWriteRepository, NotificationWriteRepository>();
        services.AddScoped<INotificationReadGateway, NotificationReadGateway>();
        services.AddScoped<IOutboxMessageHandler, NotificationOutboxHandler>();
        services.AddScoped<INotificationChannelSender, SmtpNotificationSender>();
        services.AddHttpClient<MetaWhatsAppCloudSender>((provider, client) =>
        {
            var settings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MetaWhatsAppOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });
        services.AddScoped<INotificationChannelSender>(provider => provider.GetRequiredService<MetaWhatsAppCloudSender>());
        services.AddScoped<IReadConnectionFactory, NpgsqlReadConnectionFactory>();
        services.AddScoped<IReviewOrderGateway, ReviewOrderGateway>();
        services.AddScoped<IReviewReadGateway, ReviewReadGateway>();
        services.AddScoped<IReviewWriteRepository, ReviewRepository>();
        services.AddScoped<IReviewTransaction, ReviewTransaction>();
        services.AddScoped<IEstablishmentRepository, EstablishmentRepository>();
        services.AddScoped<IEstablishmentReadGateway, EstablishmentReadGateway>();
        services.AddScoped<IPlatformScopeGateway, PlatformScopeGateway>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddScoped<IAdministrativeUserRepository, AdministrativeUserRepository>();
        services.AddScoped<IAdministrativeUserManagementRepository, AdministrativeUserManagementRepository>();
        services.AddScoped<IAdministrativeUserManagementTransaction, AdministrativeUserManagementTransaction>();
        services.AddScoped<IAdministrativeUserReadGateway, AdministrativeUserReadGateway>();
        services.AddSingleton(configuration.GetSection(AuthenticationOptions.SectionName).Get<AuthenticationOptions>() ?? new AuthenticationOptions());
        services.AddSingleton<IAuthenticationSecretProtector, AuthenticationSecretProtector>();
        services.AddOptions<AuthenticationEmailOptions>().Bind(configuration.GetSection(AuthenticationEmailOptions.SectionName));
        services.AddScoped<IAuthenticationCodeSender, SmtpAuthenticationCodeSender>();
        services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
        services.AddScoped<IAuthenticationSessionResolver, AuthenticationSessionResolver>();
        services.AddScoped<IAuthenticationContextGateway, AuthenticationContextGateway>();
        services.AddOptions<PlatformBootstrapOptions>().Bind(configuration.GetSection(PlatformBootstrapOptions.SectionName));
        services.AddScoped<PlatformBootstrapper>();
        services.AddScoped<IEstablishmentAccessGateway, EstablishmentAccessGateway>();
        services.AddScoped<IOperationsReadGateway, OperationsReadGateway>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICategoryHierarchyGateway>(provider => (CategoryRepository)provider.GetRequiredService<ICategoryRepository>());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IAdditionalRepository, AdditionalRepository>();
        services.AddScoped<IAdditionalGroupRepository, AdditionalGroupRepository>();
        services.AddScoped<ICatalogReadGateway, CatalogReadGateway>();
        services.AddScoped<ICatalogMaintenanceReadGateway, CatalogMaintenanceReadGateway>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerReadGateway, CustomerReadGateway>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderNumberSequence, OrderNumberSequence>();
        services.AddScoped<IOrderConfirmationTransaction, OrderConfirmationTransaction>();
        services.AddScoped<IOrderOfferResolver, OrderOfferResolver>();
        services.AddScoped<IOrderCustomerResolver, OrderCustomerResolver>();
        services.AddScoped<IOrderTableResolver, OrderTableResolver>();
        services.AddScoped<IOrderReadGateway, OrderReadGateway>();
        services.AddScoped<IOrderSchedulingRepository, OrderSchedulingRepository>();
        services.AddScoped<IOrderSchedulingReadGateway, OrderSchedulingReadGateway>();
        services.AddScoped<IKitchenDisplayReadGateway, KitchenDisplayReadGateway>();
        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddScoped<ICouponReadGateway, CouponReadGateway>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentIdempotencyRepository, PaymentIdempotencyRepository>();
        services.AddScoped<IPaymentOrderGateway, PaymentOrderGateway>();
        services.AddScoped<IPaymentConfirmationTransaction, PaymentConfirmationTransaction>();
        services.AddScoped<IPaymentReadGateway, PaymentReadGateway>();
        services.AddScoped<IPublicOrderingContextGateway, PublicOrderingContextGateway>();
        services.AddScoped<IPublicOrderLocator, PublicOrderLocator>();
        services.AddScoped<IPublicOrderRequestRepository, PublicOrderRequestRepository>();
        services.AddScoped<IPublicOrderTransaction, PublicOrderTransaction>();
        services.AddScoped<IDeliveryRegionRepository, DeliveryRegionRepository>();
        services.AddScoped<IDeliveryReadGateway, DeliveryReadGateway>();
        services.AddScoped<IBusinessDashboardReadGateway, BusinessDashboardReadGateway>();
        services.AddHealthChecks().AddDbContextCheck<OrderHubDbContext>("postgresql", tags: ["ready"]);

        services.AddScoped<IEstablishmentConfigurationRepository, EstablishmentConfigurationRepository>();
        services.AddScoped<IOnboardingReadGateway, OnboardingReadGateway>();
        services.AddScoped<IAvailabilityRepository, AvailabilityRepository>();
        services.AddScoped<IOfferAvailabilityRepository, OfferAvailabilityRepository>();
        services.AddScoped<IAvailabilityReadGateway, AvailabilityReadGateway>();
        services.AddScoped<IOrderAvailabilityGateway, OrderAvailabilityGateway>();
        return services;
    }
}
