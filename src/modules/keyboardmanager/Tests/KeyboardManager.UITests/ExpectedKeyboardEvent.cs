// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.KeyboardManager.UITests;

internal readonly record struct ExpectedKeyboardEvent(int VirtualKey, bool IsKeyDown);

internal readonly record struct ObservedKeyboardEvent(
    int VirtualKey,
    bool IsKeyDown,
    ulong ExtraInfo,
    uint EventTime);
