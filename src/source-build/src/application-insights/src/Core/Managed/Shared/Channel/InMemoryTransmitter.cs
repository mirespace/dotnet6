﻿// <copyright file="InMemoryTransmitter.cs" company="Microsoft">
// Copyright © Microsoft. All Rights Reserved.
// </copyright>

namespace Microsoft.ApplicationInsights.Channel
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Extensibility.Implementation;
    using Extensibility.Implementation.Tracing;

    /// <summary>
    /// A transmitter that will immediately send telemetry over HTTP. 
    /// Telemetry items are being sent when Flush is called, or when the buffer is full (An OnFull "event" is raised) or every 30 seconds. 
    /// </summary>
    internal class InMemoryTransmitter : IDisposable
    {
        private readonly TelemetryBuffer buffer;

        /// <summary>
        /// A lock object to serialize the sending calls from Flush, OnFull event and the Runner.  
        /// </summary>
        private object sendingLockObj = new object();
        private AutoResetEvent startRunnerEvent;
        private bool enabled = true;
        
        /// <summary>
        /// The number of times this object was disposed.
        /// </summary>
        private int disposeCount = 0;

        private TimeSpan sendingInterval = TimeSpan.FromSeconds(30);
        private Uri endpointAddress = new Uri(Constants.TelemetryServiceEndpoint);
                
        internal InMemoryTransmitter(TelemetryBuffer buffer)
        {
            this.buffer = buffer;
            this.buffer.OnFull = this.OnBufferFull;

            // Starting the Runner
            Task.Factory.StartNew(this.Runner, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .ContinueWith(
                    task => 
                    {
                        string msg = string.Format(CultureInfo.InvariantCulture, "InMemoryTransmitter: Unhandled exception in Runner: {0}", task.Exception);
                        CoreEventSource.Log.LogVerbose(msg);
                    }, 
                    TaskContinuationOptions.OnlyOnFaulted);
        }

        internal Uri EndpointAddress
        {
            get { return this.endpointAddress; }
            set { Property.Set(ref this.endpointAddress, value); }
        }

        internal TimeSpan SendingInterval
        {
            get { return this.sendingInterval; }
            set { this.sendingInterval = value; }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Flushes the in-memory buffer and sends it.
        /// </summary>
        internal void Flush(TimeSpan timeout)
        {
#if !NETSTANDARD1_3
            SdkInternalOperationsMonitor.Enter();
            try
            {
#endif
                this.DequeueAndSend(timeout);
#if !NETSTANDARD1_3
            }
            finally
            {
                SdkInternalOperationsMonitor.Exit();
            }
#endif
        }

        /// <summary>
        /// Flushes the in-memory buffer and sends the telemetry items in <see cref="sendingInterval"/> intervals or when 
        /// <see cref="startRunnerEvent" /> is set.
        /// </summary>
        private void Runner()
        {
#if !NETSTANDARD1_3
            SdkInternalOperationsMonitor.Enter();
            try
            {
#endif
                using (this.startRunnerEvent = new AutoResetEvent(false))
                {
                    while (this.enabled)
                    {
                        // Pulling all items from the buffer and sending as one transmission.
                        this.DequeueAndSend(timeout: default(TimeSpan)); // when default(TimeSpan) is provided, value is ignored and default timeout of 100 sec is used

                        // Waiting for the flush delay to elapse
                        this.startRunnerEvent.WaitOne(this.sendingInterval);
                    }
                }
#if !NETSTANDARD1_3
            }
            finally
            {
                SdkInternalOperationsMonitor.Exit();
            }
#endif
        }

        /// <summary>
        /// Happens when the in-memory buffer is full. Flushes the in-memory buffer and sends the telemetry items.
        /// </summary>
        private void OnBufferFull()
        {
            this.startRunnerEvent.Set();
            CoreEventSource.Log.LogVerbose("StartRunnerEvent set as Buffer is full.");
        }

        /// <summary>
        /// Flushes the in-memory buffer and send it.
        /// </summary>
        private void DequeueAndSend(TimeSpan timeout)
        {
            lock (this.sendingLockObj)
            {
                IEnumerable<ITelemetry> telemetryItems = this.buffer.Dequeue();
                try
                {
                    // send request
                    this.Send(telemetryItems, timeout).Wait();
                }
                catch (Exception e)
                {
                    CoreEventSource.Log.FailedToSend(e.Message);
                }
            }
        }

        /// <summary>
        /// Serializes a list of telemetry items and sends them.
        /// </summary>
        private Task Send(IEnumerable<ITelemetry> telemetryItems, TimeSpan timeout)
        {
            byte[] data = null;

            if (telemetryItems != null)
            {
                data = JsonSerializer.Serialize(telemetryItems);
            }

            if (data == null || data.Length == 0)
            {
                CoreEventSource.Log.LogVerbose("No Telemetry Items passed to Enqueue");
#if NET40
                return TaskEx.FromResult<object>(null);
#else
                return Task.FromResult<object>(null);
#endif
            }

            var transmission = new Transmission(this.endpointAddress, data, JsonSerializer.ContentType, JsonSerializer.CompressionType, timeout);

            return transmission.SendAsync();
        }

        private void Dispose(bool disposing)
        {
            if (Interlocked.Increment(ref this.disposeCount) == 1)
            {
                // Stops the runner loop.
                this.enabled = false;

                if (this.startRunnerEvent != null)
                {
                    // Call Set to prevent waiting for the next interval in the runner.
                    try
                    {
                        this.startRunnerEvent.Set();
                    }
                    catch (ObjectDisposedException)
                    {
                        // We need to try catch the Set call in case the auto-reset event wait interval occurs between setting enabled
                        // to false and the call to Set then the auto-reset event will have already been disposed by the runner thread.
                    }
                }

                this.Flush(default(TimeSpan));
            }
        }
    }
}
