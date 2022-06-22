﻿namespace Microsoft.ApplicationInsights.Extensibility.Implementation
{
    using System.Globalization;

    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;

    internal static class Telemetry
    {
        public static void WriteEnvelopeProperties(this ITelemetry telemetry, IJsonWriter json)
        {
            json.WriteProperty("time", telemetry.Timestamp.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));

            var samplingSupportingTelemetry = telemetry as ISupportSampling;

            if (samplingSupportingTelemetry != null
                && samplingSupportingTelemetry.SamplingPercentage.HasValue
                && (samplingSupportingTelemetry.SamplingPercentage.Value > 0.0 + 1.0E-12) 
                && (samplingSupportingTelemetry.SamplingPercentage.Value < 100.0 - 1.0E-12))
            {
                json.WriteProperty("sampleRate", samplingSupportingTelemetry.SamplingPercentage.Value);
            }

            json.WriteProperty("seq", telemetry.Sequence);
            WriteTelemetryContext(json, telemetry.Context);
        }

        public static void WriteTelemetryName(this ITelemetry telemetry, IJsonWriter json, string telemetryName)
        {
            // A different event name prefix is sent for normal mode and developer mode.
            bool isDevMode = false;
            string devModeProperty;
            var telemetryWithProperties = telemetry as ISupportProperties;
            if (telemetryWithProperties != null && telemetryWithProperties.Properties.TryGetValue("DeveloperMode", out devModeProperty))
            {
                bool.TryParse(devModeProperty, out isDevMode);
            }

            // Format the event name using the following format:
            // Microsoft.ApplicationInsights[.Dev].<normalized-instrumentation-key>.<event-type>
            var eventName = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0}{1}{2}",
                isDevMode ? Constants.DevModeTelemetryNamePrefix : Constants.TelemetryNamePrefix,
                NormalizeInstrumentationKey(telemetry.Context.InstrumentationKey),
                telemetryName);
            json.WriteProperty("name", eventName);
        }

        public static void WriteTelemetryContext(IJsonWriter json, TelemetryContext context)
        {
            if (context != null)
            {
                json.WriteProperty("iKey", context.InstrumentationKey);
                json.WriteProperty("tags", context.Tags);
            }
        }

        /// <summary>
        /// Normalize instrumentation key by removing dashes ('-') and making string in the lowercase.
        /// In case no InstrumentationKey is available just return empty string.
        /// In case when InstrumentationKey is available return normalized key + dot ('.')
        /// as a separator between instrumentation key part and telemetry name part.
        /// </summary>
        private static string NormalizeInstrumentationKey(string instrumentationKey)
        {
            if (instrumentationKey.IsNullOrWhiteSpace())
            {
                return string.Empty;
            }

            return instrumentationKey.Replace("-", string.Empty).ToLowerInvariant() + ".";
        }
    }
}
