// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http3;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Xunit;
using Http3SettingType = Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http3.Http3SettingType;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Tests
{
    public class Http3ConnectionTests : Http3TestBase
    {
        private static readonly KeyValuePair<string, string>[] Headers = new[]
        {
            new KeyValuePair<string, string>(HeaderNames.Method, "Custom"),
            new KeyValuePair<string, string>(HeaderNames.Path, "/"),
            new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
            new KeyValuePair<string, string>(HeaderNames.Authority, "localhost:80"),
        };

        [Fact]
        public async Task CreateRequestStream_RequestCompleted_Disposed()
        {
            var appCompletedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await Http3Api.InitializeConnectionAsync(async context =>
            {
                var buffer = new byte[16 * 1024];
                var received = 0;

                while ((received = await context.Request.Body.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await context.Response.Body.WriteAsync(buffer, 0, received);
                }

                await appCompletedTcs.Task;
            });

            await Http3Api.CreateControlStream();
            await Http3Api.GetInboundControlStream();

            var requestStream = await Http3Api.CreateRequestStream();

            var headers = new[]
            {
                new KeyValuePair<string, string>(HeaderNames.Method, "Custom"),
                new KeyValuePair<string, string>(HeaderNames.Path, "/"),
                new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
                new KeyValuePair<string, string>(HeaderNames.Authority, "localhost:80"),
            };

            await requestStream.SendHeadersAsync(headers);
            await requestStream.SendDataAsync(Encoding.ASCII.GetBytes("Hello world"), endStream: true);

            Assert.False(requestStream.Disposed);

            appCompletedTcs.SetResult();
            await requestStream.ExpectHeadersAsync();
            var responseData = await requestStream.ExpectDataAsync();
            Assert.Equal("Hello world", Encoding.ASCII.GetString(responseData.ToArray()));

            await requestStream.OnDisposedTask.DefaultTimeout();
            Assert.True(requestStream.Disposed);
        }

        [Fact]
        public async Task HEADERS_Received_ContainsExpect100Continue_100ContinueSent()
        {
            await Http3Api.InitializeConnectionAsync(async context =>
            {
                var buffer = new byte[16 * 1024];
                var received = 0;

                while ((received = await context.Request.Body.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await context.Response.Body.WriteAsync(buffer, 0, received);
                }
            });

            await Http3Api.CreateControlStream();
            await Http3Api.GetInboundControlStream();

            var requestStream = await Http3Api.CreateRequestStream();

            var expectContinueRequestHeaders = new[]
            {
                new KeyValuePair<string, string>(HeaderNames.Method, "POST"),
                new KeyValuePair<string, string>(HeaderNames.Path, "/"),
                new KeyValuePair<string, string>(HeaderNames.Authority, "127.0.0.1"),
                new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
                new KeyValuePair<string, string>(HeaderNames.Expect, "100-continue"),
            };

            await requestStream.SendHeadersAsync(expectContinueRequestHeaders);

            var frame = await requestStream.ReceiveFrameAsync();
            Assert.Equal(Http3FrameType.Headers, frame.Type);

            var continueBytesQpackEncoded = new byte[] { 0x00, 0x00, 0xff, 0x00 };
            Assert.Equal(continueBytesQpackEncoded, frame.PayloadSequence.ToArray());

            await requestStream.SendDataAsync(Encoding.ASCII.GetBytes("Hello world"), endStream: false);
            var headers = await requestStream.ExpectHeadersAsync();
            Assert.Equal("200", headers[HeaderNames.Status]);

            var responseData = await requestStream.ExpectDataAsync();
            Assert.Equal("Hello world", Encoding.ASCII.GetString(responseData.ToArray()));

            Assert.False(requestStream.Disposed, "Request is in progress and shouldn't be disposed.");

            await requestStream.SendDataAsync(Encoding.ASCII.GetBytes($"End"), endStream: true);
            responseData = await requestStream.ExpectDataAsync();
            Assert.Equal($"End", Encoding.ASCII.GetString(responseData.ToArray()));

            await requestStream.ExpectReceiveEndOfStream();
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 4)]
        [InlineData(111, 444)]
        [InlineData(512, 2048)]
        public async Task GOAWAY_GracefulServerShutdown_SendsGoAway(int connectionRequests, int expectedStreamId)
        {
            await Http3Api.InitializeConnectionAsync(_echoApplication);

            var inboundControlStream = await Http3Api.GetInboundControlStream();
            await inboundControlStream.ExpectSettingsAsync();

            for (var i = 0; i < connectionRequests; i++)
            {
                var request = await Http3Api.CreateRequestStream();
                await request.SendHeadersAsync(Headers);
                await request.EndStreamAsync();
                await request.ExpectReceiveEndOfStream();

                await request.OnStreamCompletedTask.DefaultTimeout();
            }

            // Trigger server shutdown.
            Http3Api.CloseServerGracefully();

            Assert.Null(await Http3Api.MultiplexedConnectionContext.AcceptAsync().DefaultTimeout());

            await Http3Api.WaitForConnectionStopAsync(expectedStreamId, false, expectedErrorCode: Http3ErrorCode.NoError);
        }

        [Fact]
        public async Task GOAWAY_GracefulServerShutdownWithActiveRequest_SendsMultipleGoAways()
        {
            await Http3Api.InitializeConnectionAsync(_echoApplication);

            var inboundControlStream = await Http3Api.GetInboundControlStream();
            await inboundControlStream.ExpectSettingsAsync();

            var activeRequest = await Http3Api.CreateRequestStream();
            await activeRequest.SendHeadersAsync(Headers);

            // Trigger server shutdown.
            Http3Api.CloseServerGracefully();

            await Http3Api.WaitForGoAwayAsync(false, VariableLengthIntegerHelper.EightByteLimit);

            // Request made while shutting down is rejected.
            var rejectedRequest = await Http3Api.CreateRequestStream();
            await rejectedRequest.WaitForStreamErrorAsync(Http3ErrorCode.RequestRejected);

            // End active request.
            await activeRequest.EndStreamAsync();
            await activeRequest.ExpectReceiveEndOfStream();

            // Client aborts the connection.
            Http3Api.MultiplexedConnectionContext.Abort();

            await Http3Api.WaitForConnectionStopAsync(4, false, expectedErrorCode: Http3ErrorCode.NoError);
        }

        [Theory]
        [InlineData(0x0)]
        [InlineData(0x2)]
        [InlineData(0x3)]
        [InlineData(0x4)]
        [InlineData(0x5)]
        public async Task SETTINGS_ReservedSettingSent_ConnectionError(long settingIdentifier)
        {
            await Http3Api.InitializeConnectionAsync(_echoApplication);

            var outboundcontrolStream = await Http3Api.CreateControlStream();
            await outboundcontrolStream.SendSettingsAsync(new List<Http3PeerSetting>
            {
                new Http3PeerSetting((Http3SettingType) settingIdentifier, 0) // reserved value
            });

            await Http3Api.GetInboundControlStream();

            await Http3Api.WaitForConnectionErrorAsync<Http3ConnectionErrorException>(
                ignoreNonGoAwayFrames: true,
                expectedLastStreamId: 0,
                expectedErrorCode: Http3ErrorCode.SettingsError,
                expectedErrorMessage: CoreStrings.FormatHttp3ErrorControlStreamReservedSetting($"0x{settingIdentifier.ToString("X", CultureInfo.InvariantCulture)}"));
        }

        [Theory]
        [InlineData(0, "control")]
        [InlineData(2, "encoder")]
        [InlineData(3, "decoder")]
        public async Task InboundStreams_CreateMultiple_ConnectionError(int streamId, string name)
        {
            await Http3Api.InitializeConnectionAsync(_noopApplication);

            await Http3Api.CreateControlStream(streamId);
            await Http3Api.CreateControlStream(streamId);

            await Http3Api.WaitForConnectionErrorAsync<Http3ConnectionErrorException>(
                ignoreNonGoAwayFrames: true,
                expectedLastStreamId: 0,
                expectedErrorCode: Http3ErrorCode.StreamCreationError,
                expectedErrorMessage: CoreStrings.FormatHttp3ControlStreamErrorMultipleInboundStreams(name));
        }

        [Theory]
        [InlineData(nameof(Http3FrameType.Data))]
        [InlineData(nameof(Http3FrameType.Headers))]
        [InlineData(nameof(Http3FrameType.PushPromise))]
        public async Task ControlStream_ClientToServer_UnexpectedFrameType_ConnectionError(string frameType)
        {
            await Http3Api.InitializeConnectionAsync(_noopApplication);

            var controlStream = await Http3Api.CreateControlStream();

            var f = Enum.Parse<Http3FrameType>(frameType);
            await controlStream.SendFrameAsync(f, Memory<byte>.Empty);

            await Http3Api.WaitForConnectionErrorAsync<Http3ConnectionErrorException>(
                ignoreNonGoAwayFrames: true,
                expectedLastStreamId: 0,
                expectedErrorCode: Http3ErrorCode.UnexpectedFrame,
                expectedErrorMessage: CoreStrings.FormatHttp3ErrorUnsupportedFrameOnControlStream(Http3Formatting.ToFormattedType(f)));
        }

        [Fact]
        public async Task ControlStream_ClientToServer_ClientCloses_ConnectionError()
        {
            await Http3Api.InitializeConnectionAsync(_noopApplication);

            var controlStream = await Http3Api.CreateControlStream(id: 0);
            await controlStream.SendSettingsAsync(new List<Http3PeerSetting>());

            await controlStream.EndStreamAsync();

            await Http3Api.WaitForConnectionErrorAsync<Http3ConnectionErrorException>(
                ignoreNonGoAwayFrames: true,
                expectedLastStreamId: 0,
                expectedErrorCode: Http3ErrorCode.ClosedCriticalStream,
                expectedErrorMessage: CoreStrings.Http3ErrorControlStreamClientClosedInbound);
        }

        [Fact]
        public async Task ControlStream_ServerToClient_ErrorInitializing_ConnectionError()
        {
            Http3Api.OnCreateServerControlStream = testStreamContext =>
            {
                var controlStream = new Microsoft.AspNetCore.Testing.Http3ControlStream(Http3Api, testStreamContext);

                // Make server connection error when trying to write to control stream.
                controlStream.StreamContext.Transport.Output.Complete();

                return controlStream;
            };

            await Http3Api.InitializeConnectionAsync(_noopApplication);

            Http3Api.AssertConnectionError<Http3ConnectionErrorException>(
                expectedErrorCode: Http3ErrorCode.ClosedCriticalStream,
                expectedErrorMessage: CoreStrings.Http3ControlStreamErrorInitializingOutbound);
        }

        [Fact]
        public async Task SETTINGS_MaxFieldSectionSizeSent_ServerReceivesValue()
        {
            await Http3Api.InitializeConnectionAsync(_echoApplication);

            var inboundControlStream = await Http3Api.GetInboundControlStream();
            var incomingSettings = await inboundControlStream.ExpectSettingsAsync();

            var defaultLimits = new KestrelServerLimits();
            Assert.Collection(incomingSettings,
                kvp =>
                {
                    Assert.Equal((long)Http3SettingType.MaxFieldSectionSize, kvp.Key);
                    Assert.Equal(defaultLimits.MaxRequestHeadersTotalSize, kvp.Value);
                });

            var outboundcontrolStream = await Http3Api.CreateControlStream();
            await outboundcontrolStream.SendSettingsAsync(new List<Http3PeerSetting>
            {
                new Http3PeerSetting(Http3SettingType.MaxFieldSectionSize, 100)
            });

            var maxFieldSetting = await Http3Api.ServerReceivedSettingsReader.ReadAsync().DefaultTimeout();

            Assert.Equal(Http3SettingType.MaxFieldSectionSize, maxFieldSetting.Key);
            Assert.Equal(100, maxFieldSetting.Value);
        }

        [Fact]
        [QuarantinedTest("https://github.com/dotnet/aspnetcore/issues/34685")]
        public async Task StreamPool_MultipleStreamsInSequence_PooledStreamReused()
        {
            var headers = new[]
            {
                new KeyValuePair<string, string>(HeaderNames.Method, "Custom"),
                new KeyValuePair<string, string>(HeaderNames.Path, "/"),
                new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
                new KeyValuePair<string, string>(HeaderNames.Authority, "localhost:80"),
            };

            await Http3Api.InitializeConnectionAsync(_echoApplication);

            var streamContext1 = await MakeRequestAsync(0, headers, sendData: true, waitForServerDispose: true);
            var streamContext2 = await MakeRequestAsync(1, headers, sendData: true, waitForServerDispose: true);

            Assert.Same(streamContext1, streamContext2);
        }

        [Fact]
        public async Task RequestHeaderStringReuse_MultipleStreams_KnownHeaderClearedIfNotReused()
        {
            const BindingFlags privateFlags = BindingFlags.NonPublic | BindingFlags.Instance;

            KeyValuePair<string, string>[] requestHeaders1 = new[]
            {
                new KeyValuePair<string, string>(HeaderNames.Method, "GET"),
                new KeyValuePair<string, string>(HeaderNames.Path, "/hello"),
                new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
                new KeyValuePair<string, string>(HeaderNames.Authority, "localhost:80"),
                new KeyValuePair<string, string>(HeaderNames.ContentType, "application/json")
            };

            // Note: No content-type
            KeyValuePair<string, string>[] requestHeaders2 = new[]
            {
                new KeyValuePair<string, string>(HeaderNames.Method, "GET"),
                new KeyValuePair<string, string>(HeaderNames.Path, "/hello"),
                new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
                new KeyValuePair<string, string>(HeaderNames.Authority, "localhost:80")
            };

            await Http3Api.InitializeConnectionAsync(_echoApplication);

            var streamContext1 = await MakeRequestAsync(0, requestHeaders1, sendData: true, waitForServerDispose: true);
            var http3Stream1 = (Http3Stream)streamContext1.Features.Get<IPersistentStateFeature>().State[Http3Connection.StreamPersistentStateKey];

            // Hacky but required because header references is private.
            var headerReferences1 = typeof(HttpRequestHeaders).GetField("_headers", privateFlags).GetValue(http3Stream1.RequestHeaders);
            var contentTypeValue1 = (StringValues)headerReferences1.GetType().GetField("_ContentType").GetValue(headerReferences1);

            var streamContext2 = await MakeRequestAsync(1, requestHeaders2, sendData: true, waitForServerDispose: true);
            var http3Stream2 = (Http3Stream)streamContext2.Features.Get<IPersistentStateFeature>().State[Http3Connection.StreamPersistentStateKey];

            // Hacky but required because header references is private.
            var headerReferences2 = typeof(HttpRequestHeaders).GetField("_headers", privateFlags).GetValue(http3Stream2.RequestHeaders);
            var contentTypeValue2 = (StringValues)headerReferences1.GetType().GetField("_ContentType").GetValue(headerReferences2);

            Assert.Equal("application/json", contentTypeValue1);
            Assert.Equal(StringValues.Empty, contentTypeValue2);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(500)]
        [QuarantinedTest("https://github.com/dotnet/aspnetcore/issues/34685")]
        public async Task StreamPool_VariableMultipleStreamsInSequence_PooledStreamReused(int count)
        {
            var headers = new[]
            {
                new KeyValuePair<string, string>(HeaderNames.Method, "Custom"),
                new KeyValuePair<string, string>(HeaderNames.Path, "/"),
                new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
                new KeyValuePair<string, string>(HeaderNames.Authority, "localhost:80"),
            };

            await Http3Api.InitializeConnectionAsync(_echoApplication);

            ConnectionContext first = null;
            ConnectionContext last = null;
            for (var i = 0; i < count; i++)
            {
                Logger.LogInformation($"Iteration {i}");

                var streamContext = await MakeRequestAsync(i, headers, sendData: true, waitForServerDispose: true);

                first ??= streamContext;
                last = streamContext;

                Assert.Same(first, last);
            }
        }

        [Theory]
        [InlineData(10, false)]
        [InlineData(10, true)]
        [InlineData(100, false)]
        [InlineData(100, true)]
        [InlineData(500, false)]
        [InlineData(500, true)]
        [QuarantinedTest("https://github.com/dotnet/aspnetcore/issues/34685")]
        public async Task VariableMultipleStreamsInSequence_Success(int count, bool sendData)
        {
            var headers = new[]
            {
                new KeyValuePair<string, string>(HeaderNames.Method, "Custom"),
                new KeyValuePair<string, string>(HeaderNames.Path, "/"),
                new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
                new KeyValuePair<string, string>(HeaderNames.Authority, "localhost:80"),
            };

            var requestDelegate = sendData ? _echoApplication : _noopApplication;

            await Http3Api.InitializeConnectionAsync(requestDelegate);

            for (var i = 0; i < count; i++)
            {
                Logger.LogInformation($"Iteration {i}");

                await MakeRequestAsync(i, headers, sendData, waitForServerDispose: false);
            }
        }

        private async Task<ConnectionContext> MakeRequestAsync(int index, KeyValuePair<string, string>[] headers, bool sendData, bool waitForServerDispose)
        {
            var requestStream = await Http3Api.CreateRequestStream();
            var streamContext = requestStream.StreamContext;

            await requestStream.SendHeadersAsync(headers, endStream: !sendData);

            if (sendData)
            {
                await requestStream.SendDataAsync(Encoding.ASCII.GetBytes($"Hello world {index}"));
            }

            await requestStream.ExpectHeadersAsync();

            if (sendData)
            {
                var responseData = await requestStream.ExpectDataAsync();
                Assert.Equal($"Hello world {index}", Encoding.ASCII.GetString(responseData.ToArray()));

                Assert.False(requestStream.Disposed, "Request is in progress and shouldn't be disposed.");

                await requestStream.SendDataAsync(Encoding.ASCII.GetBytes($"End {index}"), endStream: true);
                responseData = await requestStream.ExpectDataAsync();
                Assert.Equal($"End {index}", Encoding.ASCII.GetString(responseData.ToArray()));
            }

            await requestStream.ExpectReceiveEndOfStream();

            if (waitForServerDispose)
            {
                await requestStream.OnDisposedTask.DefaultTimeout();
                Assert.True(requestStream.Disposed, "Request is complete and should be disposed.");

                Logger.LogInformation($"Received notification that stream {index} disposed.");
            }

            return streamContext;
        }
    }
}
