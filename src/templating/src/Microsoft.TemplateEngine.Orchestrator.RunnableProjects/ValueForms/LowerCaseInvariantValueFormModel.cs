// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms
{
    internal class LowerCaseInvariantValueFormModel : IValueForm
    {
        internal LowerCaseInvariantValueFormModel()
        {
        }

        internal LowerCaseInvariantValueFormModel(string name)
        {
            Name = name;
        }

        public string Identifier => "lowerCaseInvariant";

        public string Name { get; }

        public IValueForm FromJObject(string name, JObject configuration)
        {
            return new LowerCaseInvariantValueFormModel(name);
        }

        public string Process(IReadOnlyDictionary<string, IValueForm> forms, string value)
        {
            return value.ToLowerInvariant();
        }
    }
}
