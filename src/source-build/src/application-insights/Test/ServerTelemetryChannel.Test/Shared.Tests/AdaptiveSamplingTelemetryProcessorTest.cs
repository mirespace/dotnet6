﻿namespace Microsoft.ApplicationInsights.WindowsServer.Channel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reflection;
    using System.Threading;

    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.TestFramework;
    using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
    using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Assert = Xunit.Assert;

    [TestClass]
    public class AdaptiveSamplingTelemetryProcessorTest
    {
        [TestMethod]
        public void AllTelemetryCapturedWhenProductionRateIsLow()
        {
            var sentTelemetry = new List<ITelemetry>();
            int itemsProduced = 0;

            using (var tc = new TelemetryConfiguration() { TelemetryChannel = new StubTelemetryChannel() })
            {
                var chainBuilder = new TelemetryProcessorChainBuilder(tc);

                // set up adaptive sampling that evaluates and changes sampling % frequently
                chainBuilder
                    .UseAdaptiveSampling(
                        new SamplingPercentageEstimatorSettings()
                        {
                            EvaluationInterval = TimeSpan.FromSeconds(1),
                            SamplingPercentageDecreaseTimeout = TimeSpan.FromSeconds(2),
                            SamplingPercentageIncreaseTimeout = TimeSpan.FromSeconds(2),
                        },
                        this.TraceSamplingPercentageEvaluation)
                    .Use((next) => new StubTelemetryProcessor(next) { OnProcess = (t) => sentTelemetry.Add(t) });

                chainBuilder.Build();

                const int productionFrequencyMs = 1000;

                using (var productionTimer = new Timer(
                            (state) => { tc.TelemetryProcessorChain.Process(new RequestTelemetry()); itemsProduced++; },
                            null,
                            productionFrequencyMs,
                            productionFrequencyMs))
                {
                    Thread.Sleep(25000);
                }
            }

            Assert.Equal(itemsProduced, sentTelemetry.Count);
        }

        [TestMethod]
        public void SamplingPercentageAdjustsAccordingToConstantHighProductionRate()
        {
            var sentTelemetry = new List<ITelemetry>();
            int itemsProduced = 0;

            using (var tc = new TelemetryConfiguration() { TelemetryChannel = new StubTelemetryChannel() })
            {
                var chainBuilder = new TelemetryProcessorChainBuilder(tc);

                // set up adaptive sampling that evaluates and changes sampling % frequently
                chainBuilder
                    .UseAdaptiveSampling(
                        new SamplingPercentageEstimatorSettings()
                        {
                            EvaluationInterval = TimeSpan.FromSeconds(1),
                            SamplingPercentageDecreaseTimeout = TimeSpan.FromSeconds(2),
                            SamplingPercentageIncreaseTimeout = TimeSpan.FromSeconds(2),
                        },
                        this.TraceSamplingPercentageEvaluation)
                    .Use((next) => new StubTelemetryProcessor(next) { OnProcess = (t) => sentTelemetry.Add(t) });

                chainBuilder.Build();

                const int productionFrequencyMs = 100;

                using (var productionTimer = new Timer(
                            (state) =>
                            {
                                for (int i = 0; i < 2; i++)
                                {
                                    tc.TelemetryProcessorChain.Process(new RequestTelemetry());
                                    itemsProduced++;
                                }
                            },
                            null,
                            0,
                            productionFrequencyMs))
                {
                    Thread.Sleep(25000);
                }
            }

            // number of items produced should be close to target of 5/second
            int targetItemCount = 25 * 5;

            // tolrance +-
            int tolerance = targetItemCount / 2;

            Trace.WriteLine(string.Format("'Ideal' telemetry item count: {0}", targetItemCount));
            Trace.WriteLine(string.Format(
                "Expected range: from {0} to {1}",
                targetItemCount - tolerance,
                targetItemCount + tolerance));
            Trace.WriteLine(string.Format(
                "Actual telemetry item count: {0} ({1:##.##}% of ideal)", 
                sentTelemetry.Count,
                100.0 * sentTelemetry.Count / targetItemCount));

            Assert.True(sentTelemetry.Count > targetItemCount - tolerance);
            Assert.True(sentTelemetry.Count < targetItemCount + tolerance);
        }

        [TestMethod]
        public void SamplingPercentageAdjustsForSpikyProductionRate()
        {
            var sentTelemetry = new List<ITelemetry>();
            int itemsProduced = 0;

            using (var tc = new TelemetryConfiguration() { TelemetryChannel = new StubTelemetryChannel() })
            {
                var chainBuilder = new TelemetryProcessorChainBuilder(tc);

                // set up adaptive sampling that evaluates and changes sampling % frequently
                chainBuilder
                    .UseAdaptiveSampling(
                        new SamplingPercentageEstimatorSettings()
                        {
                            InitialSamplingPercentage = 5.0,
                            EvaluationInterval = TimeSpan.FromSeconds(1),
                            SamplingPercentageDecreaseTimeout = TimeSpan.FromSeconds(2),
                            SamplingPercentageIncreaseTimeout = TimeSpan.FromSeconds(10),
                        },
                        this.TraceSamplingPercentageEvaluation)
                    .Use((next) => new StubTelemetryProcessor(next) { OnProcess = (t) => sentTelemetry.Add(t) });

                chainBuilder.Build();

                const int regularProductionFrequencyMs = 100;
                const int spikeProductionFrequencyMs = 3000;

                using (var regularProductionTimer = new Timer(
                            (state) =>
                            {
                                for (int i = 0; i < 2; i++)
                                {
                                    tc.TelemetryProcessorChain.Process(new RequestTelemetry());
                                    Interlocked.Increment(ref itemsProduced);
                                }
                            },
                            null,
                            0,
                            regularProductionFrequencyMs))
                using (var spikeProductionTimer = new Timer(
                            (state) =>
                            {
                                for (int i = 0; i < 200; i++)
                                {
                                    tc.TelemetryProcessorChain.Process(new RequestTelemetry());
                                    Interlocked.Increment(ref itemsProduced);
                                }
                            },
                            null,
                            0,
                            spikeProductionFrequencyMs))
                {
                    Thread.Sleep(30000);
                }
            }

            // number of items produced should be close to target of 5/second
            int targetItemCount = 30 * 5;
            int tolerance = targetItemCount / 2;

            Trace.WriteLine(string.Format("'Ideal' telemetry item count: {0}", targetItemCount));
            Trace.WriteLine(string.Format(
                "Expected range: from {0} to {1}",
                targetItemCount - tolerance,
                targetItemCount + tolerance));
            Trace.WriteLine(string.Format(
                "Actual telemetry item count: {0} ({1:##.##}% of ideal)",
                sentTelemetry.Count,
                100.0 * sentTelemetry.Count / targetItemCount));

            Assert.True(sentTelemetry.Count > targetItemCount - tolerance);
            Assert.True(sentTelemetry.Count < targetItemCount + tolerance);
        }

        [TestMethod]
        public void AdaptiveSamplingSetsExcludedTypesOnInternalSamplingProcessor()
        {
            var tc = new TelemetryConfiguration { TelemetryChannel = new StubTelemetryChannel() };
            var channelBuilder = new TelemetryProcessorChainBuilder(tc);
            channelBuilder.UseAdaptiveSampling(5, "request;");
            channelBuilder.Build();

            var fieldInfo = typeof(AdaptiveSamplingTelemetryProcessor).GetField("samplingProcessor", BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic);
            SamplingTelemetryProcessor internalProcessor = (SamplingTelemetryProcessor) fieldInfo.GetValue(tc.TelemetryProcessorChain.FirstTelemetryProcessor);

            Assert.Equal("request;", internalProcessor.ExcludedTypes);
        }

        private void TraceSamplingPercentageEvaluation(
            double afterSamplingTelemetryItemRatePerSecond,
            double currentSamplingPercentage,
            double newSamplingPercentage,
            bool isSamplingPercentageChanged,
            SamplingPercentageEstimatorSettings settings)
        {
            Trace.WriteLine(string.Format(
                "[Sampling% evaluation] {0}, Eps: {1}, Current %: {2}, New %: {3}, Changed: {4}",
                DateTimeOffset.UtcNow.ToString("o"), 
                afterSamplingTelemetryItemRatePerSecond,
                currentSamplingPercentage,
                newSamplingPercentage,
                isSamplingPercentageChanged));
        }
    }
}
