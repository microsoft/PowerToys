// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <summary>
//     Logging.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
namespace MouseWithoutBorders.Class
{
    internal class SeverityLevel
    {
        internal static readonly SeverityLevel Information = new SeverityLevel();
        internal static readonly SeverityLevel Error = new SeverityLevel();
        internal static readonly SeverityLevel Warning = new SeverityLevel();
    }
}
