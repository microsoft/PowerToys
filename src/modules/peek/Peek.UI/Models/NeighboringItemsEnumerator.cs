// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using Peek.Common.Models;

namespace Peek.UI.Models
{
    public class NeighboringItemsEnumerator : IEnumerator<IFileSystemItem>
    {
        public IFileSystemItem Current => Items[CurrentIndex];

        object IEnumerator.Current => Current;

        private int CurrentIndex { get; set; }

        private NeighboringItems Items { get; }

        public NeighboringItemsEnumerator(NeighboringItems items)
        {
            CurrentIndex = -1;
            Items = items;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public bool MoveNext()
        {
            if (CurrentIndex >= Items.Count)
            {
                return false;
            }

            CurrentIndex++;

            return true;
        }

        public void Reset()
        {
            CurrentIndex = -1;
        }
    }
}
