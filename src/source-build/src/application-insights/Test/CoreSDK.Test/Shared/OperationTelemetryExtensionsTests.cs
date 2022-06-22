﻿namespace Microsoft.ApplicationInsights
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Assert = Xunit.Assert;

    /// <summary>
    /// Tests corresponding to OperationExtension methods.
    /// </summary>
    [TestClass]
    public class OperationTelemetryExtensionsTests
    {
        /// <summary>
        /// Tests the scenario if StartOperation returns operation with telemetry item of same type.
        /// </summary>
        [TestMethod]
        public void OperationTelemetryStartInitializesTimeStampAndStartTimeToTelemetry()
        {
            var telemetry = new DependencyTelemetry();
            Assert.Equal(DateTimeOffset.MinValue, telemetry.Timestamp);
            telemetry.Start();
            Assert.NotEqual(DateTimeOffset.MinValue, telemetry.Timestamp);
        }

        /// <summary>
        /// Tests the scenario if Stop does not change start time and timestamp after start is called.
        /// </summary>
        [TestMethod]
        public void OperationTelemetryStopDoesNotAffectTimeStampAndStartTimeAfterStart()
        {
            var telemetry = new DependencyTelemetry();
            telemetry.Start();
            DateTimeOffset actualTime = telemetry.Timestamp;
            telemetry.Stop();
            Assert.Equal(telemetry.Timestamp, actualTime);
        }

        /// <summary>
        /// Tests the scenario if Stop computes the duration of the telemetry.
        /// </summary>
        [TestMethod]
        public void OperationTelemetryStopComputesDurationAfterStart()
        {
            var telemetry = new DependencyTelemetry();
            telemetry.Start();
            Thread.Sleep(2000);
            Assert.Equal(TimeSpan.Zero, telemetry.Duration);
            telemetry.Stop();
            Assert.True(telemetry.Duration.TotalMilliseconds > 0);
        }

        /// <summary>
        /// Tests the scenario if Stop computes assigns current time to start time and time stamp and assigns 0 to duration without start().
        /// </summary>
        [TestMethod]
        public void OperationTelemetryStopAssignsCurrentTimeAsStartTimeAndTimeStampWithoutStart()
        {
            var telemetry = new DependencyTelemetry();
            telemetry.Stop();
            Assert.NotEqual(DateTimeOffset.MinValue, telemetry.Timestamp);
            Assert.Equal(telemetry.Duration, TimeSpan.Zero);
        }

        /// <summary>
        /// Tests the scenario if Stop computes the duration of the telemetry when timestamps are supplied to Start and Stop.
        /// </summary>
        [TestMethod]
        public void OperationTelemetryStopWithTimestampComputesDurationAfterStartWithTimestamp()
        {
            var telemetry = new DependencyTelemetry();

            long startTime = 123456789012345L;
            long ticksInOneSecond = Stopwatch.Frequency;
            long stopTime = startTime + ticksInOneSecond;

            telemetry.Start(timestamp: startTime);
            telemetry.Stop(timestamp: stopTime);

            Assert.Equal(TimeSpan.FromSeconds(1), telemetry.Duration);
        }

        /// <summary>
        /// Tests the scenario if Stop assigns the duration to zero when a timestamp is supplied by Start is not called.
        /// </summary>
        [TestMethod]
        public void OperationTelemetryStopWithTimestampAssignsDurationZeroWithoutStart()
        {
            var telemetry = new DependencyTelemetry();
            telemetry.Stop(timestamp: 123456789012345L); // timestamp is ignored because Start was not called

            Assert.Equal(TimeSpan.Zero, telemetry.Duration);
        }

        /// <summary>
        /// Tests the scenario if durations can be recorded more precisely than 1ms
        /// </summary>
        [TestMethod]
        public void OperationTelemetryCanRecordPreciseDurations()
        {
            var telemetry = new DependencyTelemetry();

            long startTime = Stopwatch.GetTimestamp();
            telemetry.Start(timestamp: startTime);

            // Note: Do not use TimeSpan.FromSeconds because it rounds to the nearest millisecond.
            var expectedDuration = TimeSpan.Parse("00:00:00.1234560");

            // Ensure we choose a time that has a fractional (non-integral) number of milliseconds
            Assert.NotEqual(Math.Round(expectedDuration.TotalMilliseconds), expectedDuration.TotalMilliseconds);

            double durationInStopwatchTicks = Stopwatch.Frequency * expectedDuration.TotalSeconds;

            long stopTime = (long)Math.Round(startTime + durationInStopwatchTicks);
            telemetry.Stop(timestamp: stopTime);

            if (Stopwatch.Frequency == TimeSpan.TicksPerSecond)
            {
                // In this case, the times should match exactly.
                Assert.Equal(expectedDuration, telemetry.Duration);
            }
            else
            {
                // There will be a difference, but it should be less than
                // 1 microsecond (10 ticks)
                var difference = (telemetry.Duration - expectedDuration).Duration();
                Assert.True(difference.Ticks < 10);
            }
        }
    }
}
