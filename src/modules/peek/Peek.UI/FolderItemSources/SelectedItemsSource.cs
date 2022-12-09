// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.FolderItemSources
{
    using System.Threading.Tasks;
    using Peek.Common.Models;
    using Shell32;

    // Source of folder items for use when user activates Peek with multiple selected files
    public class SelectedItemsSource : IFolderItemsSource
    {
        public Task<File?> GetItemAt(uint index)
        {
            throw new System.NotImplementedException();
        }

        public Task<InitialQueryData?> Initialize(IShellFolderViewDual3 folderView)
        {
            throw new System.NotImplementedException();
        }
    }
}
