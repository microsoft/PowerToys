// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Peek.FilePreviewer.Models;

namespace Peek.FilePreviewer.Previewers
{
    public interface IPreviewer : INotifyPropertyChanged
    {
        PreviewState State { get; set; }

        public Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken);

        Task LoadPreviewAsync(CancellationToken cancellationToken);

        Task CopyAsync();
    }

    public enum PreviewState
    {
        Uninitialized,
        Loading,
        Loaded,
        Error,
    }
}
