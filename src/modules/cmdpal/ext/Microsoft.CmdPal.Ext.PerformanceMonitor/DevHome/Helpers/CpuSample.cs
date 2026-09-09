// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CoreWidgetProvider.Helpers;

internal readonly record struct CpuSample(float CpuUsage, float KernelUsage, float CpuSpeed);
