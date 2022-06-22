// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Components.E2ETest.Infrastructure.ServerFixtures
{
    public class AspNetSiteServerFixture : WebHostServerFixture
    {
        public delegate IHost BuildWebHost(string[] args);

        public Assembly ApplicationAssembly { get; set; }

        public BuildWebHost BuildWebHostMethod { get; set; }

        public AspNetEnvironment Environment { get; set; } = AspNetEnvironment.Production;

        public List<string> AdditionalArguments { get; set; } = new List<string> { "--test-execution-mode", "server" };

        protected override IHost CreateWebHost()
        {
            if (BuildWebHostMethod == null)
            {
                throw new InvalidOperationException(
                    $"No value was provided for {nameof(BuildWebHostMethod)}");
            }

            var assembly = ApplicationAssembly ?? BuildWebHostMethod.Method.DeclaringType.Assembly;
            var sampleSitePath = FindSampleOrTestSitePath(assembly.FullName);

            var host = "127.0.0.1";

            return BuildWebHostMethod(new[]
            {
                "--urls", $"http://{host}:0",
                "--contentroot", sampleSitePath,
                "--environment", Environment.ToString(),
            }.Concat(AdditionalArguments).ToArray());
        }
    }
}
