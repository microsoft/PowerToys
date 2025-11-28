// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WorkspacesCsharpLibrary.Data;

public struct MonitorConfigurationWrapper
{
    public struct MonitorRectWrapper
    {
        public int Top { get; set; }

        public int Left { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }
    }

    public string Id { get; set; }

    public string InstanceId { get; set; }

    public int MonitorNumber { get; set; }

    public int Dpi { get; set; }

    public MonitorRectWrapper MonitorRectDpiAware { get; set; }

    public MonitorRectWrapper MonitorRectDpiUnaware { get; set; }
}
