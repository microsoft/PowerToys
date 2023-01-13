// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FancyZonesEditor
{
    public class MagneticSnap
    {
        private List<int> _keyPoints;
        private double _workAreaSize;

        private const int MagnetZoneMaxSize = GridData.Multiplier / 12;

        public MagneticSnap(List<int> keyPoints, double workAreaSize)
        {
            _keyPoints = keyPoints;
            _workAreaSize = workAreaSize;
        }

        public int PixelToDataWithSnapping(double pixel, int low, int high)
        {
            var keyPoints = _keyPoints.Where(x => low < x && x < high).ToList();
            var magnetZoneSizes = new List<int>();

            for (int i = 0; i < keyPoints.Count; i++)
            {
                int previous = i == 0 ? low : keyPoints[i - 1];
                int next = i == keyPoints.Count - 1 ? high : keyPoints[i + 1];
                magnetZoneSizes.Add(Math.Min(keyPoints[i] - previous, Math.Min(next - keyPoints[i], MagnetZoneMaxSize)) / 2);
            }

            int data = Convert.ToInt32(pixel / _workAreaSize * GridData.Multiplier);
            data = Math.Clamp(data, low, high);
            int result;
            int snapId = -1;

            for (int i = 0; i < keyPoints.Count; ++i)
            {
                if (Math.Abs(data - keyPoints[i]) <= magnetZoneSizes[i])
                {
                    snapId = i;
                    break;
                }
            }

            if (snapId == -1)
            {
                result = data;
            }
            else
            {
                int deadZoneWidth = (magnetZoneSizes[snapId] + 1) / 2;
                if (Math.Abs(data - keyPoints[snapId]) <= deadZoneWidth)
                {
                    result = keyPoints[snapId];
                }
                else if (data < keyPoints[snapId])
                {
                    result = data + (data - (keyPoints[snapId] - magnetZoneSizes[snapId]));
                }
                else
                {
                    result = data - ((keyPoints[snapId] + magnetZoneSizes[snapId]) - data);
                }
            }

            return Math.Clamp(result, low, high);
        }

        public double DataToPixelWithoutSnapping(int data)
        {
            return _workAreaSize * data / GridData.Multiplier;
        }
    }
}
