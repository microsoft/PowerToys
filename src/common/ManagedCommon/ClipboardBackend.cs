// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1649 // File name should match first type name

using Windows.ApplicationModel.DataTransfer;

namespace ManagedCommon
{
    internal interface IClipboardBackend
    {
        DataPackageView GetContent();

        void SetContent(DataPackage content);

        void Flush();
    }

    internal sealed class WindowsClipboardBackend : IClipboardBackend
    {
        public DataPackageView GetContent() => Clipboard.GetContent();

        public void SetContent(DataPackage content) => Clipboard.SetContent(content);

        public void Flush() => Clipboard.Flush();
    }
}

#pragma warning restore SA1649 // File name should match first type name
