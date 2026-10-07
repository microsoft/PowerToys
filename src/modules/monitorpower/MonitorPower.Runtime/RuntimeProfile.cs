// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using MonitorPower;

namespace MonitorPower.Runtime;

internal sealed record RuntimeProfile(
    string FileName,
    string Name,
    BuiltInDisplayProfile BuiltInProfile = BuiltInDisplayProfile.None)
{
    public string Description => MainWindow.GetProfileDescription(BuiltInProfile);
}
