// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Ioctl;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

/// <summary>Reads what kind of disk a physical disk is, as Task Manager shows it, such as SSD (NVMe).</summary>
internal static class DiskDevices
{
    private static readonly ConcurrentDictionary<int, DiskDeviceInfo> Cache = new();

    /// <summary>Returns the kind of disk <paramref name="diskNumber"/>. It's read once, since it doesn't change.</summary>
    public static DiskDeviceInfo Get(int diskNumber) => Cache.GetOrAdd(diskNumber, Read);

    /// <summary>Returns the name of a bus type, such as NVMe, or null for buses the card doesn't name.</summary>
    public static string? GetBusName(STORAGE_BUS_TYPE busType) => busType switch
    {
        STORAGE_BUS_TYPE.BusTypeNvme => "NVMe",
        STORAGE_BUS_TYPE.BusTypeSata => "SATA",
        STORAGE_BUS_TYPE.BusTypeAta => "ATA",
        STORAGE_BUS_TYPE.BusTypeUsb => "USB",
        STORAGE_BUS_TYPE.BusTypeSas => "SAS",
        STORAGE_BUS_TYPE.BusTypeScsi => "SCSI",
        STORAGE_BUS_TYPE.BusTypeSd or STORAGE_BUS_TYPE.BusTypeMmc => "SD",
        STORAGE_BUS_TYPE.BusTypeUfs => "UFS",
        STORAGE_BUS_TYPE.BusTypeRAID => "RAID",
        _ => null,
    };

    private static unsafe DiskDeviceInfo Read(int diskNumber)
    {
        // Querying device properties needs no access rights, so this works without elevation.
        using var handle = PInvoke.CreateFile(
            $@"\\.\PhysicalDrive{diskNumber}",
            0,
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            0,
            null);
        if (handle.IsInvalid)
        {
            return new(null, STORAGE_BUS_TYPE.BusTypeUnknown);
        }

        // The SafeFileHandle keeps the handle open while the queries use it.
        var device = (HANDLE)handle.DangerousGetHandle();
        var query = new STORAGE_PROPERTY_QUERY
        {
            PropertyId = STORAGE_PROPERTY_ID.StorageDeviceSeekPenaltyProperty,
            QueryType = STORAGE_QUERY_TYPE.PropertyStandardQuery,
        };

        bool? isSolidState = null;
        DEVICE_SEEK_PENALTY_DESCRIPTOR seekPenalty = default;
        uint returned;
        if (PInvoke.DeviceIoControl(device, PInvoke.IOCTL_STORAGE_QUERY_PROPERTY, &query, (uint)sizeof(STORAGE_PROPERTY_QUERY), &seekPenalty, (uint)sizeof(DEVICE_SEEK_PENALTY_DESCRIPTOR), &returned, null))
        {
            // Disks without seek penalty, such as SSDs, don't need to move a head.
            isSolidState = !seekPenalty.IncursSeekPenalty;
        }

        // The descriptor ends with variable-length strings; the fixed part holds the bus type.
        query.PropertyId = STORAGE_PROPERTY_ID.StorageDeviceProperty;
        var buffer = stackalloc byte[1024];
        var busType = STORAGE_BUS_TYPE.BusTypeUnknown;
        if (PInvoke.DeviceIoControl(device, PInvoke.IOCTL_STORAGE_QUERY_PROPERTY, &query, (uint)sizeof(STORAGE_PROPERTY_QUERY), buffer, 1024, &returned, null)
            && returned >= sizeof(STORAGE_DEVICE_DESCRIPTOR) - 1)
        {
            busType = ((STORAGE_DEVICE_DESCRIPTOR*)buffer)->BusType;
        }

        return new(isSolidState, busType);
    }
}
