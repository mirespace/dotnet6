﻿namespace Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using System.Xml.Linq;
    using Microsoft.Web.XmlTransform;

    public static class ConfigurationHelpers
    {
        private const string ApplicationInsightsConfigInstall = "Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel.Resources.ApplicationInsights.config.install.xdt";
        private const string ApplicationInsightsConfigUninstall = "Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel.Resources.ApplicationInsights.config.uninstall.xdt";
        private const string ApplicationInsightsTransform = "Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel.Resources.ApplicationInsights.config.transform";
        
        private static readonly XNamespace XmlNamespace = "http://schemas.microsoft.com/ApplicationInsights/2013/Settings";

        public static string GetEmptyConfig()
        {
            Stream stream = typeof(TelemetryChannelTests).Assembly.GetManifestResourceStream(ApplicationInsightsTransform);
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        public static IEnumerable<XElement> GetTelemetryChannel(XDocument config)
        {
            return config.Descendants(XmlNamespace + "TelemetryChannel");
        }

        public static IEnumerable<XElement> GetTelemetryProcessors(XDocument config)
        {
            var processors = config.Descendants(XmlNamespace + "TelemetryProcessors");
            return processors?.Nodes().Cast<XElement>();
        }

        public static string GetPartialTypeName(Type typeToFind)
        {
            return typeToFind.FullName + ", " + typeToFind.Assembly.GetName().Name;
        }

        public static XDocument InstallTransform(string sourceXml)
        {
            return Transform(sourceXml, ApplicationInsightsConfigInstall);
        }

        public static XDocument UninstallTransform(string sourceXml)
        {
            return Transform(sourceXml, ApplicationInsightsConfigUninstall);
        }

        private static XDocument Transform(string sourceXml, string transformationFileResourceName)
        {
            using (var document = new XmlTransformableDocument())
            {
                Stream stream = typeof(TelemetryChannelTests).Assembly.GetManifestResourceStream(transformationFileResourceName);
                using (var reader = new StreamReader(stream))
                {
                    string transform = reader.ReadToEnd();
                    using (var transformation = new XmlTransformation(transform, false, null))
                    {
                        XmlReaderSettings settings = new XmlReaderSettings();
                        settings.ValidationType = ValidationType.None;

                        using (XmlReader xmlReader = XmlReader.Create(new StringReader(sourceXml), settings))
                        {
                            document.Load(xmlReader);
                            transformation.Apply(document);
                            return XDocument.Parse(document.OuterXml);
                        }
                    }
                }
            }
        }
    }
}
