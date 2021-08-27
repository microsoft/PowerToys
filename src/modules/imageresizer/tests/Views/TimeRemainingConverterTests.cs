// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Globalization;
using ImageResizer.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Views
{
    public class TimeRemainingConverterTests
    {
        [DataTestMethod]
        [DataRow("HourMinute", 1, 1, 0)]
        [DataRow("HourMinutes", 1, 2, 0)]
        [DataRow("HoursMinute", 2, 1, 0)]
        [DataRow("HoursMinutes", 2, 2, 0)]
        [DataRow("MinuteSecond", 0, 1, 1)]
        [DataRow("MinuteSeconds", 0, 1, 2)]
        [DataRow("MinutesSecond", 0, 2, 1)]
        [DataRow("MinutesSeconds", 0, 2, 2)]
        [DataRow("Second", 0, 0, 1)]
        [DataRow("Seconds", 0, 0, 2)]
        public void ConvertWorks(string resource, int hours, int minutes, int seconds)
        {
            var timeRemaining = new TimeSpan(hours, minutes, seconds);
            var converter = new TimeRemainingConverter();

            // Using InvariantCulture since these are internal
            var result = converter.Convert(
                timeRemaining,
                targetType: null,
                parameter: null,
                CultureInfo.InvariantCulture);

            Assert.AreEqual(
                string.Format(
                    CultureInfo.InvariantCulture,
                    Resources.ResourceManager.GetString("Progress_TimeRemaining_" + resource, CultureInfo.InvariantCulture),
                    hours,
                    minutes,
                    seconds),
                result);
        }
    }
}
