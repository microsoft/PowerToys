// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.FolderItemSources
{
    using System.Threading.Tasks;
    using Peek.Common.Models;
    using Shell32;

    public interface IFolderItemsSource
    {
        // Result is null if no file at index
        public Task<File?> GetItemAt(uint index);

        public Task<InitialQueryData?> Initialize(IShellFolderViewDual3 folderView);
    }

    public struct InitialQueryData
    {
        public uint FirstItemIndex { get; set; }

        public uint ItemsCount { get; set; }
    }
}
