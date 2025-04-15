// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;

using Peek.Common.Models;
using Peek.UI.Extensions;

namespace Peek.UI.Models
{
    public partial class NeighboringItems : IReadOnlyList<IFileSystemItem>
    {
        public IFileSystemItem this[int index] => Items[index] = Items[index] ?? ShellItemArray.GetItemAt(index).ToIFileSystemItem();

        public int Count { get; }

        private IFileSystemItem[] Items { get; }

        private IShellItemArray ShellItemArray { get; }

        public NeighboringItems(IShellItemArray shellItemArray)
        {
            ShellItemArray = shellItemArray;
            Count = ShellItemArray.GetCount();
            Items = new IFileSystemItem[Count];
        }

        public IEnumerator<IFileSystemItem> GetEnumerator() => new NeighboringItemsEnumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
