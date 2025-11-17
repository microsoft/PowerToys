// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.VSCodeHelper
{
    public enum VSCodeVersion
    {
        Stable = 1,
        Insiders = 2,
        Exploration = 3,
    }

    public class VSCodeInstance
    {
        public VSCodeVersion VSCodeVersion { get; set; }

        public string ExecutablePath { get; set; } = string.Empty;

        public string AppData { get; set; } = string.Empty;

        public ImageSource WorkspaceIcon() => WorkspaceIconBitMap;

        public ImageSource RemoteIcon() => RemoteIconBitMap;

        public BitmapImage WorkspaceIconBitMap { get; set; }

        public BitmapImage RemoteIconBitMap { get; set; }
    }
}
