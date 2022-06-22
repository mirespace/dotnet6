﻿// <copyright file="TaskTimerInternal.cs" company="Microsoft">
// Copyright © Microsoft. All Rights Reserved.
// </copyright>

namespace Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel.Implementation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;
    using WindowsServer.TelemetryChannel.Implementation;

#if !NET40
    using TaskEx = System.Threading.Tasks.Task;
#endif

    /// <summary>
    /// Runs a task after a certain delay and log any error.
    /// </summary>
    internal class TaskTimerInternal : IDisposable
    {
        /// <summary>
        /// Represents an infinite time span.
        /// </summary>
        public static readonly TimeSpan InfiniteTimeSpan = new TimeSpan(0, 0, 0, 0, Timeout.Infinite);

        private TimeSpan delay = TimeSpan.FromMinutes(1);
        private CancellationTokenSource tokenSource;

        /// <summary>
        /// Gets or sets the delay before the task starts. 
        /// </summary>
        public TimeSpan Delay
        {
            get
            {
                return this.delay;
            }

            set
            {
                if ((value <= TimeSpan.Zero || value.TotalMilliseconds > int.MaxValue) && value != InfiniteTimeSpan)
                {
                    throw new ArgumentOutOfRangeException("value");
                }

                this.delay = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether value that indicates if a task has already started.
        /// </summary>
        public bool IsStarted
        {
            get { return this.tokenSource != null; }
        }

        /// <summary>
        /// Start the task.
        /// </summary>
        /// <param name="elapsed">The task to run.</param>
        public void Start(Func<Task> elapsed)
        {
            var newTokenSource = new CancellationTokenSource();

            TaskEx.Delay(this.Delay, newTokenSource.Token)
                .ContinueWith(
#if !NET40
                async previousTask =>
#else
                    previousTask =>
#endif
                    {
                        CancelAndDispose(Interlocked.CompareExchange(ref this.tokenSource, null, newTokenSource));
                        try
                        {
                            Task task = elapsed();

                            // Task may be executed synchronously
                            // It should return Task.FromResult but just in case we check for null if someone returned null
                            if (task != null)
                            {
#if !NET40
                                await task.ConfigureAwait(false);
#else
                                task.ContinueWith(
                                    userTask =>
                                    {
                                        try
                                        {
                                            userTask.RethrowIfFaulted();
                                        }
                                        catch (Exception exception)
                                        {
                                            LogException(exception);
                                        }
                                    },
                                    CancellationToken.None,
                                    TaskContinuationOptions.ExecuteSynchronously,
                                    TaskScheduler.Default);
#endif
                            }
                        }
                        catch (Exception exception)
                        {
                            LogException(exception);
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

            CancelAndDispose(Interlocked.Exchange(ref this.tokenSource, newTokenSource));
        }

        /// <summary>
        /// Cancels the current task.
        /// </summary>
        public void Cancel()
        {
            CancelAndDispose(Interlocked.Exchange(ref this.tokenSource, null));
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Log exception thrown by outer code.
        /// </summary>
        /// <param name="exception">Exception to log.</param>
        private static void LogException(Exception exception)
        {
            var aggregateException = exception as AggregateException;
            if (aggregateException != null)
            {
                aggregateException = aggregateException.Flatten();
                foreach (Exception e in aggregateException.InnerExceptions)
                {
                    TelemetryChannelEventSource.Log.LogError(e.ToInvariantString());
                }
            }

            TelemetryChannelEventSource.Log.LogError(exception.ToInvariantString());
        }

        private static void CancelAndDispose(CancellationTokenSource tokenSource)
        {
            if (tokenSource != null)
            {
                tokenSource.Cancel();
                tokenSource.Dispose();
            }
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Cancel();
            }
        }
    }
}
