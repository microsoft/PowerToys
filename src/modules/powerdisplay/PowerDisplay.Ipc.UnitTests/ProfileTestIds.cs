// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Ipc.UnitTests;

internal static class ProfileTestIds
{
    internal const string FirstText = "b869bd32-aacf-4408-9c00-b883143ce9a4";
    internal const string SecondText = "420c330a-a8d1-4e1b-a15c-e3c3465b7f20";
    internal const string UnknownText = "c0ac737f-3f3d-4334-9e58-df0c93829441";

    internal static readonly Guid First = new(FirstText);
    internal static readonly Guid Second = new(SecondText);
    internal static readonly Guid Unknown = new(UnknownText);
}
