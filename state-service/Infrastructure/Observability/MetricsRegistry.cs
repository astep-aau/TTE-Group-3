using System.Diagnostics.Metrics;
using System.Diagnostics;

namespace StateService.Infrastructure.Observability
{
    public static class MetricsRegistry
    {
        public static readonly Meter Meter = new("StateService", "1.0.0");
        public static readonly Counter<int> MessagesConsumed = Meter.CreateCounter<int>("messages.consumed", "messages", "Total messages successfully consumed");
        public static readonly Counter<int> MessagesFailed = Meter.CreateCounter<int>("messages.failed", "messages", "Total messages failed and requeued");
        public static readonly Histogram<double> MessageProcessingMs = Meter.CreateHistogram<double>("message.processing.ms", "ms", "Message processing duration");
    }

    public sealed class ActivitySourceHolder
    {
        public static readonly ActivitySource Source = new("StateService", "1.0.0");
    }
}
