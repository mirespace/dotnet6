﻿namespace Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing
{
    using System.Reflection;
    using Microsoft.ApplicationInsights.TestFramework;
#if NET40
    using Microsoft.Diagnostics.Tracing;
#else
    using System.Diagnostics.Tracing;
#endif
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CoreEventSourceTest
    {
        [TestMethod]
        public void MethodsAreImplementedConsistentlyWithTheirAttributes()
        {
            EventSourceTest.MethodsAreImplementedConsistentlyWithTheirAttributes(CoreEventSource.Log);
        }

        [TestMethod]
        public void LogErrorIsDoesNotHaveUserActionableKeywordToBeClearlyIndicatedInPortalUx()
        {
            Assert.AreNotEqual(CoreEventSource.Keywords.UserActionable, GetEventAttribute("LogError").Keywords & CoreEventSource.Keywords.UserActionable);
        }

        private static EventAttribute GetEventAttribute(string methodName)
        {
            MethodInfo method = typeof(CoreEventSource).GetMethod(methodName);
            return method.GetCustomAttribute<EventAttribute>();
        }
    }
}