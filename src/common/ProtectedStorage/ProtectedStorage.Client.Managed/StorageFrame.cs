// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace PowerToys.ProtectedStorage;

public sealed record StorageFrame(uint Command, Guid RequestId, JsonElement Metadata, byte[] Bytes);
