// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WorkspacesCsharpLibrary.Data;

internal sealed record SourceReceipt(string Origin, Guid MigrationId, string Identity, long Length, string Hash);
