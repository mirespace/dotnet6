// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TestPlatform.Common.UnitTests.Logging
{
    using System;
    using Microsoft.VisualStudio.TestPlatform.Common.Logging;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TestSessionMessageLoggerTests
    {
        private TestSessionMessageLogger testSessionMessageLogger;

        private TestRunMessageEventArgs currentEventArgs;

        [TestInitialize]
        public void TestInit()
        {
            this.testSessionMessageLogger = TestSessionMessageLogger.Instance;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            TestSessionMessageLogger.Instance = null;
        }

        [TestMethod]
        public void InstanceShouldReturnALoggerInstance()
        {
            Assert.IsNotNull(this.testSessionMessageLogger);
        }

        [TestMethod]
        public void SendMessageShouldLogErrorMessages()
        {
            this.testSessionMessageLogger.TestRunMessage += OnMessage;

            var message = "Alert";
            this.testSessionMessageLogger.SendMessage(TestMessageLevel.Error, message);

            Assert.AreEqual(TestMessageLevel.Error, this.currentEventArgs.Level);
            Assert.AreEqual(message, this.currentEventArgs.Message);
        }

        [TestMethod]
        public void SendMessageShouldLogErrorAsWarningIfSpecifiedSo()
        {
            this.testSessionMessageLogger.TestRunMessage += OnMessage;
            this.testSessionMessageLogger.TreatTestAdapterErrorsAsWarnings = true;

            var message = "Alert";
            this.testSessionMessageLogger.SendMessage(TestMessageLevel.Error, message);

            Assert.AreEqual(TestMessageLevel.Warning, this.currentEventArgs.Level);
            Assert.AreEqual(message, this.currentEventArgs.Message);
        }

        private void OnMessage(object sender, TestRunMessageEventArgs e)
        {
            this.currentEventArgs = e;
        }
    }
}
