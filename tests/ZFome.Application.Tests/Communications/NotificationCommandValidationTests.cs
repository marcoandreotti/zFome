using ZFome.Application.Communications;
using ZFome.Application.Abstractions.Communications;

namespace ZFome.Application.Tests.Communications;

public sealed class NotificationCommandValidationTests
{
    [Theory]
    [InlineData(NotificationChannel.Email, "person@example.test", true)]
    [InlineData(NotificationChannel.Email, "not-an-address", false)]
    [InlineData(NotificationChannel.WhatsApp, "+15551234567", true)]
    [InlineData(NotificationChannel.WhatsApp, "555-1234", false)]
    public async Task Notification_destination_validation_is_channel_specific(NotificationChannel channel, string destination, bool expectedValid)
    {
        var command = new RequestNotificationCommand(Guid.NewGuid(), Guid.NewGuid(), channel, destination,
            new Dictionary<string, string>(), "request-123");

        var result = await new RequestNotificationValidator().ValidateAsync(command);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public async Task WhatsApp_template_requires_a_provider_approved_template_name()
    {
        var command = new UpsertNotificationTemplateCommand(Guid.NewGuid(), null, "order.update", NotificationChannel.WhatsApp,
            "pt_BR", string.Empty, "Order {{1}}", null, true, true);

        var result = await new UpsertNotificationTemplateValidator().ValidateAsync(command);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.ProviderTemplateName));
    }

    [Fact]
    public async Task Customer_order_template_accepts_supported_placeholders()
    {
        var command = new UpsertNotificationTemplateCommand(Guid.NewGuid(), null, "order.confirmed", NotificationChannel.Email,
            "pt_BR", "Pedido {{orderNumber}}", "Olá {{name}}: {{statusLabel}} {{trackingUrl}}", null, false, true);

        var result = await new UpsertNotificationTemplateValidator().ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Customer_order_template_rejects_unavailable_placeholders()
    {
        var command = new UpsertNotificationTemplateCommand(Guid.NewGuid(), null, "order.ready", NotificationChannel.Email,
            "pt_BR", "Pedido {{orderId}}", "Pronto", null, false, true);

        var result = await new UpsertNotificationTemplateValidator().ValidateAsync(command);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Subject));
    }

    [Fact]
    public async Task Consent_change_requires_a_verifiable_source()
    {
        var command = new SetNotificationConsentCommand(Guid.NewGuid(), NotificationChannel.Email, "order.update",
            "person@example.test", true, " ");

        var result = await new SetNotificationConsentValidator().ValidateAsync(command);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Source));
    }

    [Fact]
    public async Task Notification_history_requires_start_before_exclusive_end()
    {
        var query = new ListNotificationHistoryQuery(Guid.NewGuid(), 1, 100,
            DateTimeOffset.Parse("2026-10-02T00:00:00Z"), DateTimeOffset.Parse("2026-10-02T00:00:00Z"));

        var result = await new ListNotificationHistoryValidator().ValidateAsync(query);

        Assert.Contains(result.Errors, error => error.ErrorMessage == "A data inicial deve ser anterior à data final.");
    }
}
