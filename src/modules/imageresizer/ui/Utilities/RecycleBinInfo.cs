// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ImageResizer.Utilities;

/// <summary>
/// Information about the Recycle Bin, either on a single drive or across all drives.
/// </summary>
/// <param name="NumberOfItems">The total number of items in the specified Recycle Bin.</param>
/// <param name="SizeInBytes">The total size of the objects in the specified Recycle Bin, in bytes.
/// </param>
internal record RecycleBinInfo(ulong NumberOfItems, ulong SizeInBytes);
