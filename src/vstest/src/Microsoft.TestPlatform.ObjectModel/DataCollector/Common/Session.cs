// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Class identifying a session.
    /// </summary>
    [DataContract]
    public sealed class SessionId
    {
        private Guid sessionId;

        private static SessionId empty = new SessionId(Guid.Empty);

        public SessionId()
        {
            sessionId = Guid.NewGuid();
        }

        public SessionId(Guid id)
        {
            sessionId = id;
        }

        [DataMember]
        public static SessionId Empty
        {
            get { return empty; }
        }

        [DataMember]
        public Guid Id
        {
            get { return sessionId; }
        }

        public override bool Equals(object obj)
        {
            SessionId id = obj as SessionId;

            if (id == null)
            {
                return false;
            }

            return sessionId.Equals(id.sessionId);
        }

        public override int GetHashCode()
        {
            return sessionId.GetHashCode();
        }

        public override string ToString()
        {
            return sessionId.ToString("B");
        }
    }
}