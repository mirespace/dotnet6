﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace vstest.console.UnitTests.Processors
{
    using Microsoft.VisualStudio.TestPlatform.Client.RequestHelper;
    using Microsoft.VisualStudio.TestPlatform.CommandLine;
    using Microsoft.VisualStudio.TestPlatform.CommandLine.Processors;
    using Microsoft.VisualStudio.TestPlatform.Common.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.Utilities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System.Collections.Generic;

    [TestClass]
    public class UseVsixExtensionsArgumentProcessorTests
    {
        private const string DefaultRunSettings = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n<RunSettings>\r\n  <DataCollectionRunSettings>\r\n    <DataCollectors >{0}</DataCollectors>\r\n  </DataCollectionRunSettings>\r\n</RunSettings>";
        private const string DeprecationMessage = @"/UseVsixExtensions is getting deprecated. Please use /TestAdapterPath instead.";
        private Mock<ITestRequestManager> testRequestManager;
        private Mock<IVSExtensionManager> extensionManager;
        private Mock<IOutput> output;
        private UseVsixExtensionsArgumentExecutor executor;

        public UseVsixExtensionsArgumentProcessorTests()
        {
            this.testRequestManager = new Mock<ITestRequestManager>();
            this.extensionManager = new Mock<IVSExtensionManager>();
            this.output = new Mock<IOutput>();
            this.executor = new UseVsixExtensionsArgumentExecutor(CommandLineOptions.Instance, this.testRequestManager.Object, this.extensionManager.Object, this.output.Object);
        }

        [TestMethod]
        public void GetMetadataShouldReturnUseVsixExtensionsArgumentProcessorCapabilities()
        {
            var processor = new UseVsixExtensionsArgumentProcessor();
            Assert.IsTrue(processor.Metadata.Value is UseVsixExtensionsArgumentProcessorCapabilities);
        }

        [TestMethod]
        public void GetExecuterShouldReturnUseVsixExtensionsArgumentProcessorCapabilities()
        {
            var processor = new UseVsixExtensionsArgumentProcessor();
            Assert.IsTrue(processor.Executor.Value is UseVsixExtensionsArgumentExecutor);
        }

        #region UseVsixExtensionsArgumentProcessorCapabilities tests

        [TestMethod]
        public void CapabilitiesShouldReturnAppropriateProperties()
        {
            var capabilities = new UseVsixExtensionsArgumentProcessorCapabilities();

            Assert.AreEqual("/UseVsixExtensions", capabilities.CommandName);
            Assert.IsNull(capabilities.HelpContentResourceName);
            Assert.AreEqual(HelpContentPriority.None, capabilities.HelpPriority);
            Assert.IsFalse(capabilities.IsAction);
            Assert.AreEqual(ArgumentProcessorPriority.AutoUpdateRunSettings, capabilities.Priority);

            Assert.IsFalse(capabilities.AllowMultiple);
            Assert.IsFalse(capabilities.AlwaysExecute);
            Assert.IsFalse(capabilities.IsSpecialCommand);
        }

        #endregion

        #region UseVsixExtensionsArgumentExecutor tests

        [TestMethod]
        public void InitializeShouldThrowExceptionIfArgumentIsNull()
        {
            var message = Assert.ThrowsException<CommandLineException>(() => this.executor.Initialize(null)).Message;
            Assert.AreEqual(@"The /UseVsixExtensions parameter requires a value. If 'true', the installed VSIX extensions (if any) will be used in the test run. If false, they will be ignored.   Example:  /UseVsixExtensions:true", message);
        }

        [TestMethod]
        public void InitializeShouldThrowExceptionIfArgumentIsInvalid()
        {
            var invalidArg = "Foo";

            var message = Assert.ThrowsException<CommandLineException>(() => this.executor.Initialize(invalidArg)).Message;
            Assert.AreEqual(@"Argument Foo is not expected in the 'UseVsixExtensions' command. Specify the command indicating whether the vsix extensions should be used or skipped (Example: vstest.console.exe myTests.dll /UseVsixExtensions:true) and try again.", message);
        }

        [TestMethod]
        public void InitializeForArgumentEqualTrueShouldCallTestRequestManagerInitializeExtensions()
        {
            var extensions = new List<string> { "T1.dll", "T2.dll" };
            this.extensionManager.Setup(em => em.GetUnitTestExtensions()).Returns(extensions);

            this.executor.Initialize("true");

            this.output.Verify(o => o.WriteLine(DeprecationMessage, OutputLevel.Warning), Times.Once);
            this.extensionManager.Verify(em => em.GetUnitTestExtensions(), Times.Once);
            this.testRequestManager.Verify(trm => trm.InitializeExtensions(extensions, true), Times.Once);
        }

        [TestMethod]
        public void InitializeForArgumentEqualfalseShouldNotCallTestRequestManagerInitializeExtensions()
        {
            this.executor.Initialize("false");

            this.output.Verify(o => o.WriteLine(DeprecationMessage, OutputLevel.Warning), Times.Once);
            this.extensionManager.Verify(em => em.GetUnitTestExtensions(), Times.Never);
            this.testRequestManager.Verify(trm => trm.InitializeExtensions(It.IsAny<IEnumerable<string>>(), true), Times.Never);
        }

        #endregion
    }
}
