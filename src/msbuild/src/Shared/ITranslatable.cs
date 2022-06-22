﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// An interface representing an object which may be serialized by the node packet serializer.
    /// </summary>
    internal interface ITranslatable
    {
        /// <summary>
        /// Reads or writes the packet to the serializer.
        /// </summary>
        void Translate(ITranslator translator);
    }
}
