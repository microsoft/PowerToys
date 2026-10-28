// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.ProtectedStorage;

namespace WorkspacesCsharpLibrary.Data;

public sealed record WorkspaceSnapshot(IReadOnlyList<ProjectWrapper> Projects, Revision Revision, bool SourceCleanupPending = false);
