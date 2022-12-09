// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using Windows.Foundation;

    public interface IPreviewer : INotifyPropertyChanged
    {
        PreviewState State { get; set; }

        public static bool IsFileTypeSupported(string fileExt) => throw new NotImplementedException();

        public Task<Size> GetPreviewSizeAsync();

        Task LoadPreviewAsync();

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
