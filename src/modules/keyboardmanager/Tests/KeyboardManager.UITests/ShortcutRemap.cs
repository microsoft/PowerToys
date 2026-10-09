// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.KeyboardManager.UITests;

internal sealed record ShortcutRemap(int[] OriginalKeys, int[] NewKeys, string TargetApp = "");
