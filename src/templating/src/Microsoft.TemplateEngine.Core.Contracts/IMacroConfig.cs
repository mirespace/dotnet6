﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Contracts
{
    // Base interface for all macro configurations
    public interface IMacroConfig
    {
        string VariableName { get; }

        string Type { get; }
    }
}
