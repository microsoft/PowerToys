// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace WorkspacesEditor.ViewModels;

public sealed record OperationFailure(Guid OperationId, string Stage, string MessageResourceKey, string Retry, int? NativeCode);
