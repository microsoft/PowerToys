// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Xml;

namespace Microsoft.PowerToys.FilePreviewCommon.Monaco.Formatters
{
    public class XmlFormatter : IFormatter
    {
        /// <inheritdoc/>
        public string LangSet => "xml";

        /// <inheritdoc/>
        public string Format(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(value);

            var stringBuilder = new StringBuilder();
            var xmlWriterSettings = new XmlWriterSettings()
            {
                OmitXmlDeclaration = xmlDocument.FirstChild?.NodeType != XmlNodeType.XmlDeclaration,
                Indent = true,
            };

            using (var xmlWriter = XmlWriter.Create(stringBuilder, xmlWriterSettings))
            {
                xmlDocument.Save(xmlWriter);
            }

            return stringBuilder.ToString();
        }
    }
}
