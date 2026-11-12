// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Cli;

internal interface IClipboardAdapter
{
    DataPackageView Read();

    void Write(DataPackage content);
}
