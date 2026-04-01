// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using AdvancedPaste.Models;

namespace AdvancedPaste.Services.CustomActions
{
    public sealed class CustomActionTransformResult
    {
        public CustomActionTransformResult(string content, AIServiceUsage usage)
        {
            Content = content;
            Usage = usage;
        }

        public string Content { get; }

        public AIServiceUsage Usage { get; }
    }
}
