﻿// <copyright file="Tags.cs" company="Microsoft">
// Copyright © Microsoft. All Rights Reserved.
// </copyright>

namespace Microsoft.ApplicationInsights.Extensibility.Implementation.External
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Base class for tags backed context.
    /// </summary>
    internal static class Tags
    {
        internal static bool? GetTagBoolValueOrNull(this IDictionary<string, string> tags, string tagKey)
        {
            string tagValue = GetTagValueOrNull(tags, tagKey);
            if (string.IsNullOrEmpty(tagValue))
            {
                return null;
            }

            return bool.Parse(tagValue);
        }

        internal static bool GetTagBoolValueOrDefault(this IDictionary<string, string> tags, string tagKey)
        {
            string tagValue = GetTagValueOrNull(tags, tagKey);
            if (string.IsNullOrEmpty(tagValue))
            {
                return default(bool);
            }

            return bool.Parse(tagValue);
        }

        internal static DateTimeOffset? GetTagDateTimeOffsetValueOrNull(this IDictionary<string, string> tags, string tagKey)
        {
            string tagValue = GetTagValueOrNull(tags, tagKey);
            if (string.IsNullOrEmpty(tagValue))
            {
                return null;
            }

            return DateTimeOffset.Parse(tagValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        internal static int? GetTagIntValueOrNull(this IDictionary<string, string> tags, string tagKey)
        {
            string tagValue = GetTagValueOrNull(tags, tagKey);
            if (string.IsNullOrEmpty(tagValue))
            {
                return null;
            }

            return int.Parse(tagValue, CultureInfo.InvariantCulture);
        }

        internal static int GetTagIntValueOrDefault(this IDictionary<string, string> tags, string tagKey)
        {
            string tagValue = GetTagValueOrNull(tags, tagKey);
            if (string.IsNullOrEmpty(tagValue))
            {
                return default(int);
            }

            return int.Parse(tagValue, CultureInfo.InvariantCulture);
        }

        internal static long GetTagLongValueOrDefault(this IDictionary<string, string> tags, string tagKey)
        {
            string tagValue = GetTagValueOrNull(tags, tagKey);
            if (string.IsNullOrEmpty(tagValue))
            {
                return default(long);
            }

            return long.Parse(tagValue, CultureInfo.InvariantCulture);
        }

        internal static Guid GetTagGuidValueOrDefault(this IDictionary<string, string> tags, string tagKey)
        {
            string tagValue = GetTagValueOrNull(tags, tagKey);
            if (string.IsNullOrEmpty(tagValue))
            {
                return new Guid();
            }

            return new Guid(tagValue);
        }

        internal static void SetBoolValueOrRemove(this IDictionary<string, string> tags, string tagKey, bool? tagValue)
        {
            if (tagValue == null)
            {
                SetTagValueOrRemove(tags, tagKey, tagValue);
            }
            else
            {
                string tagStringValue = tagValue.Value.ToString();
                SetTagValueOrRemove(tags, tagKey, tagStringValue);
            }
        }

        internal static void SetIntValueOrRemove(this IDictionary<string, string> tags, string tagKey, int? tagValue)
        {
            if (tagValue == null)
            {
                SetTagValueOrRemove(tags, tagKey, tagValue);
            }
            else
            {
                string tagStringValue = tagValue.Value.ToString(CultureInfo.InvariantCulture);
                SetTagValueOrRemove(tags, tagKey, tagStringValue);
            }
        }

        internal static void SetLongValueOrRemove(this IDictionary<string, string> tags, string tagKey, long? tagValue)
        {
            if (tagValue == null)
            {
                SetTagValueOrRemove(tags, tagKey, tagValue);
            }
            else
            {
                string tagStringValue = tagValue.Value.ToString(CultureInfo.InvariantCulture);
                SetTagValueOrRemove(tags, tagKey, tagStringValue);
            }
        }

        internal static void SetStringValueOrRemove(this IDictionary<string, string> tags, string tagKey, string tagValue)
        {
            SetTagValueOrRemove(tags, tagKey, tagValue);
        }

        internal static void SetDateTimeOffsetValueOrRemove(this IDictionary<string, string> tags, string tagKey, DateTimeOffset? tagValue)
        {
            if (tagValue == null)
            {
                SetTagValueOrRemove(tags, tagKey, tagValue);
            }
            else
            {
                string tagValueString = tagValue.Value.ToString("O", CultureInfo.InvariantCulture);
                SetTagValueOrRemove(tags, tagKey, tagValueString);
            }
        }

        internal static void SetGuidValueOrRemove(this IDictionary<string, string> tags, string tagKey, Guid? tagValue)
        {
            if (tagValue == null)
            {
                SetTagValueOrRemove(tags, tagKey, tagValue);
            }
            else
            {
                string tagValueString = tagValue.Value.ToString();
                SetTagValueOrRemove(tags, tagKey, tagValueString);
            }
        }

        internal static void SetTagValueOrRemove<T>(this IDictionary<string, string> tags, string tagKey, T tagValue)
        {
            SetTagValueOrRemove(tags, tagKey, Convert.ToString(tagValue, CultureInfo.InvariantCulture));
        }

        internal static void InitializeTagValue<T>(this IDictionary<string, string> tags, string tagKey, T tagValue)
        {
            if (!tags.ContainsKey(tagKey))
            {
                SetTagValueOrRemove(tags, tagKey, tagValue);
            }
        }

        internal static void InitializeTagDateTimeOffsetValue(this IDictionary<string, string> tags, string tagKey, DateTimeOffset? tagValue)
        {
            if (!tags.ContainsKey(tagKey))
            {
                SetDateTimeOffsetValueOrRemove(tags, tagKey, tagValue);
            }
        }

        internal static string GetTagValueOrNull(this IDictionary<string, string> tags, string tagKey)
        {
            string tagValue;
            if (tags.TryGetValue(tagKey, out tagValue))
            {
                return tagValue;
            }

            return null;
        }

        private static void SetTagValueOrRemove(this IDictionary<string, string> tags, string tagKey, string tagValue)
        {
            if (string.IsNullOrEmpty(tagValue))
            {
                tags.Remove(tagKey);
            }
            else
            {
                tags[tagKey] = tagValue;
            }
        }
    }
}
