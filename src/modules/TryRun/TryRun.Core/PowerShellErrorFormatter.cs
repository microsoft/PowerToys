// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Xml;
using System.Xml.Linq;

namespace PowerToys.TryRun.Core;

public static class PowerShellErrorFormatter
{
    public static string Format(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.StartsWith("#< CLIXML", StringComparison.Ordinal) || text.Length > OutputBuffer.MaximumCharacters)
        {
            return text;
        }

        var start = text.IndexOf('<', 2);
        if (start < 0)
        {
            return text;
        }

        try
        {
            using var input = new StringReader(text[start..]);
            using var reader = XmlReader.Create(input, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = OutputBuffer.MaximumCharacters,
            });
            var document = XDocument.Load(reader);
            if (document.Root?.Name.LocalName != "Objs")
            {
                return text;
            }

            return string.Concat(document.Descendants()
                .Where(element => element.Name.LocalName == "S" && element.Attribute("S") is not null)
                .Select(element => XmlConvert.DecodeName(element.Value)));
        }
        catch (XmlException)
        {
            // Preserve truncated or non-XML diagnostic output as plain text.
            return text;
        }
    }
}
