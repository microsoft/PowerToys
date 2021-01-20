namespace Mages.Core.Runtime
{
    using System;

    static class Range
    {
        public static Double[,] Create(Double from, Double to)
        {
            var step = Double.NaN;

            if (!Double.IsNaN(from) && !Double.IsNaN(to))
            {
                step = Math.Sign(to - from);
            }

            return Create(from, to, step);
        }

        public static Double[,] Create(Double from, Double to, Double step)
        {
            var count = (to - from) / step;

            if (count < 0 || Double.IsNaN(count))
            {
                count = 0;
            }
            else
            {
                count = 1.0 + Math.Floor(count);
            }

            var result = new Double[1, (Int32)count];

            for (var i = 0; i < count; i++)
            {
                result[0, i] = from;
                from += step;
            }

            return result;
        }
    }
}
