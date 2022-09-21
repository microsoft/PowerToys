// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.PreviewHandler.Monaco.Formatters
{
    using System.Text.Json;

    public class JsonFormatter : IFormatter
    {
        /// <inheritdoc/>
        public string LangSet => "json";

        /// <inheritdoc/>
        public string Format(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            using (var jDocument = JsonDocument.Parse(value, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }))
            {
                return JsonSerializer.Serialize(jDocument, new JsonSerializerOptions { WriteIndented = true });
            }
        }
    }
}
