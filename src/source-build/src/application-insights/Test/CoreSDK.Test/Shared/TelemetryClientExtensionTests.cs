﻿namespace Microsoft.ApplicationInsights
{
    using System;
#if !NET40
    using System.Diagnostics;
#endif
    using System.Collections.Generic;
#if !NET40
    using System.Linq;
#endif
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Extensibility.Implementation;
    using TestFramework;

    [TestClass]
    public class TelemetryClientExtensionTests
    {
        private TelemetryClient telemetryClient;
        private List<ITelemetry> sendItems;

        [TestInitialize]
        public void TestInitialize()
        {
            var configuration = new TelemetryConfiguration();
            this.sendItems = new List<ITelemetry>();
            configuration.TelemetryChannel = new StubTelemetryChannel { OnSend = item => this.sendItems.Add(item) };
            configuration.InstrumentationKey = Guid.NewGuid().ToString();
            configuration.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());
            this.telemetryClient = new TelemetryClient(configuration);
            CallContextHelpers.RestoreOperationContext(null);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CallContextHelpers.RestoreOperationContext(null);
#if !NET40
            while (Activity.Current != null)
            {
                Activity.Current.Stop();
            }
#endif
        }

        [TestMethod]
        public void StartDependencyTrackingReturnsOperationWithSameTelemetryItem()
        {
            var operation = this.telemetryClient.StartOperation<DependencyTelemetry>(operationName: null);
            Assert.IsNotNull(operation);
            Assert.IsNotNull(operation.Telemetry);
        }

        [TestMethod]
        public void StartDependencyTrackingReturnsOperationWithInitializedOperationName()
        {
            var operation = this.telemetryClient.StartOperation<DependencyTelemetry>("TestOperationName");
            Assert.AreEqual("TestOperationName", operation.Telemetry.Name);
        }

        [TestMethod]
        public void StartDependencyTrackingReturnsOperationWithInitializedOperationId()
        {
            var operation = this.telemetryClient.StartOperation<DependencyTelemetry>("TestOperationName");
            Assert.IsNotNull(operation.Telemetry.Context.Operation.Id);
        }

        [TestMethod]
        public void StartDependencyTrackingReturnsOperationWithInitializedOperationRootId()
        {
            var operation = this.telemetryClient.StartOperation<DependencyTelemetry>("TestOperationName");
            Assert.IsNotNull(operation.Telemetry.Context.Operation.Id);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void StartDependencyTrackingThrowsExceptionWithNullTelemetryClient()
        {
            TelemetryClient tc = null;
            tc.StartOperation<DependencyTelemetry>(operationName: null);
        }

        [TestMethod]
        public void StartDependencyTrackingCreatesADependencyTelemetryItemWithTimeStamp()
        {
            var operation = this.telemetryClient.StartOperation<DependencyTelemetry>(operationName: null);
            Assert.AreNotEqual(operation.Telemetry.Timestamp, DateTimeOffset.MinValue);
        }

#if NET40
        [TestMethod]
        public void StartDependencyTrackingAddsOperationContextStoreToCallContext()
        {
            Assert.IsNull(CallContextHelpers.GetCurrentOperationContext());
            var operation = this.telemetryClient.StartOperation<DependencyTelemetry>(operationName: null);
            Assert.IsNotNull(CallContextHelpers.GetCurrentOperationContext());
        }
#else
        [TestMethod]
        public void StartDependencyTrackingAddsOperationContextStoreToCurrentActivity()
        {
            Assert.IsNull(Activity.Current);
            var operation = this.telemetryClient.StartOperation<DependencyTelemetry>(operationName: null);
            Assert.IsNotNull(Activity.Current);
        }
#endif

#if NET40
        [TestMethod]
        public void UsingSendsTelemetryAndDisposesOperationItem()
        {
            Assert.IsNull(CallContextHelpers.GetCurrentOperationContext());
            using (var operation = this.telemetryClient.StartOperation<DependencyTelemetry>(operationName: null))
            {
            }

            Assert.IsNull(CallContextHelpers.GetCurrentOperationContext());
            Assert.AreEqual(1, this.sendItems.Count);
        }
#else 
        [TestMethod]
        public void UsingSendsTelemetryAndDisposesOperationItem()
        {
            Assert.IsNull(Activity.Current);
            using (var operation = this.telemetryClient.StartOperation<DependencyTelemetry>(operationName: null))
            {
            }

            Assert.IsNull(Activity.Current);
            Assert.AreEqual(1, this.sendItems.Count);
        }
#endif

#if NET40
        [TestMethod]
        public void UsingWithStopOperationSendsTelemetryAndDisposesOperationItemOnlyOnce()
        {
            Assert.IsNull(CallContextHelpers.GetCurrentOperationContext());
            using (var operation = this.telemetryClient.StartOperation<DependencyTelemetry>(operationName: null))
            {
                this.telemetryClient.StopOperation(operation);
            }

            Assert.IsNull(CallContextHelpers.GetCurrentOperationContext());
            Assert.AreEqual(1, this.sendItems.Count);
        }
#else
        [TestMethod]
        public void UsingWithStopOperationSendsTelemetryAndDisposesOperationItemOnlyOnce()
        {
            Assert.IsNull(Activity.Current);
            using (var operation = this.telemetryClient.StartOperation<DependencyTelemetry>(operationName: null))
            {
                this.telemetryClient.StopOperation(operation);
            }

            Assert.IsNull(Activity.Current);
            Assert.AreEqual(1, this.sendItems.Count);
        }
#endif

#if NET40
        [TestMethod]
        public void StartDependencyTrackingHandlesMultipleContextStoresInCallContext()
        {
            var operation = this.telemetryClient.StartOperation<DependencyTelemetry>("OperationName") as OperationHolder<DependencyTelemetry>;
            var parentContextStore = CallContextHelpers.GetCurrentOperationContext();
            Assert.AreEqual(operation.Telemetry.Id, parentContextStore.ParentOperationId);
            Assert.AreEqual(operation.Telemetry.Context.Operation.Name, parentContextStore.RootOperationName);

            var childOperation = this.telemetryClient.StartOperation<DependencyTelemetry>("OperationName") as OperationHolder<DependencyTelemetry>;
            var childContextStore = CallContextHelpers.GetCurrentOperationContext();
            Assert.AreEqual(childOperation.Telemetry.Id, childContextStore.ParentOperationId);
            Assert.AreEqual(childOperation.Telemetry.Context.Operation.Name, childContextStore.RootOperationName);

            Assert.IsNull(operation.ParentContext);
            Assert.AreEqual(parentContextStore, childOperation.ParentContext);

            this.telemetryClient.StopOperation(childOperation);
            Assert.AreEqual(parentContextStore, CallContextHelpers.GetCurrentOperationContext());
            this.telemetryClient.StopOperation(operation);
            Assert.IsNull(CallContextHelpers.GetCurrentOperationContext());
        }
#else
        [TestMethod]
        public void StartDependencyTrackingHandlesMultipleContextStoresInCurrentActivity()
        {
            var operation = this.telemetryClient.StartOperation<DependencyTelemetry>("OperationName") as OperationHolder<DependencyTelemetry>;
            var currentActivity = Activity.Current;
            Assert.AreEqual(operation.Telemetry.Id, currentActivity.Id);
            Assert.AreEqual(operation.Telemetry.Context.Operation.Name, this.GetOperationName(currentActivity));

            var childOperation = this.telemetryClient.StartOperation<DependencyTelemetry>("OperationName") as OperationHolder<DependencyTelemetry>;
            var childActivity = Activity.Current;
            Assert.AreEqual(childOperation.Telemetry.Id, childActivity.Id);
            Assert.AreEqual(childOperation.Telemetry.Context.Operation.Name, this.GetOperationName(currentActivity));

            Assert.IsNull(currentActivity.Parent);
            Assert.AreEqual(currentActivity, childActivity.Parent);

            this.telemetryClient.StopOperation(childOperation);
            Assert.AreEqual(currentActivity, Activity.Current);
            this.telemetryClient.StopOperation(operation);
            Assert.IsNull(Activity.Current);
        }
#endif

        [TestMethod]
        public void StopOperationDoesNotFailOnNullOperation()
        {
            this.telemetryClient.StopOperation<DependencyTelemetry>(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void StopDependencyTrackingThrowsExceptionWithNullTelemetryClient()
        {
            var operationItem = new OperationHolder<DependencyTelemetry>(this.telemetryClient, new DependencyTelemetry());
            TelemetryClient tc = null;
            tc.StopOperation(operationItem);
        }

        [TestMethod]
        public void StopOperationDoesNotThrowExceptionIfParentOpertionIsStoppedBeforeChildOperation()
        {
            using (var parentOperation = this.telemetryClient.StartOperation<DependencyTelemetry>("operationName"))
            {
                using (var childOperation = this.telemetryClient.StartOperation<DependencyTelemetry>("operationName"))
                {
                    this.telemetryClient.StopOperation(parentOperation);
                }
            }
        }

        [TestMethod]
        public void StopOperationWorksFineWithNestedOperations()
        {
            using (var parentOperation = this.telemetryClient.StartOperation<DependencyTelemetry>("operationName"))
            {
                using (var childOperation = this.telemetryClient.StartOperation<DependencyTelemetry>("operationName"))
                {
                    this.telemetryClient.StopOperation(childOperation);
                }

                this.telemetryClient.StopOperation(parentOperation);
            }

            Assert.AreEqual(2, this.sendItems.Count);
        }

#if NET40
        [TestMethod]
        public void StartDependencyTrackingStoresTheArgumentOperationNameInContext()
        {
            var operation = this.telemetryClient.StartOperation<DependencyTelemetry>("TestOperationName");
            Assert.AreEqual("TestOperationName", CallContextHelpers.GetCurrentOperationContext().RootOperationName);
        }
#else
        [TestMethod]
        public void StartDependencyTrackingStoresTheArgumentOperationNameInCurrentActivity()
        {
            var operation = this.telemetryClient.StartOperation<DependencyTelemetry>("TestOperationName");
            Assert.AreEqual("TestOperationName", this.GetOperationName(Activity.Current));
        }
#endif

        [TestMethod]
        public void ContextPropagatesThroughNestedOperations()
        {
            using (this.telemetryClient.StartOperation<RequestTelemetry>("OuterRequest"))
            {
                using (this.telemetryClient.StartOperation<DependencyTelemetry>("DependentCall"))
                {
                }
            }

            Assert.AreEqual(2, this.sendItems.Count);

            var requestTelmetry = (RequestTelemetry)this.sendItems[1];
            var dependentTelmetry = (DependencyTelemetry)this.sendItems[0];
            Assert.IsNull(requestTelmetry.Context.Operation.ParentId);
            Assert.AreEqual(requestTelmetry.Id, dependentTelmetry.Context.Operation.ParentId);
            Assert.AreEqual(requestTelmetry.Context.Operation.Id, dependentTelmetry.Context.Operation.Id);
            Assert.AreEqual(requestTelmetry.Context.Operation.Name, dependentTelmetry.Context.Operation.Name);
        }

        [TestMethod]
        public void StartOperationCanOverrideOperationId()
        {
            using (this.telemetryClient.StartOperation<RequestTelemetry>("Request", "HOME"))
            {
            }

            Assert.AreEqual(1, this.sendItems.Count);

            var requestTelmetry = (RequestTelemetry)this.sendItems[0];
            Assert.IsNull(requestTelmetry.Context.Operation.ParentId);
            Assert.AreEqual("HOME", requestTelmetry.Context.Operation.Id);
        }

        [TestMethod]
        public void StartOperationCanOverrideRootAndParentOperationId()
        {
            using (this.telemetryClient.StartOperation<RequestTelemetry>("Request", operationId: "ROOT", parentOperationId: "PARENT"))
            {
            }

            Assert.AreEqual(1, this.sendItems.Count);

            var requestTelmetry = (RequestTelemetry)this.sendItems[0];
            Assert.AreEqual("PARENT", requestTelmetry.Context.Operation.ParentId);
            Assert.AreEqual("ROOT", requestTelmetry.Context.Operation.Id);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void StartOperationThrowsOnNullOperationTelemetry()
        {
            this.telemetryClient.StartOperation<RequestTelemetry>(operationTelemetry: null);
        }

        [TestMethod]
        public void StartOperationWithOperationTelemetrySetsOperationHolderTelemetry()
        {
            var requestTelemetry = new RequestTelemetry
            {
                Name = "REQUEST",
                Id = "1"
            };

            using (var operationHolder = this.telemetryClient.StartOperation<RequestTelemetry>(requestTelemetry))
            {
                Assert.AreEqual(requestTelemetry, operationHolder.Telemetry);
            }

            Assert.AreEqual(1, this.sendItems.Count);
            Assert.AreEqual(requestTelemetry, this.sendItems[0]);
        }

#if !NET40
        private string GetOperationName(Activity activity)
        {
            return activity.Tags.FirstOrDefault(tag => tag.Key == "OperationName").Value;
        }
#endif
    }
}
