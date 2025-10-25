// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

// 类型别名，兼容 Windows API 命名约定
using RECT = PowerDisplay.Native.Rect;

namespace PowerDisplay.Native;

/// <summary>
/// 委托类型定义
/// </summary>
public static class NativeDelegates
{
    /// <summary>
    /// 显示器枚举过程委托
    /// </summary>
    /// <param name="hMonitor">显示器句柄</param>
    /// <param name="hdcMonitor">显示器 DC</param>
    /// <param name="lprcMonitor">显示器矩形指针</param>
    /// <param name="dwData">用户数据</param>
    /// <returns>继续枚举返回 true</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    /// <summary>
    /// 线程启动例程委托
    /// </summary>
    /// <param name="lpParameter">线程参数</param>
    /// <returns>线程退出代码</returns>
    public delegate uint ThreadStartRoutine(IntPtr lpParameter);
}
