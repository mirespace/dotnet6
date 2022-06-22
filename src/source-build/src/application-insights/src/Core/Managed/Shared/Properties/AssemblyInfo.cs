﻿using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: ComVisible(false)]

[assembly: InternalsVisibleTo("Microsoft.ApplicationInsights.Core.Net40.Tests" + AssemblyInfo.PublicKey)]
[assembly: InternalsVisibleTo("Microsoft.ApplicationInsights.Core.Net45.Tests" + AssemblyInfo.PublicKey)]
[assembly: InternalsVisibleTo("Microsoft.ApplicationInsights.Core.Net46.Tests" + AssemblyInfo.PublicKey)]
[assembly: InternalsVisibleTo("Microsoft.ApplicationInsights.Core.PCL.Tests" + AssemblyInfo.PublicKey)]

[assembly: InternalsVisibleTo("Microsoft.ApplicationInsights.TelemetryChannel.Net40.Tests" + AssemblyInfo.PublicKey)]
[assembly: InternalsVisibleTo("Microsoft.ApplicationInsights.TelemetryChannel.Net45.Tests" + AssemblyInfo.PublicKey)]

internal static class AssemblyInfo
{
    // Public key; assemblies are delay signed or OSS signed
    public const string PublicKey = ", PublicKey=0024000004800000940000000602000000240000525341310004000001000100b5fc90e7027f67871e773a8fde8938c81dd402ba65b9201d60593e96c492651e889cc13f1415ebb53fac1131ae0bd333c5ee6021672d9718ea31a8aebd0da0072f25d87dba6fc90ffd598ed4da35e44c398c454307e8e33b8426143daec9f596836f97c8f74750e5975c64e2189f45def46b2a2b1247adc3652bf5c308055da9";
}