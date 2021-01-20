namespace Mages.Core.Runtime
{
    using Mages.Core.Runtime.Converters;
    using Mages.Core.Runtime.Functions;
    using System;

    static class BinaryOperators
    {
        #region Converter Fields

        private static readonly Func<Object, Double> AsNumber = m => m.ToNumber();

        #endregion

        #region Add Fields

        private static readonly Func<Double, Double, Object> AddNumbers = (y, x) => x + y;
        private static readonly Func<Double[,], Double[,], Object> AddMatrices = (y, x) => x.Add(y);
        private static readonly Func<String, String, Object> AddStrings = (y, x) => String.Concat(x, y);
        private static readonly Func<Object, String, Object> AddAnyStr = (y, x) => String.Concat(x, Stringify.This(y));
        private static readonly Func<String, Object, Object> AddStrAny = (y, x) => String.Concat(Stringify.This(x), y);

        #endregion

        #region Sub Fields

        private static readonly Func<Double, Double, Object> SubNumbers = (y, x) => x - y;
        private static readonly Func<Double[,], Double[,], Object> SubMatrices = (y, x) => x.Subtract(y);

        #endregion

        #region Mul Fields

        private static readonly Func<Double, Double, Object> MulNumbers = (y, x) => x * y;
        private static readonly Func<Double[,], Double[,], Object> MulMatrices = (y, x) => x.Multiply(y);
        private static readonly Func<Double, Double[,], Object> MulNumMat = (y, x) => x.Multiply(y);
        private static readonly Func<Double[,], Double, Object> MulMatNum = (y, x) => y.Multiply(x);

        #endregion

        #region RDiv Fields

        private static readonly Func<Double, Double, Object> RDivNumbers = (y, x) => x / y;
        private static readonly Func<Double, Double[,], Object> RDivNumMat = (y, x) => x.Divide(y);

        #endregion

        #region LDiv Fields

        private static readonly Func<Double, Double, Object> LDivNumbers = (y, x) => y / x;
        private static readonly Func<Double[,], Double, Object> LDivMatNum = (y, x) => y.Divide(x);

        #endregion

        #region Pow Fields

        private static readonly Func<Double, Double, Object> PowNumbers = (y, x) => Math.Pow(x, y);
        private static readonly Func<Double[,], Double[,], Object> PowMatrices = (y, x) => x.Pow(y);
        private static readonly Func<Double[,], Double, Object> PowMatNum = (y, x) => x.Pow(y);
        private static readonly Func<Double, Double[,], Object> PowNumMat = (y, x) => x.Pow(y);

        #endregion

        #region Other Fields

        private static readonly Func<Double, Double, Object> ModNumbers = (y, x) => x % y;
        private static readonly Func<Function, Object, Object> InvokeFunction = (f, arg) => f.Invoke(new[] { arg });

        #endregion

        #region And Fields

        private static readonly Func<Double, Double, Object> AndNumbers = (y, x) => x.ToBoolean() && y.ToBoolean();
        private static readonly Func<Boolean, Boolean, Object> AndBooleans = (y, x) => x && y;
        private static readonly Func<Double[,], Double[,], Object> AndMatrices = (y, x) => x.And(y);
        private static readonly Func<Double[,], Double, Object> AndMatNum = (y, x) => y.And(x);
        private static readonly Func<Double, Double[,], Object> AndNumMat = (y, x) => x.And(y);
        private static readonly Func<Double[,], Boolean, Object> AndMatBool = (y, x) => y.And(x.ToNumber());
        private static readonly Func<Boolean, Double[,], Object> AndBoolMat = (y, x) => x.And(y.ToNumber());

        #endregion

        #region Or Fields

        private static readonly Func<Double, Double, Object> OrNumbers = (y, x) => x.ToBoolean() || y.ToBoolean();
        private static readonly Func<Boolean, Boolean, Object> OrBooleans = (y, x) => x || y;
        private static readonly Func<Double[,], Double[,], Object> OrMatrices = (y, x) => x.Or(y);
        private static readonly Func<Double[,], Double, Object> OrMatNum = (y, x) => y.Or(x);
        private static readonly Func<Double, Double[,], Object> OrNumMat = (y, x) => x.Or(y);
        private static readonly Func<Double[,], Boolean, Object> OrMatBool = (y, x) => y.Or(x.ToNumber());
        private static readonly Func<Boolean, Double[,], Object> OrBoolMat = (y, x) => x.Or(y.ToNumber());

        #endregion

        #region Eq Fields

        private static readonly Func<Double, Double, Object> EqNumbers = (y, x) => x == y;
        private static readonly Func<Boolean, Boolean, Object> EqBooleans = (y, x) => x == y;
        private static readonly Func<Double[,], Double[,], Object> EqMatrices = (y, x) => x.AreEqual(y);
        private static readonly Func<Double[,], Double, Object> EqMatNum = (y, x) => y.AreEqual(x);
        private static readonly Func<Double, Double[,], Object> EqNumMat = (y, x) => x.AreEqual(y);
        private static readonly Func<Double[,], Boolean, Object> EqMatBool = (y, x) => y.AreEqual(x.ToNumber());
        private static readonly Func<Boolean, Double[,], Object> EqBoolMat = (y, x) => x.AreEqual(y.ToNumber());
        private static readonly Func<String, String, Object> EqStrings = (y, x) => y.Equals(x);

        #endregion

        #region Neq Fields

        private static readonly Func<Double, Double, Object> NeqNumbers = (y, x) => x != y;
        private static readonly Func<Boolean, Boolean, Object> NeqBooleans = (y, x) => x != y;
        private static readonly Func<Double[,], Double[,], Object> NeqMatrices = (y, x) => x.AreNotEqual(y);
        private static readonly Func<Double[,], Double, Object> NeqMatNum = (y, x) => y.AreNotEqual(x);
        private static readonly Func<Double, Double[,], Object> NeqNumMat = (y, x) => x.AreNotEqual(y);
        private static readonly Func<Double[,], Boolean, Object> NeqMatBool = (y, x) => y.AreNotEqual(x.ToNumber());
        private static readonly Func<Boolean, Double[,], Object> NeqBoolMat = (y, x) => x.AreNotEqual(y.ToNumber());
        private static readonly Func<String, String, Object> NeqStrings = (y, x) => !x.Equals(y);

        #endregion

        #region Gt Fields

        private static readonly Func<Double, Double, Object> GtNumbers = (y, x) => x > y;
        private static readonly Func<Double[,], Double[,], Object> GtMatrices = Matrix.IsLessThan;
        private static readonly Func<Double[,], Double, Object> GtMatNum = Matrix.IsLessThan;
        private static readonly Func<Double, Double[,], Object> GtNumMat = (y, x) => x.IsGreaterThan(y);

        #endregion

        #region Geq Fields

        private static readonly Func<Double, Double, Object> GeqNumbers = (y, x) => x >= y;
        private static readonly Func<Double[,], Double[,], Object> GeqMatrices = Matrix.IsLessOrEqual;
        private static readonly Func<Double[,], Double, Object> GeqMatNum = Matrix.IsLessOrEqual;
        private static readonly Func<Double, Double[,], Object> GeqNumMat = (y, x) => x.IsGreaterOrEqual(y);

        #endregion

        #region Lt Fields

        private static readonly Func<Double, Double, Object> LtNumbers = (y, x) => x < y;
        private static readonly Func<Double[,], Double[,], Object> LtMatrices = Matrix.IsGreaterThan;
        private static readonly Func<Double[,], Double, Object> LtMatNum = Matrix.IsGreaterThan;
        private static readonly Func<Double, Double[,], Object> LtNumMat = (y, x) => x.IsLessThan(y);

        #endregion

        #region Leq Fields

        private static readonly Func<Double, Double, Object> LeqNumbers = (y, x) => x <= y;
        private static readonly Func<Double[,], Double[,], Object> LeqMatrices = Matrix.IsGreaterOrEqual;
        private static readonly Func<Double[,], Double, Object> LeqMatNum = Matrix.IsGreaterOrEqual;
        private static readonly Func<Double, Double[,], Object> LeqNumMat = (y, x) => x.IsLessOrEqual(y);

        #endregion

        #region Functions

        public static Object Add(Object[] args)
        {
            return If.Is<Double, Double>(args, AddNumbers) ??
                If.Is<Double[,], Double[,]>(args, AddMatrices) ??
                If.Is<String, String>(args, AddStrings) ??
                If.Is<Object, String>(args, AddAnyStr) ??
                If.Is<String, Object>(args, AddStrAny) ??
                If.IsNotNull(args, AsNumber, AddNumbers);
        }

        public static Object Sub(Object[] args)
        {
            return If.Is<Double, Double>(args, SubNumbers) ??
                If.Is<Double[,], Double[,]>(args, SubMatrices) ??
                If.IsNotNull(args, AsNumber, SubNumbers);
        }
        
        public static Object Mul(Object[] args)
        {
            return If.Is<Double, Double>(args, MulNumbers) ??
                If.Is<Double[,], Double[,]>(args, MulMatrices) ??
                If.Is<Double, Double[,]>(args, MulNumMat) ??
                If.Is<Double[,], Double>(args, MulMatNum) ??
                If.IsNotNull(args, AsNumber, MulNumbers);
        }

        public static Object RDiv(Object[] args)
        {
            return If.Is<Double, Double>(args, RDivNumbers) ??
                If.Is<Double, Double[,]>(args, RDivNumMat) ??
                If.IsNotNull(args, AsNumber, RDivNumbers);
        }

        public static Object LDiv(Object[] args)
        {
            return If.Is<Double, Double>(args, LDivNumbers) ??
                If.Is<Double[,], Double>(args, LDivMatNum) ??
                If.IsNotNull(args, AsNumber, LDivNumbers);
        }

        public static Object Pow(Object[] args)
        {
            return If.Is<Double, Double>(args, PowNumbers) ??
                If.Is<Double[,], Double[,]>(args, PowMatrices) ??
                If.Is<Double[,], Double>(args, PowMatNum) ??
                If.Is<Double, Double[,]>(args, PowNumMat) ??
                If.IsNotNull(args, AsNumber, PowNumbers);
        }

        public static Object Mod(Object[] args)
        {
            return If.Is<Double, Double>(args, ModNumbers) ??
                If.IsNotNull(args, AsNumber, ModNumbers);
        }

        public static Object And(Object[] args)
        {
            return If.Is<Double, Double>(args, AndNumbers) ??
                If.Is<Boolean, Boolean>(args, AndBooleans) ??
                If.Is<Double[,], Double[,]>(args, AndMatrices) ??
                If.Is<Double[,], Double>(args, AndMatNum) ??
                If.Is<Double, Double[,]>(args, AndNumMat) ??
                If.Is<Double[,], Boolean>(args, AndMatBool) ??
                If.Is<Boolean, Double[,]>(args, AndBoolMat) ??
                (args[1].ToBoolean() && args[0].ToBoolean());
        }

        public static Object Or(Object[] args)
        {
            return If.Is<Double, Double>(args, OrNumbers) ??
                If.Is<Boolean, Boolean>(args, OrBooleans) ??
                If.Is<Double[,], Double[,]>(args, OrMatrices) ??
                If.Is<Double[,], Double>(args, OrMatNum) ??
                If.Is<Double, Double[,]>(args, OrNumMat) ??
                If.Is<Double[,], Boolean>(args, OrMatBool) ??
                If.Is<Boolean, Double[,]>(args, OrBoolMat) ??
                (args[1].ToBoolean() || args[0].ToBoolean());
        }

        public static Object Eq(Object[] args)
        {
            return If.Is<Double, Double>(args, EqNumbers) ??
                If.Is<Boolean, Boolean>(args, EqBooleans) ??
                If.Is<Double[,], Double[,]>(args, EqMatrices) ??
                If.Is<Double[,], Double>(args, EqMatNum) ??
                If.Is<Double, Double[,]>(args, EqNumMat) ??
                If.Is<Double[,], Boolean>(args, EqMatBool) ??
                If.Is<Boolean, Double[,]>(args, EqBoolMat) ??
                If.Is<String, String>(args, EqStrings) ??
                Object.ReferenceEquals(args[1], args[0]);
        }

        public static Object Neq(Object[] args)
        {
            return If.Is<Double, Double>(args, NeqNumbers) ??
                If.Is<Boolean, Boolean>(args, NeqBooleans) ??
                If.Is<Double[,], Double[,]>(args, NeqMatrices) ??
                If.Is<Double[,], Double>(args, NeqMatNum) ??
                If.Is<Double, Double[,]>(args, NeqNumMat) ??
                If.Is<Double[,], Boolean>(args, NeqMatBool) ??
                If.Is<Boolean, Double[,]>(args, NeqBoolMat) ??
                If.Is<String, String>(args, NeqStrings) ??
                !Object.ReferenceEquals(args[1], args[0]);
        }

        public static Object Gt(Object[] args)
        {
            return If.Is<Double, Double>(args, GtNumbers) ??
                If.Is<Double[,], Double[,]>(args, GtMatrices) ??
                If.Is<Double[,], Double>(args, GtMatNum) ??
                If.Is<Double, Double[,]>(args, GtNumMat) ??
                (args[1].ToNumber() > args[0].ToNumber());
        }

        public static Object Geq(Object[] args)
        {
            return If.Is<Double, Double>(args, GeqNumbers) ??
                If.Is<Double[,], Double[,]>(args, GeqMatrices) ??
                If.Is<Double[,], Double>(args, GeqMatNum) ??
                If.Is<Double, Double[,]>(args, GeqNumMat) ??
                (args[1].ToNumber() >= args[0].ToNumber());
        }

        public static Object Lt(Object[] args)
        {
            return If.Is<Double, Double>(args, LtNumbers) ??
                If.Is<Double[,], Double[,]>(args, LtMatrices) ??
                If.Is<Double[,], Double>(args, LtMatNum) ??
                If.Is<Double, Double[,]>(args, LtNumMat) ??
                (args[1].ToNumber() < args[0].ToNumber());
        }

        public static Object Leq(Object[] args)
        {
            return If.Is<Double, Double>(args, LeqNumbers) ??
                If.Is<Double[,], Double[,]>(args, LeqMatrices) ??
                If.Is<Double[,], Double>(args, LeqMatNum) ??
                If.Is<Double, Double[,]>(args, LeqNumMat) ??
                (args[1].ToNumber() <= args[0].ToNumber());
        }

        public static Object Pipe(Object[] args)
        {
            return If.Is<Function, Object>(args, InvokeFunction);
        }

        #endregion
    }
}
