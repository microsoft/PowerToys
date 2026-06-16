// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WorkspacesLauncherUI.Data
{
    public struct ApplicationWrapper
    {
        public string Application { get; set; }

        public string ApplicationPath { get; set; }

        public string Title { get; set; }

        public string PackageFullName { get; set; }

        public string AppUserModelId { get; set; }

        public string PwaAppId { get; set; }

        public string CommandLineArguments { get; set; }

        public bool IsElevated { get; set; }

        public bool CanLaunchElevated { get; set; }

        public bool Minimized { get; set; }

        public bool Maximized { get; set; }

        public PositionWrapper Position { get; set; }

        public int Monitor { get; set; }
    }
}
