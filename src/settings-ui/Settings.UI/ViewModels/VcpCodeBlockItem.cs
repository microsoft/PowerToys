// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class VcpCodeBlockItem
    {
        public VcpCodeBlockItem(byte code, string title, IReadOnlyList<VcpValueBlockItem> values)
        {
            Code = code;
            Title = string.IsNullOrWhiteSpace(title) ? $"0x{code:X2}" : title;
            Values = values;
        }

        public byte Code { get; }

        public string Title { get; }

        public IReadOnlyList<VcpValueBlockItem> Values { get; }
    }
}
