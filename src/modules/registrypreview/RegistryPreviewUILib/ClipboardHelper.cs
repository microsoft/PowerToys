// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace RegistryPreviewUILib
{
    /// <summary>
    /// Helper class to centralize clipboard actions
    /// </summary>
    internal static class ClipboardHelper
    {
        internal static void CopyToClipboardAction(string text)
        {
            try
            {
                var data = new DataPackage();
                data.SetText(text);
                Clipboard.SetContent(data);
                Clipboard.Flush();
            }
            catch
            {
            }
        }
    }
}
