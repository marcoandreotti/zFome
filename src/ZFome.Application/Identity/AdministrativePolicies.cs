using ZFome.Domain.Identity;

namespace ZFome.Application.Identity;

public static class AdministrativePolicies
{
    public const string Ownership = "ownership";
    public const string Administration = "administration";
    public const string Management = "management";
    public const string Attendance = "attendance";
    public const string Kitchen = "kitchen";
    public const string Delivery = "delivery";
    public const string CustomerOperations = "customer-operations";
    public const string OrderRead = "order-read";
    public const string OrderAttendance = "order-attendance";
    public const string OrderKitchen = "order-kitchen";
    public const string OrderDelivery = "order-delivery";
    public const string OrderCompletion = "order-completion";
    public const string PromotionManagement = "promotion-management";
    public const string PaymentManagement = "payment-management";
    public const string PaymentOperations = "payment-operations";

    public static IReadOnlyDictionary<string, AdministrativeRole[]> RoleMap { get; } =
        new Dictionary<string, AdministrativeRole[]>(StringComparer.Ordinal)
        {
            [Ownership] = [AdministrativeRole.Owner],
            [Administration] = [AdministrativeRole.Owner, AdministrativeRole.Admin],
            [Management] = [AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager],
            [Attendance] = [AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager, AdministrativeRole.Attendant],
            [Kitchen] = [AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager, AdministrativeRole.Kitchen],
            [Delivery] = [AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager, AdministrativeRole.Delivery],
            [CustomerOperations] = [AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager, AdministrativeRole.Attendant],
            [OrderRead] = Enum.GetValues<AdministrativeRole>(),
            [OrderAttendance] = [AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager, AdministrativeRole.Attendant],
            [OrderKitchen] = [AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager, AdministrativeRole.Kitchen],
            [OrderDelivery] = [AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager, AdministrativeRole.Delivery],
            [OrderCompletion] = [AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager, AdministrativeRole.Attendant, AdministrativeRole.Delivery],
            [PromotionManagement] = [AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager],
            [PaymentManagement] = [AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager],
            [PaymentOperations] = [AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager, AdministrativeRole.Attendant]
        };
}
