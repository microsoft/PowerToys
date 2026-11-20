// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerToys.FileLocksmithUI.Helpers
{
    internal static class ProcessOwner
    {
        public static bool IsSystem(string user)
        {
            if (user is null)
            {
                return false;
            }

            string normalized = user.ToUpperInvariant().Trim();
            return normalized.Equals("SYSTEM", StringComparison.Ordinal) || normalized.Equals("LOCALSYSTEM", StringComparison.Ordinal);
        }

        public static ProcessOwnerKind Classify(string user, string currentUser)
        {
            if (string.IsNullOrWhiteSpace(user))
            {
                return ProcessOwnerKind.Unknown;
            }

            if (IsSystem(user))
            {
                return ProcessOwnerKind.System;
            }

            // Compares user names only, because pid_to_user drops the domain: a same-named account on another domain counts as CurrentUser,
            // and when elevated as a different admin your own processes count as OtherUser. Comparing SIDs would be exact.
            return user.Equals(currentUser, StringComparison.OrdinalIgnoreCase) ? ProcessOwnerKind.CurrentUser : ProcessOwnerKind.OtherUser;
        }
    }

    internal enum ProcessOwnerKind
    {
        CurrentUser,
        OtherUser,
        System,
        Unknown,
    }
}
