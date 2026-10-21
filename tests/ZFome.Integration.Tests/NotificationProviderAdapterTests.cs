using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ZFome.Application.Abstractions.Communications;
using ZFome.Infrastructure.Communications;

namespace ZFome.Integration.Tests;

public sealed class NotificationProviderAdapterTests
{
    [Fact]
    public async Task Missing_provider_configuration_is_recoverable_without_exposing_a_secret()
    {
        var email = new SmtpNotificationSender(Options.Create(new NotificationSmtpOptions()));
        var emailResult = await email.SendAsync(new NotificationChannelSendRequest(NotificationChannel.Email,
            "customer@example.test", "pt_BR", "Subject", "Body", null, new Dictionary<string, string>(), "email-0"), CancellationToken.None);
        Assert.Equal(NotificationProviderOutcome.RetryableFailure, emailResult.Outcome);
        Assert.Equal("smtp_not_configured", emailResult.SafeErrorCode);

        using var http = new HttpClient(new DelegateHandler((_, _) => throw new InvalidOperationException("HTTP must not be called.")));
        var whatsapp = new MetaWhatsAppCloudSender(http, Options.Create(new MetaWhatsAppOptions()));
        var whatsappResult = await whatsapp.SendAsync(new NotificationChannelSendRequest(NotificationChannel.WhatsApp,
            "+15551234567", "pt_BR", "", "", "template", new Dictionary<string, string>(), "wa-0"), CancellationToken.None);
        Assert.Equal(NotificationProviderOutcome.RetryableFailure, whatsappResult.Outcome);
        Assert.Equal("meta_whatsapp_not_configured", whatsappResult.SafeErrorCode);
    }

    [Fact]
    public async Task Smtp_sender_submits_rendered_email_to_configured_sandbox()
    {
        using var server = new SmtpTestServer();
        var sender = new SmtpNotificationSender(Options.Create(new NotificationSmtpOptions
        {
            Host = "127.0.0.1",
            Port = server.Port,
            FromAddress = "no-reply@example.test",
            TimeoutSeconds = 5
        }));

        var result = await sender.SendAsync(new NotificationChannelSendRequest(NotificationChannel.Email,
            "customer@example.test", "pt_BR", "Order {{1}} confirmed", "Hello {{name}}!", null,
            new Dictionary<string, string> { ["1"] = "42", ["name"] = "Ana" }, "email-42"), CancellationToken.None);

        Assert.Equal(NotificationProviderOutcome.Accepted, result.Outcome);
        var message = await server.Message;
        Assert.Contains("Order 42 confirmed", message);
        Assert.Contains("Hello Ana!", message);
    }

    [Fact]
    public async Task Meta_sender_posts_only_approved_template_data_and_maps_acceptance()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        using var http = new HttpClient(new DelegateHandler(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"messages\":[{\"id\":\"wamid.123\"}]}")
            };
        }));
        var sender = new MetaWhatsAppCloudSender(http, Options.Create(new MetaWhatsAppOptions
        {
            GraphApiVersion = "v99.0",
            PhoneNumberId = "1234567890",
            AccessToken = "test-token"
        }));

        var result = await sender.SendAsync(new NotificationChannelSendRequest(NotificationChannel.WhatsApp,
            "+15551234567", "pt_BR", "", "", "order_update",
            new Dictionary<string, string> { ["2"] = "42", ["1"] = "Ana" }, "wa-42"), CancellationToken.None);

        Assert.Equal(NotificationProviderOutcome.Accepted, result.Outcome);
        Assert.Equal("wamid.123", result.ProviderMessageId);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("test-token", capturedRequest.Headers.Authorization.Parameter);
        Assert.Equal("https://graph.facebook.com/v99.0/1234567890/messages", capturedRequest.RequestUri!.ToString());
        using var body = JsonDocument.Parse(capturedBody!);
        Assert.Equal("whatsapp", body.RootElement.GetProperty("messaging_product").GetString());
        Assert.Equal("order_update", body.RootElement.GetProperty("template").GetProperty("name").GetString());
        var parameters = body.RootElement.GetProperty("template").GetProperty("components")[0].GetProperty("parameters");
        Assert.Equal(new[] { "Ana", "42" }, parameters.EnumerateArray().Select(x => x.GetProperty("text").GetString()).ToArray());
    }

    [Fact]
    public async Task Meta_rate_limit_is_retryable_and_timeout_is_uncertain()
    {
        using var rateLimitedHttp = new HttpClient(new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)429))));
        var sender = new MetaWhatsAppCloudSender(rateLimitedHttp, Options.Create(new MetaWhatsAppOptions
        {
            GraphApiVersion = "v99.0", PhoneNumberId = "1234567890", AccessToken = "test-token"
        }));
        var request = new NotificationChannelSendRequest(NotificationChannel.WhatsApp, "+15551234567", "pt_BR", "", "",
            "order_update", new Dictionary<string, string>(), "wa-43");

        Assert.Equal(NotificationProviderOutcome.RetryableFailure, (await sender.SendAsync(request, CancellationToken.None)).Outcome);

        using var timeoutHttp = new HttpClient(new DelegateHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        })) { Timeout = TimeSpan.FromMilliseconds(50) };
        var timeoutSender = new MetaWhatsAppCloudSender(timeoutHttp, Options.Create(new MetaWhatsAppOptions
        {
            GraphApiVersion = "v99.0", PhoneNumberId = "1234567890", AccessToken = "test-token"
        }));
        var timeout = await timeoutSender.SendAsync(request, CancellationToken.None);
        Assert.Equal(NotificationProviderOutcome.Uncertain, timeout.Outcome);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class SmtpTestServer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly TaskCompletionSource<string> message = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public Task<string> Message => message.Task;

        public SmtpTestServer()
        {
            listener.Start();
            _ = ServeAsync();
        }

        private async Task ServeAsync()
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync();
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream);
                await using var writer = new StreamWriter(stream) { AutoFlush = true };
                await writer.WriteLineAsync("220 localhost ESMTP");
                var line = await reader.ReadLineAsync();
                var data = new System.Text.StringBuilder();
                while (line is not null)
                {
                    if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) || line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                        await writer.WriteLineAsync("250 localhost");
                    else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase) || line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
                        await writer.WriteLineAsync("250 OK");
                    else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("354 End data with <CRLF>.<CRLF>");
                        while ((line = await reader.ReadLineAsync()) is not null && line != ".") data.AppendLine(line);
                        message.TrySetResult(data.ToString());
                        await writer.WriteLineAsync("250 queued");
                    }
                    else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("221 Bye");
                        return;
                    }
                    else
                        await writer.WriteLineAsync("250 OK");
                    line = await reader.ReadLineAsync();
                }
                message.TrySetException(new InvalidOperationException("SMTP client disconnected before sending a message."));
            }
            catch (Exception exception)
            {
                message.TrySetException(exception);
            }
        }

        public void Dispose() => listener.Stop();
    }
}
