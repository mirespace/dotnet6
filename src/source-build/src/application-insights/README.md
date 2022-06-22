![Build Status](https://mseng.visualstudio.com/DefaultCollection/_apis/public/build/definitions/96a62c4a-58c2-4dbb-94b6-5979ebc7f2af/1822/badge)
[![Nuget](https://img.shields.io/nuget/vpre/Microsoft.ApplicationInsights.svg)](https://www.nuget.org/packages/Microsoft.ApplicationInsights/)
[![codecov.io](https://codecov.io/github/Microsoft/ApplicationInsights-dotnet/coverage.svg?branch=develop)](https://codecov.io/github/Microsoft/ApplicationInsights-dotnet?branch=develop)

# Application Insights for .NET

This repository has code for the core .NET SDK for Application Insights. [Application Insights][AILandingPage] is a service that allows developers ensure their application are available, performing, and succeeding. This SDK provides the core ability to send all Application Insights types from any .NET project. 

## Getting Started

If developing for a .Net project that is supported by one of our platform specific packages, [Web][WebGetStarted] or [Windows Apps][WinAppGetStarted], we strongly recommend to use one of those packages instead of this core library. If your project does not fall into one of those platforms you can use this library for any .Net code. This library should have no dependencies outside of the .Net framework. If you are building a [Desktop][DesktopGetStarted] or any other .Net project type this library will enable you to utilize Application Insights.

### Get an Instrumentation Key

To use the Application Insights SDK you will need to provide it with an Instrumentation Key which can be [obtained from the portal][AIKey]. This Instrumentation Key will identify all the data flowing from your application instances as belonging to your account and specific application.

### Add the SDK library

We recommend consuming the library as a NuGet package. Make sure to look for the [Microsoft.ApplicationInsights][NuGetCore] package. Use the NuGet package manager to add a reference to your application code. 

### Initialize a TelemetryClient

The `TelemetryClient` object is the primary root object for the library. Almost all functionality around telemetry sending is located on this object. You must initialize an instance of this object and populate it with your Instrumentation Key to identify your data.

```C#
using Microsoft.ApplicationInsights;

var tc = new TelemetryClient();
tc.InstrumentationKey = "INSERT YOUR KEY";
```

### Use the TelemetryClient to send telemetry

This "core" library does not provide any automatic telemetry collection or any automatic meta-data properties. You can populate common context on the `TelemetryClient.context` property which will be automatically attached to each telemetry item sent. You can also attach additional property data to each telemetry item sent. The `TelemetryClient` also exposes a number of `Track...()` methods that can be used to send all core telemetry types understood by the Application Insights service. Some example use cases are shown below.

```C#
tc.Context.User.Id = Environment.GetUserName(); // This is probably a bad idea from a PII perspective.
tc.Context.Device.OperatingSystem = Environment.OSVersion.ToString();

tc.TrackPageView("Form1");

tc.TrackEvent("PurchaseOrderSubmitted", new Dictionary<string, string>() { {"CouponCode", "JULY2015" } }, new Dictionary<string, double>() { {"OrderTotal", 68.99 }, {"ItemsOrdered", 5} });
	
try
{
	...
}
catch(Exception e)
{
	tc.TrackException(e);
}
``` 

### Ensure you don't lose telemetry

This library makes use of the InMemoryChannel to send telemetry data. This is a very lightweight channel implementation. It stores all telemetry to an in-memory queue and batches and sends telemetry. As a result, if the process is terminated suddenly, you could lose telemetry that is stored in the queue but not yet sent. It is recommended to track the closing of your process and call the `TelemetryClient.Flush()` method to ensure no telemetry is lost.

### Full API Overview

Read about [how to use the API and see the results in the portal][api-overview].

## Branches

- [master][master] contains the *latest* published release located on [NuGet][NuGetCore].
- [develop][develop] contains the code for the *next* release. 

## Contributing

We strongly welcome and encourage contributions to this project. Please read the [contributor's guide][ContribGuide] located in the ApplicationInsights-Home repository. If making a large change we request that you open an [issue][GitHubIssue] first. We follow the [Git Flow][GitFlow] approach to branching. 

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

[AILandingPage]: http://azure.microsoft.com/services/application-insights/
[api-overview]: https://azure.microsoft.com/documentation/articles/app-insights-api-custom-events-metrics/
[ContribGuide]: https://github.com/Microsoft/ApplicationInsights-Home/blob/master/CONTRIBUTING.md
[GitFlow]: http://nvie.com/posts/a-successful-git-branching-model/
[GitHubIssue]: https://github.com/Microsoft/ApplicationInsights-dotnet/issues
[master]: https://github.com/Microsoft/ApplicationInsights-dotnet/tree/master
[develop]: https://github.com/Microsoft/ApplicationInsights-dotnet/tree/development
[NuGetCore]: https://www.nuget.org/packages/Microsoft.ApplicationInsights
[WebGetStarted]: https://azure.microsoft.com/documentation/articles/app-insights-start-monitoring-app-health-usage/
[WinAppGetStarted]: https://azure.microsoft.com/documentation/articles/app-insights-windows-get-started/
[DesktopGetStarted]: https://azure.microsoft.com/documentation/articles/app-insights-windows-desktop/
[AIKey]: https://github.com/Microsoft/ApplicationInsights-Home/wiki#getting-an-application-insights-instrumentation-key
