// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

// Attached only by the native block-mode report parser, never by workload observations.
// Keep the original identifier separate from bounded display text.
public sealed record NativeAccessDenial(string Resource, string ResourceType, string Access);
