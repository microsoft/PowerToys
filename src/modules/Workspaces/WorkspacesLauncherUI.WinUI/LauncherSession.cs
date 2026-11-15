// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace WorkspacesLauncherUI
{
    internal sealed record LauncherSession(string OperationId, int OwnerPid, bool ConfirmationOnly)
    {
        internal static bool TryParse(string[] args, out LauncherSession session)
        {
            session = new LauncherSession(string.Empty, 0, false);
            if (args.Length == 0)
            {
                return true;
            }

            if (args.Length != 3 || (args[0] != "--confirmation-only" && args[0] != "--launch-ui") ||
                !Guid.TryParse(args[1], out var id) || id == Guid.Empty ||
                !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var ownerPid) || ownerPid <= 0)
            {
                return false;
            }

            session = new LauncherSession(id.ToString("D"), ownerPid, args[0] == "--confirmation-only");
            return true;
        }
    }
}
