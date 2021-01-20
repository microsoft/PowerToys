namespace Mages.Core.Runtime
{
    using System;

    static class PrimeNumber
    {
        public static Boolean Check(Int32 n)
        {
            if (n < 8)
            {
                return n == 2 || n == 3 || n == 5 || n == 7;
            }
            else if (n % 2 == 0)
            {
                return false;
            }
            else
            {
                var m = n - 1;
                var d = m;
                var s = 0;

                while (d % 2 == 0)
                {
                    s++;
                    d = d / 2;
                }

                if (n < 1373653)
                {
                    return IsProbablyPrime(n, m, s, d, 2) && IsProbablyPrime(n, m, s, d, 3);
                }

                return IsProbablyPrime(n, m, s, d, 2) && IsProbablyPrime(n, m, s, d, 7) && IsProbablyPrime(n, m, s, d, 61);
            }
        }

        private static Boolean IsProbablyPrime(Int32 n, Int32 m, Int32 s, Int32 d, Int32 w)
        {
            var x = PowMod(w, d, n);

            if (x != 1 && x != m)
            {
                for (var i = 0; i < s; i++)
                {
                    x = PowMod(x, 2, n);

                    if (x == 1)
                    {
                        return false;
                    }
                    else if (x == m)
                    {
                        return true;
                    }
                }

                return false;
            }
                
            return true;
        }

        private static Int32 PowMod(Int32 b, Int32 e, Int32 m)
        {
            if (b < 0)
                throw new ArgumentOutOfRangeException("b");

            if (e < 1)
                throw new ArgumentOutOfRangeException("e");

            if (m < 1)
                throw new ArgumentOutOfRangeException("m");

            var bb = Convert.ToInt64(b);
            var mm = Convert.ToInt64(m);
            var rr = 1L;

            while (e > 0)
            {
                if ((e & 1) == 1)
                {
                    rr = checked((rr * bb) % mm);
                }

                e = e >> 1;
                bb = checked((bb * bb) % mm);
            }

            return Convert.ToInt32(rr);
        }
    }
}
