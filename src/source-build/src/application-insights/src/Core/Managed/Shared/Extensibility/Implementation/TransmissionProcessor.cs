﻿namespace Microsoft.ApplicationInsights.Extensibility.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;
    using Platform;

    /// <summary>
    /// An <see cref="ITelemetryProcessor"/> that act as a proxy to the Transmission of telemetry"/>.
    /// The <see cref="ITelemetryChannel"/>, as configured in <see cref="TelemetryConfiguration"/> will be used for transmission.
    /// This processor is always appended as the last processor in the chain.
    /// </summary>
    internal class TransmissionProcessor : ITelemetryProcessor
    {        
        private readonly TelemetryConfiguration configuration;
        private readonly IDebugOutput debugOutput;     

        /// <summary>
        /// Initializes a new instance of the <see cref="TransmissionProcessor"/> class.
        /// </summary>        
        /// <param name="configuration">The <see cref="TelemetryConfiguration"/> to get the channel from.</param>
        internal TransmissionProcessor(TelemetryConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            this.configuration = configuration;
            this.debugOutput = PlatformSingleton.Current.GetDebugOutput();
        }

        /// <summary>
        /// Process the given <see cref="ITelemetry"/> item. Here processing is sending the item through the channel/>.
        /// </summary>
        public void Process(ITelemetry item)
        {
            TelemetryDebugWriter.WriteTelemetry(item);

            this.configuration.TelemetryChannel.Send(item);
        }
    }
}
