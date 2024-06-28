// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using HostsUILib.Models;

namespace HostsUILib.Helpers
{
    public interface IDuplicateService
    {
        void Initialize(IList<Entry> entries);

        void CheckDuplicates(string address, string[] hosts);
    }
}
