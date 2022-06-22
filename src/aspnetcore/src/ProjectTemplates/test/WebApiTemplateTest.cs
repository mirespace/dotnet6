// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Templates.Test.Helpers;
using Microsoft.AspNetCore.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Templates.Test
{
    public class WebApiTemplateTest : LoggedTest
    {
        public WebApiTemplateTest(ProjectFactoryFixture factoryFixture)
        {
            FactoryFixture = factoryFixture;
        }

        public ProjectFactoryFixture FactoryFixture { get; }

        private ITestOutputHelper _output;
        public ITestOutputHelper Output
        {
            get
            {
                if (_output == null)
                {
                    _output = new TestOutputLogger(Logger);
                }
                return _output;
            }
        }

        [ConditionalTheory]
        [SkipOnHelix("https://github.com/dotnet/aspnetcore/issues/28090", Queues = HelixConstants.Windows10Arm64 + HelixConstants.DebianArm64)]
        [InlineData("IndividualB2C", null)]
        [InlineData("IndividualB2C", new string[] { "--use-program-main" })]
        [InlineData("IndividualB2C", new string[] { "--called-api-url \"https://graph.microsoft.com\"", "--called-api-scopes user.readwrite" })]
        [InlineData("IndividualB2C", new string[] { "--use-program-main --called-api-url \"https://graph.microsoft.com\"", "--called-api-scopes user.readwrite" })]
        [InlineData("SingleOrg", null)]
        [InlineData("SingleOrg", new string[] { "--use-program-main" })]
        [InlineData("SingleOrg", new string[] { "--called-api-url \"https://graph.microsoft.com\"", "--called-api-scopes user.readwrite" })]
        [InlineData("SingleOrg", new string[] { "--use-program-main --called-api-url \"https://graph.microsoft.com\"", "--called-api-scopes user.readwrite" })]
        [InlineData("SingleOrg", new string[] { "--calls-graph" })]
        [InlineData("SingleOrg", new string[] { "--use-program-main --calls-graph" })]
        public Task WebApiTemplateCSharp_IdentityWeb_BuildsAndPublishes(string auth, string[] args) => PublishAndBuildWebApiTemplate(languageOverride: null, auth: auth, args: args);

        [Fact]
        public Task WebApiTemplateFSharp() => WebApiTemplateCore(languageOverride: "F#");

        [ConditionalFact]
        [SkipOnHelix("Cert failure, https://github.com/dotnet/aspnetcore/issues/28090", Queues = "All.OSX;" + HelixConstants.Windows10Arm64 + HelixConstants.DebianArm64)]
        public Task WebApiTemplateCSharp() => WebApiTemplateCore(languageOverride: null);

        [ConditionalFact]
        [SkipOnHelix("Cert failure, https://github.com/dotnet/aspnetcore/issues/28090", Queues = "All.OSX;" + HelixConstants.Windows10Arm64 + HelixConstants.DebianArm64)]
        public Task WebApiTemplateProgramMainCSharp() => WebApiTemplateCore(languageOverride: null, args: new [] { "--use-program-main" });

        [ConditionalTheory]
        [InlineData(false)]
        [InlineData(true)]
        [SkipOnHelix("Cert failure, https://github.com/dotnet/aspnetcore/issues/28090", Queues = "All.OSX;" + HelixConstants.Windows10Arm64 + HelixConstants.DebianArm64)]
        public async Task WebApiTemplateCSharp_WithoutOpenAPI(bool useProgramMain)
        {
            var project = await FactoryFixture.GetOrCreateProject("webapinoopenapi", Output);

            var args = useProgramMain ? new[] { "--use-program-main --no-openapi" } : new[] { "--no-openapi" };
            var createResult = await project.RunDotNetNewAsync("webapi", args: args);
            Assert.True(0 == createResult.ExitCode, ErrorMessages.GetFailedProcessMessage("create/restore", project, createResult));

            var buildResult = await project.RunDotNetBuildAsync();
            Assert.True(0 == buildResult.ExitCode, ErrorMessages.GetFailedProcessMessage("build", project, buildResult));

            using var aspNetProcess = project.StartBuiltProjectAsync();
            Assert.False(
                aspNetProcess.Process.HasExited,
                ErrorMessages.GetFailedProcessMessageOrEmpty("Run built project", project, aspNetProcess.Process));

            await aspNetProcess.AssertNotFound("swagger");
        }

        private async Task<Project> PublishAndBuildWebApiTemplate(string languageOverride, string auth, string[] args = null)
        {
            var project = await FactoryFixture.GetOrCreateProject("webapi" + (languageOverride == "F#" ? "fsharp" : "csharp") + Guid.NewGuid().ToString().Substring(0, 10).ToLowerInvariant(), Output);

            var createResult = await project.RunDotNetNewAsync("webapi", language: languageOverride, auth: auth, args: args);
            Assert.True(0 == createResult.ExitCode, ErrorMessages.GetFailedProcessMessage("create/restore", project, createResult));

            // Avoid the F# compiler. See https://github.com/dotnet/aspnetcore/issues/14022
            if (languageOverride != null)
            {
                return project;
            }

            var publishResult = await project.RunDotNetPublishAsync();
            Assert.True(0 == publishResult.ExitCode, ErrorMessages.GetFailedProcessMessage("publish", project, publishResult));

            // Run dotnet build after publish. The reason is that one uses Config = Debug and the other uses Config = Release
            // The output from publish will go into bin/Release/netcoreappX.Y/publish and won't be affected by calling build
            // later, while the opposite is not true.

            var buildResult = await project.RunDotNetBuildAsync();
            Assert.True(0 == buildResult.ExitCode, ErrorMessages.GetFailedProcessMessage("build", project, buildResult));

            return project;
        }

        private async Task WebApiTemplateCore(string languageOverride, string[] args = null)
        {
            var project = await PublishAndBuildWebApiTemplate(languageOverride, null, args);

            // Avoid the F# compiler. See https://github.com/dotnet/aspnetcore/issues/14022
            if (languageOverride != null)
            {
                return;
            }

            using (var aspNetProcess = project.StartBuiltProjectAsync())
            {
                Assert.False(
                    aspNetProcess.Process.HasExited,
                    ErrorMessages.GetFailedProcessMessageOrEmpty("Run built project", project, aspNetProcess.Process));

                await aspNetProcess.AssertOk("weatherforecast");
                await aspNetProcess.AssertOk("swagger");
                await aspNetProcess.AssertNotFound("/");
            }

            using (var aspNetProcess = project.StartPublishedProjectAsync())
            {
                Assert.False(
                    aspNetProcess.Process.HasExited,
                    ErrorMessages.GetFailedProcessMessageOrEmpty("Run published project", project, aspNetProcess.Process));


                await aspNetProcess.AssertOk("weatherforecast");
                // Swagger is only available in Development
                await aspNetProcess.AssertNotFound("swagger");
                await aspNetProcess.AssertNotFound("/");
            }
        }
    }
}
