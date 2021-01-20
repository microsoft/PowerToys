namespace Mages.Core.Runtime
{
    using Mages.Core.Runtime.Converters;
    using Mages.Core.Runtime.Functions;
    using System;

    static class UnaryOperators
    {
        #region Not Fields

        private static readonly Func<Double, Object> NotNumber = x => x == 0.0;
        private static readonly Func<Double[,], Object> NotMatrix = x => x.AreEqual(0.0);
        private static readonly Func<Object, Object> NotAny = x => !x.ToBoolean();

        #endregion

        #region Positive Fields

        private static readonly Func<Double, Object> PositiveNumber = m => +m;
        private static readonly Func<Double[,], Object> PositiveMatrix = m => m;
        private static readonly Func<Object, Object> PositiveAny = m => +m.ToNumber();

        #endregion

        #region Negative Fields

        private static readonly Func<Double, Object> NegativeNumber = m => -m;
        private static readonly Func<Double[,], Object> NegativeMatrix = m => m.ForEach(x => -x);
        private static readonly Func<Object, Object> NegativeAny = m => -m.ToNumber();

        #endregion

        #region Factorial Fields

        private static readonly Func<Double, Object> FactorialNumber = x => Mathx.Factorial(x);
        private static readonly Func<Double[,], Object> FactorialMatrix = x => x.ForEach(y => Mathx.Factorial(y));
        private static readonly Func<Object, Object> FactorialAny = x => Mathx.Factorial(x.ToNumber());

        #endregion

        #region Other Fields

        private static readonly Func<Double[,], Object> TransposeMatrix = x => x.Transpose();
        private static readonly Func<Double[,], Object> AbsMatrix = x => Matrix.Abs(x);

        #endregion

        #region Functions

        public static Object Not(Object[] args)
        {
            return If.Is<Double>(args, NotNumber) ?? 
                If.Is<Double[,]>(args, NotMatrix) ??
                If.Is<Object>(args, NotAny) ??
                true;
        }

        public static Object Positive(Object[] args)
        {
            return If.Is<Double>(args, PositiveNumber) ??
                If.Is<Double[,]>(args, PositiveMatrix) ??
                If.Is<Object>(args, PositiveAny) ??
                Double.NaN;
        }

        public static Object Negative(Object[] args)
        {
            return If.Is<Double>(args, NegativeNumber) ?? 
                If.Is<Double[,]>(args, NegativeMatrix) ??
                If.Is<Object>(args, NegativeAny) ??
                Double.NaN;
        }

        public static Object Factorial(Object[] args)
        {
            return If.Is<Double>(args, FactorialNumber) ?? 
                If.Is<Double[,]>(args, FactorialMatrix) ??
                If.Is<Object>(args, FactorialAny) ??
                Double.NaN;
        }

        public static Object Transpose(Object[] args)
        {
            return If.Is<Double[,]>(args, TransposeMatrix) ??
                args[0].ToNumber().ToMatrix();
        }

        public static Object Abs(Object[] args)
        {
            return If.Is<Double[,]>(args, AbsMatrix) ??
                Math.Abs(args[0].ToNumber());
        }

        public static Object Type(Object[] args)
        {
            return args[0].ToType();
        }

        #endregion
    }
}
