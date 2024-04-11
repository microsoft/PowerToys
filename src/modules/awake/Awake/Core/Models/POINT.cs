// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Awake.Core.Models
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Matches Win32 formatting.")]
    internal struct POINT
    {
#pragma warning disable CS0649
        public int x;
        public int y;
#pragma warning restore CS0649
    }
}
