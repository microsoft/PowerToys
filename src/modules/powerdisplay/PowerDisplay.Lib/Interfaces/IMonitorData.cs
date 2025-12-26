// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Interfaces
{
    /// <summary>
    /// Core interface representing monitor hardware data.
    /// This interface defines the actual hardware values for a monitor.
    /// Implementations can add UI-specific properties and use converters for display formatting.
    /// </summary>
    public interface IMonitorData
    {
        /// <summary>
        /// Gets or sets the unique identifier for the monitor.
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// Gets or sets the display name of the monitor.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the current brightness value (0-100).
        /// </summary>
        int Brightness { get; set; }

        /// <summary>
        /// Gets or sets the current contrast value (0-100).
        /// </summary>
        int Contrast { get; set; }

        /// <summary>
        /// Gets or sets the current volume value (0-100).
        /// </summary>
        int Volume { get; set; }

        /// <summary>
        /// Gets or sets the color temperature VCP preset value (raw DDC/CI value from VCP code 0x14).
        /// This stores the raw VCP value (e.g., 0x05 for 6500K preset), not the Kelvin temperature.
        /// Use MonitorValueConverter to convert to/from human-readable Kelvin values.
        /// </summary>
        int ColorTemperatureVcp { get; set; }

        /// <summary>
        /// Gets or sets the monitor number (1, 2, 3...) as assigned by the OS.
        /// </summary>
        int MonitorNumber { get; set; }

        /// <summary>
        /// Gets or sets the monitor orientation (0=0, 1=90, 2=180, 3=270).
        /// </summary>
        int Orientation { get; set; }
    }
}
