// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Forms;

namespace Microsoft.CropAndLock.TestApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using var source = new CropSourceForm();
            Application.Run(source);
        }
    }
}
