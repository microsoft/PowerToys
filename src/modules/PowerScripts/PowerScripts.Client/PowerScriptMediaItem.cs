// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace PowerScripts.Client;

/// <summary>One image or audio item. Data contains raw bytes, not base64 or a file path.</summary>
public sealed record PowerScriptMediaItem(string MimeType, byte[] Data);
