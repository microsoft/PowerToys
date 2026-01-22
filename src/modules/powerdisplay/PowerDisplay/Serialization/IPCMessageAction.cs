// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerDisplay.Serialization
{
    /// <summary>
    /// IPC message wrapper for parsing action-based messages.
    /// Used in App.xaml.cs for dynamic IPC command handling.
    /// </summary>
    internal sealed class IpcMessageAction
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }
    }
}
