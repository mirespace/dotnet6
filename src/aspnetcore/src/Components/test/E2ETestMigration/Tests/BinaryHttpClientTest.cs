// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BasicTestApp.HttpClientTest;
using Microsoft.AspNetCore.BrowserTesting;
using Microsoft.AspNetCore.Components.E2ETest.Infrastructure.ServerFixtures;
using Microsoft.AspNetCore.Components.E2ETest.Infrastructure;
using Microsoft.AspNetCore.Testing;
using PlaywrightSharp;
using TestServer;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Components.E2ETest.Tests
{
    public class BinaryHttpClientTest : ComponentBrowserTestBase,
        IClassFixture<BasicTestAppServerSiteFixture<CorsStartup>>,
        IClassFixture<BlazorWasmTestAppFixture<BasicTestApp.Program>>
    {
        private readonly BlazorWasmTestAppFixture<BasicTestApp.Program> _devHostServerFixture;
        readonly ServerFixture _apiServerFixture;

        protected override Type TestComponent { get; } = typeof(BinaryHttpRequestsComponent);

        public BinaryHttpClientTest(
            BlazorWasmTestAppFixture<BasicTestApp.Program> devHostServerFixture,
            BasicTestAppServerSiteFixture<CorsStartup> apiServerFixture,
            ITestOutputHelper output)
            : base(output)
        {
            _devHostServerFixture = devHostServerFixture;
            _devHostServerFixture.PathBase = "/subdir";
            _apiServerFixture = apiServerFixture;
            MountUri = _devHostServerFixture.RootUri + "subdir";
        }

        [QuarantinedTest("New experimental test that need bake time.")]
        [ConditionalTheory]
        [InlineData(BrowserKind.Chromium)]
        [InlineData(BrowserKind.Firefox)]
        [InlineData(BrowserKind.Webkit)]
        // NOTE: BrowserKind argument must be first
        public async Task CanSendAndReceiveBytes(BrowserKind browserKind)
        {
            if (ShouldSkip(browserKind)) 
            {
                return;
            }

            var targetUri = new Uri(_apiServerFixture.RootUri, "/subdir/api/data");
            await TestPage.TypeAsync("#request-uri", targetUri.AbsoluteUri);
            await TestPage.ClickAsync("#send-request");

            var status = await TestPage.GetTextContentAsync("#response-status");
            var statusText = await TestPage.GetTextContentAsync("#response-status-text");
            var testOutcome = await TestPage.GetTextContentAsync("#test-outcome");

            Assert.Equal("OK", status);
            Assert.Equal("OK", statusText);
            Assert.Equal("", testOutcome);
        }
    }
}
