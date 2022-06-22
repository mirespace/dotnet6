﻿namespace Microsoft.ApplicationInsights.Extensibility
{
    /// <summary>
    /// Provides functionality to process metric values prior to aggregation.
    /// </summary>
    internal interface IMetricProcessor
    {
        /// <summary>
        /// Process metric value.
        /// </summary>
        /// <param name="metric">Metric definition.</param>
        /// <param name="value">Metric value.</param>
        void Track(Metric metric, double value);
    }
}
