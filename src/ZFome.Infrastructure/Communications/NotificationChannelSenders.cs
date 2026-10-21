using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ZFome.Application.Abstractions.Communications;

namespace ZFome.Infrastructure.Communications;

public sealed partial class SmtpNotificationSender(IOptions<NotificationSmtpOptions> options) : INotificationChannelSender
{
    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task<NotificationProviderResult> SendAsync(NotificationChannelSendRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromAddress))
            return new(NotificationProviderOutcome.RetryableFailure, null, "smtp_not_configured");
        if (!MailAddress.TryCreate(settings.FromAddress, out _)
            || !MailAddress.TryCreate(request.Destination, out _))
            return new(NotificationProviderOutcome.PermanentFailure, null, "invalid_email_address");

        try
        {
            using var message = new MailMessage(settings.FromAddress, request.Destination)
            {
                Subject = Render(request.Subject, request.Parameters),
                Body = Render(request.Body, request.Parameters),
                IsBodyHtml = false
            };
            using var client = new SmtpClient(settings.Host, settings.Port)
            {
                EnableSsl = settings.EnableSsl,
                Timeout = settings.TimeoutSeconds * 1000
            };
            if (!string.IsNullOrWhiteSpace(settings.Username))
                client.Credentials = new NetworkCredential(settings.Username, settings.Password);
            await client.SendMailAsync(message, cancellationToken);
            return new(NotificationProviderOutcome.Accepted, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SmtpException exception)
        {
            var outcome = exception.StatusCode switch
            {
                SmtpStatusCode.ServiceNotAvailable or SmtpStatusCode.MailboxBusy or SmtpStatusCode.LocalErrorInProcessing
                    or SmtpStatusCode.InsufficientStorage => NotificationProviderOutcome.RetryableFailure,
                SmtpStatusCode.GeneralFailure => NotificationProviderOutcome.Uncertain,
                _ => NotificationProviderOutcome.PermanentFailure
            };
            return new(outcome, null, $"smtp_{(int)exception.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            return new(NotificationProviderOutcome.Uncertain, null, "smtp_timeout_uncertain");
        }
        catch (SocketException)
        {
            return new(NotificationProviderOutcome.Uncertain, null, "smtp_transport_uncertain");
        }
    }

    private static string Render(string template, IReadOnlyDictionary<string, string> parameters) =>
        PlaceholderRegex().Replace(template, match => parameters.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);

    [GeneratedRegex("\\{\\{([a-zA-Z0-9_.-]{1,50})\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}

public sealed class MetaWhatsAppCloudSender(HttpClient httpClient, IOptions<MetaWhatsAppOptions> options) : INotificationChannelSender
{
    public NotificationChannel Channel => NotificationChannel.WhatsApp;

    public async Task<NotificationProviderResult> SendAsync(NotificationChannelSendRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.GraphApiVersion) || string.IsNullOrWhiteSpace(settings.PhoneNumberId)
            || string.IsNullOrWhiteSpace(settings.AccessToken) || string.IsNullOrWhiteSpace(request.ProviderTemplateName))
            return new(NotificationProviderOutcome.RetryableFailure, null, "meta_whatsapp_not_configured");

        var bodyParameters = request.Parameters
            .Where(parameter => int.TryParse(parameter.Key, out _))
            .OrderBy(parameter => int.Parse(parameter.Key, System.Globalization.CultureInfo.InvariantCulture))
            .Select(parameter => new { type = "text", text = parameter.Value })
            .ToArray();
        var payload = new
        {
            messaging_product = "whatsapp",
            to = new string(request.Destination.Where(char.IsDigit).ToArray()),
            type = "template",
            template = new
            {
                name = request.ProviderTemplateName,
                language = new { code = request.Language },
                components = bodyParameters.Length == 0
                    ? Array.Empty<object>()
                    : [new { type = "body", parameters = bodyParameters }]
            }
        };
        var endpoint = $"https://graph.facebook.com/{Uri.EscapeDataString(settings.GraphApiVersion)}/{Uri.EscapeDataString(settings.PhoneNumberId)}/messages";
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        try
        {
            using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var outcome = response.StatusCode == HttpStatusCode.RequestTimeout || (int)response.StatusCode == 429 || (int)response.StatusCode >= 500
                    ? NotificationProviderOutcome.RetryableFailure
                    : NotificationProviderOutcome.PermanentFailure;
                return new(outcome, null, $"meta_http_{(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var providerId = json.RootElement.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var id) ? id.GetString() : null;
            return string.IsNullOrWhiteSpace(providerId)
                ? new(NotificationProviderOutcome.Uncertain, null, "meta_response_uncertain")
                : new(NotificationProviderOutcome.Accepted, providerId, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new(NotificationProviderOutcome.Uncertain, null, "meta_timeout_uncertain");
        }
        catch (HttpRequestException)
        {
            return new(NotificationProviderOutcome.Uncertain, null, "meta_transport_uncertain");
        }
    }
}
