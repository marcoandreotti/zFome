using System.Diagnostics.Metrics;

namespace ZFome.Api.Realtime;

public sealed class OrderRealtimeTelemetry : IDisposable
{
    private readonly Meter meter = new("ZFome.Api.RealtimeOrders", "1.0.0");
    private readonly UpDownCounter<long> connections;
    private readonly Counter<long> subscriptions;
    private readonly Counter<long> published;
    private readonly Counter<long> deniedDeliveries;
    private readonly Counter<long> failures;

    public OrderRealtimeTelemetry()
    {
        connections = meter.CreateUpDownCounter<long>("zfome.realtime.connections");
        subscriptions = meter.CreateCounter<long>("zfome.realtime.subscriptions");
        published = meter.CreateCounter<long>("zfome.realtime.events.published");
        deniedDeliveries = meter.CreateCounter<long>("zfome.realtime.deliveries.denied");
        failures = meter.CreateCounter<long>("zfome.realtime.failures");
    }

    public void ConnectionOpened() => connections.Add(1);
    public void ConnectionClosed() => connections.Add(-1);
    public void SubscriptionAccepted() => subscriptions.Add(1);
    public void EventPublished() => published.Add(1);
    public void DeliveryDenied() => deniedDeliveries.Add(1);
    public void Failure() => failures.Add(1);
    public void Dispose() => meter.Dispose();
}
