// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Drivers.DDC;

internal static class DdcErrorClassifier
{
    internal const int ErrorGraphicsInvalidPhysicalMonitorHandle = unchecked((int)0xC026258C);
    internal const int ErrorGraphicsMonitorNoLongerExists = unchecked((int)0xC026258D);

    public static bool IsPhysicalMonitorUnavailable(int errorCode) => errorCode is
        ErrorGraphicsInvalidPhysicalMonitorHandle or
        ErrorGraphicsMonitorNoLongerExists;
}
