// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Microsoft.AspNetCore.Components.E2ETest.Infrastructure.ServerFixtures
{
    public abstract class ServerFixture : IDisposable
    {
        public Uri RootUri => _rootUriInitializer.Value;

        private readonly Lazy<Uri> _rootUriInitializer;

        public ServerFixture()
        {
            _rootUriInitializer = new Lazy<Uri>(() =>
            {
                var uri = new Uri(StartAndGetRootUri());

                return uri;
            });
        }

        public abstract void Dispose();

        protected abstract string StartAndGetRootUri();

        public static string FindSampleOrTestSitePath(string projectName)
        {
            var comma = projectName.IndexOf(",", StringComparison.Ordinal);
            if (comma != -1)
            {
                projectName = projectName.Substring(0, comma);
            }

            if (string.Equals(projectName, "Components.TestServer", StringComparison.Ordinal))
            {
                projectName = "TestServer"; // This testasset doesn't match the folder name for some reason
            }
            var path = Path.Combine(AppContext.BaseDirectory, "testassets", projectName);
            if (!Directory.Exists(path))
            {
                throw new ArgumentException($"Cannot find a sample or test site directory: '{path}'.");
            }
            return path;
        }

        protected static void RunInBackgroundThread(Action action)
        {
            var isDone = new ManualResetEvent(false);

            ExceptionDispatchInfo edi = null;
            new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    edi = ExceptionDispatchInfo.Capture(ex);
                }

                isDone.Set();
            }).Start();

            if (!isDone.WaitOne(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Timed out waiting for: " + action);
            }

            if (edi != null)
            {
                throw edi.SourceException;
            }
        }
    }
}
