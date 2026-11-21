// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Awake.Core.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SystemTime
    {
        public ushort Year;
        public ushort Month;
        public ushort DayOfWeek;
        public ushort Day;
        public ushort Hour;
        public ushort Minute;
        public ushort Second;
        public ushort Milliseconds;

        public static SystemTime FromDateTime(DateTime value)
        {
            return new SystemTime
            {
                Year = (ushort)value.Year,
                Month = (ushort)value.Month,
                DayOfWeek = (ushort)value.DayOfWeek,
                Day = (ushort)value.Day,
                Hour = (ushort)value.Hour,
                Minute = (ushort)value.Minute,
                Second = (ushort)value.Second,
            };
        }
    }
}
