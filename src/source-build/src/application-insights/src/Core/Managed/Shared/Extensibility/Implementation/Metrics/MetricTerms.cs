﻿namespace Microsoft.ApplicationInsights.Extensibility.Implementation.Metrics
{
    using System;

    internal static class MetricTerms
    {
        private const string MetricPropertiesNamePrefix = "_MS";

        public static class Aggregation
        {
            public static class Interval
            {
                public static class Moniker
                {
                    public const string Key = MetricPropertiesNamePrefix + ".AggregationIntervalMs";
                }
            }
        }

        public static class Extraction
        {
            public static class ProcessedByExtractors
            {
                public static class Moniker
                {
                    public const string Key = MetricPropertiesNamePrefix + ".ProcessedByMetricExtractors";
                    public const string ExtractorInfoTemplate = "(Name:'{0}', Ver:'{1}')";      // $"(Name:'{ExtractorName}', Ver:'{ExtractorVersion}')"
                }
            }
        }

        public static class Autocollection
        {
            public static class Moniker
            {
                public const string Key = MetricPropertiesNamePrefix + ".IsAutocollected";
                public const string Value = "True";
            }

            public static class MetricId
            {
                public static class Moniker
                {
                    public const string Key = MetricPropertiesNamePrefix + ".MetricId";
                }
            }

            public static class Metric
            {
                public static class RequestDuration
                {
                    public const string Name = "Server response time";
                    public const string Id = "requests/duration";
                }

                public static class DependencyCallDuration
                {
                    public const string Name = "Dependency duration";
                    public const string Id = "dependencies/duration";
                }
            }

            public static class Request
            {
                public static class PropertyNames
                {
                    public const string Success = "Request.Success";
                }
            }

            public static class DependencyCall
            {
                public static class PropertyNames
                {
                    public const string Success = "Dependency.Success";
                    public const string TypeName = "Dependency.Type";
                }

                public static class TypeNames
                {
                    public const string Other = "Other";
                    public const string Unknown = "Unknown";
                }
            }
        }
    }
}
