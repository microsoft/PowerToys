// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Wox.Plugin
{
    public class ToolTipData
    {
        public string Title { get; private set; }

        public string Text { get; private set; }

        public ToolTipData(string title, string text)
        {
            if (string.IsNullOrEmpty(title))
            {
                // DO NOT LOCALIZE internal exception message
                throw new ArgumentException("title cannot be null or empty", nameof(title));
            }

            Title = title;
            Text = text;
        }
    }
}
