// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using AdvancedPaste.Models.KernelQueryCache;

namespace AdvancedPaste.Helpers
{
    public class LogEvent
    {
        public LogEvent(object message)
        {
            this.message = message;
        }

        private object message;

        public string ToJsonString() => JsonSerializer.Serialize(this, AdvancedPasteJsonSerializerContext.Default.PersistedCache);
    }
}
