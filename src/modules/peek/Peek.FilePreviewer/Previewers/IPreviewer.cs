// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System.ComponentModel;
    using System.Threading.Tasks;
    using Windows.Foundation;

    public interface IPreviewer : INotifyPropertyChanged
    {
        PreviewState? State { get; }

        public Task<Size> GetPreviewSizeAsync();

        Task LoadPreviewAsync();
    }

    public enum PreviewState
    {
        Loading,
        Loaded,
        Error,
    }
}
