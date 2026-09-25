// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Peek.FilePreviewer.Previewers.EmailPreviewer
{
    internal sealed class EmailMessage
    {
        public string Sender { get; set; } = string.Empty;

        public List<string> To { get; } = [];

        public List<string> Cc { get; } = [];

        public List<string> Bcc { get; } = [];

        public string Subject { get; set; } = string.Empty;

        public DateTimeOffset? Date { get; set; }

        public string Body { get; set; } = string.Empty;

        public bool IsBodyHtml { get; set; }

        public Dictionary<string, EmailInlineImage> InlineImages { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
