﻿namespace Microsoft.ApplicationInsights.TestFramework
{
    using System;
    using System.Collections.Generic;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.External;

    internal class StubPlatform : IPlatform
    {
        public Func<IDebugOutput> OnGetDebugOutput = () => new StubDebugOutput();
        public Func<string> OnReadConfigurationXml = () => null;
        public Func<string> OnGetMachineName = () => null;

        public string ReadConfigurationXml()
        {
            return this.OnReadConfigurationXml();
        }

        public IDebugOutput GetDebugOutput()
        {
            return this.OnGetDebugOutput();
        }

        public string GetEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }

        public string GetMachineName()
        {
            return this.OnGetMachineName();
        }
    }
}
