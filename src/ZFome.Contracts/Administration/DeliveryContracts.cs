namespace ZFome.Contracts.Administration;

public sealed record DeliveryRegionUpsertRequest(string Name, string PostalCodeFrom, string PostalCodeTo, decimal Fee, int EstimatedMinutes);
public sealed record DeliveryRegionActiveRequest(bool IsActive);
