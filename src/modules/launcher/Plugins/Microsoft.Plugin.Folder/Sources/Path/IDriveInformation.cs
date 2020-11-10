// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Collections.Generic;

namespace Microsoft.Plugin.Folder.Sources
{
    public interface IDriveInformation
    {
        IEnumerable<string> GetDriveNames();
    }
}
