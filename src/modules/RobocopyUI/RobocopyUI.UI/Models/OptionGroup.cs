// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace RobocopyUI.Models
{
    public class OptionGroup
    {
        public OptionGroup(string key, IList<OptionContent> items)
        {
            Key = key;
            Items = items;
        }

        public string Key { get; }

        public IList<OptionContent> Items { get; }
    }
}
