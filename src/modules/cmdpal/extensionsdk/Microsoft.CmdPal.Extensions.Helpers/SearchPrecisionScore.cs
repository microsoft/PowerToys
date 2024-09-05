// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//[assembly: InternalsVisibleTo("Microsoft.Plugin.Program.UnitTests")]
//[assembly: InternalsVisibleTo("Microsoft.PowerToys.Run.Plugin.System.UnitTests")]
//[assembly: InternalsVisibleTo("Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests")]

namespace Microsoft.CmdPal.Extensions.Helpers;

public enum SearchPrecisionScore
{
    Regular = 50,
    Low = 20,
    None = 0,
}
