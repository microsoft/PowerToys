namespace Mages.Core.Runtime.Functions
{
    using System;

    static class SimpleRandom
    {
        [ThreadStatic]
        private static Random _random;

        public static Double[,] CreateMatrix(Double x, Double y)
        {
            var rows = ToInteger(x);
            var cols = ToInteger(y);
            return CreateMatrix(rows, cols);
        }

        public static Double[,] CreateVector(Double length)
        {
            var rows = 1;
            var cols = ToInteger(length);
            return CreateMatrix(rows, cols);
        }

        public static Double GetNumber()
        {
            EnsureRandom();
            return _random.NextDouble();
        }

        private static Double[,] CreateMatrix(Int32 rows, Int32 cols)
        {
            var matrix = new Double[rows, cols];
            EnsureRandom();

            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < cols; j++)
                {
                    matrix[i, j] = _random.NextDouble();
                }
            }

            return matrix;
        }

        private static Int32 ToInteger(Double argument)
        {
            return Math.Max((Int32)argument, 0);
        }

        private static void EnsureRandom()
        {
            if (_random == null)
            {
                _random = new Random();
            }
        }
    }
}
