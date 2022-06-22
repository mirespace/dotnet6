﻿namespace Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing
{
    using System;
#if !NET40
    using System.Diagnostics.Tracing;
#endif
    using System.Linq;
#if NET40
    using Microsoft.Diagnostics.Tracing;
#endif

    internal class DiagnosticsEventListener : EventListener
    {
        private const long AllKeyword = -1;
        private readonly EventLevel logLevel;
        private readonly DiagnosticsListener listener;

        public DiagnosticsEventListener(DiagnosticsListener listener, EventLevel logLevel)
        {
            this.listener = listener;
            this.logLevel = logLevel;
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventSourceEvent)
        {
            if (eventSourceEvent == null)
            {
                return;
            }

            var metadata = new EventMetaData
            {                
                Keywords = (long)eventSourceEvent.Keywords,
                MessageFormat = eventSourceEvent.Message,
                EventId = eventSourceEvent.EventId,
                Level = eventSourceEvent.Level
            };

            var traceEvent = new TraceEvent
            {
                MetaData = metadata,
                Payload = eventSourceEvent.Payload != null ? eventSourceEvent.Payload.ToArray() : null
            };

            this.listener.WriteEvent(traceEvent);
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.StartsWith("Microsoft-ApplicationInsights-", StringComparison.Ordinal))
            {
                this.EnableEvents(eventSource, this.logLevel, (EventKeywords)AllKeyword);
            }

            base.OnEventSourceCreated(eventSource);
        }
    }
}