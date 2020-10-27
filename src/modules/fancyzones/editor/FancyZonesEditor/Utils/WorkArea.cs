// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;

namespace FancyZonesEditor.Utils
{
    public class WorkArea
    {
        public static int DefaultDPI => 96;

        public static int SmallestUsedDPI { get; set; }

        public static List<WorkAreaData> Monitors { get; private set; }

        public static Rect WorkingAreaRect
        {
            get
            {
                if (Settings.SpanZonesAcrossMonitors)
                {
                    return WorkAreasUnion;
                }

                return GetWorkingArea(Settings.CurrentDesktopId);
            }
        }

        public static Rect WorkAreasUnion { get; set; }

        public WorkArea(int monitorsCount)
        {
            Monitors = new List<WorkAreaData>(monitorsCount);
        }

        public void Add(WorkAreaData workArea)
        {
            WorkAreasUnion = Rect.Union(WorkAreasUnion, workArea.WorkAreaRect);

            bool inserted = false;
            var workAreaRect = workArea.WorkAreaRect;
            for (int i = 0; i < Monitors.Count && !inserted; i++)
            {
                var rect = Monitors[i].WorkAreaRect;
                if (workAreaRect.Left < rect.Left && (workAreaRect.Top <= rect.Top || workAreaRect.Top == 0))
                {
                    Monitors.Insert(i, workArea);
                    inserted = true;
                }
                else if (workAreaRect.Left == rect.Left && workAreaRect.Top < rect.Top)
                {
                    Monitors.Insert(i, workArea);
                    inserted = true;
                }
            }

            if (!inserted)
            {
                Monitors.Add(workArea);
            }
        }

        public static Rect GetWorkingArea(int monitor)
        {
            if (monitor < 0 || monitor >= Monitors.Count)
            {
                return default(Rect);
            }

            var area = Monitors[monitor];
            return area.WorkAreaRect;
        }
    }
}
