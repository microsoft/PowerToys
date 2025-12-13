// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WorkspacesCsharpLibrary.Data;

public struct ApplicationWrapper
{
    public struct WindowPositionWrapper
    {
        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }
    }

    public string Id { get; set; }

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

    public WindowPositionWrapper Position { get; set; }

    public int Monitor { get; set; }

    public string Version { get; set; }
}
