namespace Mages.Core.Runtime
{
    using System;

    static class Mathx
    {
        private static readonly Double[] LanczosD = new[]
        {
             2.48574089138753565546e-5,
             1.05142378581721974210,
            -3.45687097222016235469,
             4.51227709466894823700,
            -2.98285225323576655721,
             1.05639711577126713077,
            -1.95428773191645869583e-1,
             1.70970543404441224307e-2,
            -5.71926117404305781283e-4,
             4.63399473359905636708e-6,
            -2.71994908488607703910e-9
        };

        private static readonly Double[] BernoulliNumbers = new[]
        {
            1.0,
            1.0 / 6.0,
            -1.0 / 30.0,
            1.0 / 42.0,
            -1.0 / 30.0,
            5.0 / 66.0,
            -691.0 / 2730.0,
            7.0 / 6.0,
            -3617.0 / 510.0,
            43867.0 / 798.0,
            -174611.0 / 330.0,
            854513.0 / 138.0,
            -236364091.0 / 2730.0,
            8553103.0 / 6.0,
            -23749461029.0 / 870.0,
            8615841276005.0 / 14322.0,
            -7709321041217.0 / 510.0,
            2577687858367.0 / 6.0,
            -26315271553053477373.0 / 1919190.0,
            2929993913841559.0 / 6.0,
            -261082718496449122051.0 / 13530.0
        };

        public static Double Sign(Double value)
        {
            return (Double)Math.Sign(value);
        }

        public static Double Factorial(Double value)
        {
            var result = (Double)Math.Sign(value);
            var n = (Int32)Math.Floor(result * value);

            while (n > 0)
            {
                result *= n--;
            }

            return result;
        }

        public static Double Gamma(Double x)
        {
            if (x <= 0.0)
            {
                if (x == Math.Ceiling(x))
                {
                    return Double.PositiveInfinity;
                }

                return Math.PI / (Gamma(-x) * (-x) * Math.Sin(x * Math.PI));
            }

            return Math.Exp(LogGamma(x));
        }

        public static Double Asinh(Double value)
        {
            return Math.Log(value + Math.Sqrt(value * value + 1.0));
        }

        public static Double Acosh(Double value)
        {
            return Math.Log(value + Math.Sqrt(value * value - 1.0));
        }

        public static Double Atanh(Double value)
        {
            return 0.5 * Math.Log((1.0 + value) / (1.0 - value));
        }

        public static Double Cot(Double value)
        {
            return Math.Cos(value) / Math.Sin(value);
        }

        public static Double Acot(Double value)
        {
            return Math.Atan(1.0 / value);
        }

        public static Double Coth(Double value)
        {
            var a = Math.Exp(+value);
            var b = Math.Exp(-value);
            return (a + b) / (a - b);
        }

        public static Double Acoth(Double value)
        {
            return 0.5 * Math.Log((1.0 + value) / (value - 1.0));
        }

        public static Double Sec(Double value)
        {
            return 1.0 / Math.Cos(value);
        }

        public static Double Asec(Double value)
        {
            return Math.Acos(1.0 / value);
        }

        public static Double Sech(Double value)
        {
            return 2.0 / (Math.Exp(value) + Math.Exp(-value));
        }

        public static Double Asech(Double value)
        {
            var vi = 1.0 / value;
            return Math.Log(vi + Math.Sqrt(vi + 1.0) * Math.Sqrt(vi - 1.0));
        }

        public static Double Csc(Double value)
        {
            return 1.0 / Math.Sin(value);
        }

        public static Double Acsc(Double value)
        {
            return Math.Asin(1.0 / value);
        }

        public static Double Csch(Double value)
        {
            return 2.0 / (Math.Exp(value) - Math.Exp(-value));
        }

        public static Double Acsch(Double value)
        {
            return Math.Log(1.0 / value + Math.Sqrt(1.0 / (value * value) + 1.0));
        }

        private static Double LogGamma(Double x)
        {
            if (x <= 0.0)
            {
                return double.PositiveInfinity;
            }
            else if (x > 16.0)
            {
                return StirlingLogGamma(x);
            }

            return LanczosLogGamma(x);
        }

        private static Double LanczosLogGamma(Double x)
        {
            var sum = LanczosD[0];

            for (var i = 1; i < LanczosD.Length; i++)
            {
                sum += LanczosD[i] / (x + i);
            }

            sum = 2.0 / Math.Sqrt(Math.PI) * sum / x;
            var xshift = x + 0.5;
            var t = xshift * Math.Log(xshift + 10.900511) - x;
            return t + Math.Log(sum);
        }

        private static Double StirlingLogGamma(Double x)
        {
            var f = (x - 0.5) * Math.Log(x) - x + Math.Log(2.0 * Math.PI) / 2.0;
            var xsqu = x * x;
            var xp = x;

            for (var i = 1; i < 10; i++)
            {
                var f_old = f;
                f += BernoulliNumbers[i] / (2 * i) / (2 * i - 1) / xp;

                if (f == f_old)
                    break;

                xp *= xsqu;
            }

            return f;
        }
    }
}
