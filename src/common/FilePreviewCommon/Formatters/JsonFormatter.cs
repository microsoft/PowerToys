// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Encodings.Web;
using System.Text.Json;

namespace Microsoft.PowerToys.FilePreviewCommon.Monaco.Formatters
{
    public class JsonFormatter : IFormatter
    {
        /// <inheritdoc/>
        public string LangSet => "json";

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <inheritdoc/>
        public string Format(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            using (var jDocument = JsonDocument.Parse(value, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }))
            {
                return JsonSerializer.Serialize(jDocument, _serializerOptions);
            }
        }
    }
}
