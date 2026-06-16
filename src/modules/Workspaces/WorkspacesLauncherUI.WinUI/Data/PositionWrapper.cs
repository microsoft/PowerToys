// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WorkspacesLauncherUI.Data
{
    public struct PositionWrapper
    {
        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public static bool operator ==(PositionWrapper left, PositionWrapper right)
        {
            return left.X == right.X && left.Y == right.Y && left.Width == right.Width && left.Height == right.Height;
        }

        public static bool operator !=(PositionWrapper left, PositionWrapper right)
        {
            return left.X != right.X || left.Y != right.Y || left.Width != right.Width || left.Height != right.Height;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            PositionWrapper pos = (PositionWrapper)obj;
            return X == pos.X && Y == pos.Y && Width == pos.Width && Height == pos.Height;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
