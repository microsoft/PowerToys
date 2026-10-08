// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32.Storage.FileSystem;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

/// <summary>The kind of a physical disk: whether it's solid state, and how it's connected.</summary>
internal readonly record struct DiskDeviceInfo(bool? IsSolidState, STORAGE_BUS_TYPE BusType);
