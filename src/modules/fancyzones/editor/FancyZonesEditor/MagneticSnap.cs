// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace FancyZonesEditor
{
    public class MagneticSnap
    {
        private List<int> _keyPoints;
        private List<int> _magnetZoneSizes;
        private double _workAreaSize;

        private const int MagnetZoneMaxSize = GridData.Multiplier / 12;

        public MagneticSnap(List<int> keyPoints, double workAreaSize)
        {
            _keyPoints = keyPoints;
            _workAreaSize = workAreaSize;

            _magnetZoneSizes = new List<int>();

            for (int i = 0; i < _keyPoints.Count; i++)
            {
                int previous = i == 0 ? 0 : _keyPoints[i - 1];
                int next = i == _keyPoints.Count - 1 ? GridData.Multiplier : _keyPoints[i + 1];
                _magnetZoneSizes.Add(Math.Min(_keyPoints[i] - previous, Math.Min(next - _keyPoints[i], MagnetZoneMaxSize)) / 2);
            }
        }

        public int PixelToDataWithSnapping(double pixel)
        {
            int data = Convert.ToInt32(pixel / _workAreaSize * GridData.Multiplier);
            int result;
            int snapId = -1;

            for (int i = 0; i < _keyPoints.Count; ++i)
            {
                if (Math.Abs(data - _keyPoints[i]) <= _magnetZoneSizes[i])
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
                int deadZoneWidth = (_magnetZoneSizes[snapId] + 1) / 2;
                if (Math.Abs(data - _keyPoints[snapId]) <= deadZoneWidth)
                {
                    result = _keyPoints[snapId];
                }
                else if (data < _keyPoints[snapId])
                {
                    result = data + (data - (_keyPoints[snapId] - _magnetZoneSizes[snapId]));
                }
                else
                {
                    result = data - ((_keyPoints[snapId] + _magnetZoneSizes[snapId]) - data);
                }
            }

            return Math.Max(Math.Min(GridData.Multiplier, result), 0);
        }

        public double DataToPixelWithoutSnapping(int data)
        {
            return _workAreaSize * data / GridData.Multiplier;
        }
    }
}
