// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Principal;

namespace HostsUILib.Helpers
{
    public class ElevationHelper : IElevationHelper
    {
        private readonly bool _isElevated;

        public bool IsElevated => _isElevated;

        public ElevationHelper()
        {
            _isElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
