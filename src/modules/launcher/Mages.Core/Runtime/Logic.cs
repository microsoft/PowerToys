namespace Mages.Core.Runtime
{
    using Mages.Core.Runtime.Converters;
    using System;

    static class Logic
    {
        public static Boolean IsPrime(this Double value)
        {
            if (value.IsInteger())
            {
                return PrimeNumber.Check((Int32)value);
            }

            return false;
        }

        public static Boolean IsInteger(this Double value)
        {
            return Math.Truncate(value) == value;
        }

        public static Boolean AnyTrue(this Double[,] matrix)
        {
            var rows = matrix.GetRows();
            var cols = matrix.GetColumns();
            var res = false;

            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; !res && j < cols; j++)
                {
                    res = matrix[i, j].ToBoolean();
                }
            }

            return res;
        }

        public static Boolean AllTrue(this Double[,] matrix)
        {
            var rows = matrix.GetRows();
            var cols = matrix.GetColumns();
            var res = true;

            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; res && j < cols; j++)
                {
                    res = matrix[i, j].ToBoolean();
                }
            }

            return res;
        }
    }
}
