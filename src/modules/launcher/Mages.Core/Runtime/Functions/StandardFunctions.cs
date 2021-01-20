namespace Mages.Core.Runtime.Functions
{
    using Mages.Core.Runtime.Converters;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The collection of all standard functions.
    /// </summary>
    public static class StandardFunctions
    {
        /// <summary>
        /// Exposes the + operator as a function.
        /// </summary>
        public static readonly Function Add = new Function(args => 
        {
            return Curry.MinTwo(Add, args) ??
                BinaryOperators.Add(args);
        });

        /// <summary>
        /// Exposes the - operator as a function.
        /// </summary>
        public static readonly Function Sub = new Function(args => 
        {
            return Curry.MinTwo(Sub, args) ??
                BinaryOperators.Sub(args);
        });

        /// <summary>
        /// Exposes the * operator as a function.
        /// </summary>
        public static readonly Function Mul = new Function(args => 
        {
            return Curry.MinTwo(Mul, args) ??
                BinaryOperators.Mul(args);
        });

        /// <summary>
        /// Exposes the / operator as a function.
        /// </summary>
        public static readonly Function RDiv = new Function(args => 
        {
            return Curry.MinTwo(RDiv, args) ??
                BinaryOperators.RDiv(args);
        });

        /// <summary>
        /// Exposes the \ operator as a function.
        /// </summary>
        public static readonly Function LDiv = new Function(args => 
        {
            return Curry.MinTwo(LDiv, args) ??
                BinaryOperators.LDiv(args);
        });

        /// <summary>
        /// Exposes the ^ operator as a function.
        /// </summary>
        public static readonly Function Pow = new Function(args => 
        {
            return Curry.MinTwo(Pow, args) ??
                BinaryOperators.Pow(args);
        });

        /// <summary>
        /// Exposes the % operator as a function.
        /// </summary>
        public static readonly Function Mod = new Function(args => 
        {
            return Curry.MinTwo(Mod, args) ??
                BinaryOperators.Mod(args);
        });

        /// <summary>
        /// Exposes the &amp;&amp; operator as a function.
        /// </summary>
        public static readonly Function And = new Function(args => 
        {
            return Curry.MinTwo(And, args) ??
                BinaryOperators.And(args);
        });

        /// <summary>
        /// Exposes the || operator as a function.
        /// </summary>
        public static readonly Function Or = new Function(args => 
        {
            return Curry.MinTwo(Or, args) ??
                BinaryOperators.Or(args);
        });

        /// <summary>
        /// Exposes the == operator as a function.
        /// </summary>
        public static readonly Function Eq = new Function(args => 
        {
            return Curry.MinTwo(Eq, args) ??
                BinaryOperators.Eq(args);
        });

        /// <summary>
        /// Exposes the != operator as a function.
        /// </summary>
        public static readonly Function Neq = new Function(args => 
        {
            return Curry.MinTwo(Neq, args) ??
                BinaryOperators.Neq(args);
        });

        /// <summary>
        /// Exposes the &gt; operator as a function.
        /// </summary>
        public static readonly Function Gt = new Function(args => 
        {
            return Curry.MinTwo(Gt, args) ??
                BinaryOperators.Gt(args);
        });

        /// <summary>
        /// Exposes the &gt;= operator as a function.
        /// </summary>
        public static readonly Function Geq = new Function(args => 
        {
            return Curry.MinTwo(Geq, args) ??
                BinaryOperators.Geq(args);
        });

        /// <summary>
        /// Exposes the &lt; operator as a function.
        /// </summary>
        public static readonly Function Lt = new Function(args => 
        {
            return Curry.MinTwo(Lt, args) ??
                BinaryOperators.Lt(args);
        });

        /// <summary>
        /// Exposes the &lt;= operator as a function.
        /// </summary>
        public static readonly Function Leq = new Function(args => 
        {
            return Curry.MinTwo(Leq, args) ??
                BinaryOperators.Leq(args);
        });

        /// <summary>
        /// Exposes the | operator as a function.
        /// </summary>
        public static readonly Function Pipe = new Function(args =>
        {
            return Curry.MinTwo(Pipe, args) ??
                BinaryOperators.Pipe(args);
        });

        /// <summary>
        /// Exposes the ~ operator as a function.
        /// </summary>
        public static readonly Function Not = new Function(args => 
        {
            return Curry.MinOne(Not, args) ??
                UnaryOperators.Not(args);
        });

        /// <summary>
        /// Exposes the + operator as a function.
        /// </summary>
        public static readonly Function Positive = new Function(args => 
        {
            return Curry.MinOne(Positive, args) ??
                UnaryOperators.Positive(args);
        });

        /// <summary>
        /// Exposes the - operator as a function.
        /// </summary>
        public static readonly Function Negative = new Function(args => 
        {
            return Curry.MinOne(Negative, args) ??
                UnaryOperators.Negative(args);
        });

        /// <summary>
        /// Exposes the ! operator as a function.
        /// </summary>
        public static readonly Function Factorial = new Function(args => 
        {
            return Curry.MinOne(Factorial, args) ??
                UnaryOperators.Factorial(args);
        });

        /// <summary>
        /// Exposes the ' operator as a function.
        /// </summary>
        public static readonly Function Transpose = new Function(args => 
        {
            return Curry.MinOne(Transpose, args) ??
                UnaryOperators.Transpose(args);
        });

        /// <summary>
        /// Wraps the Math.Abs function.
        /// </summary>
        public static readonly Function Abs = new Function(args => 
        {
            return Curry.MinOne(Abs, args) ??
                UnaryOperators.Abs(args);
        });

        /// <summary>
        /// Exposes the &amp; operator as a function.
        /// </summary>
        public static readonly Function Type = new Function(args => 
        {
            return Curry.MinOne(Type, args) ?? 
                UnaryOperators.Type(args);
        });

        /// <summary>
        /// Wraps the Math.Sqrt function.
        /// </summary>
        public static readonly Function Sqrt = new Function(args => 
        {
            return Curry.MinOne(Sqrt, args) ??
                If.Is<Double>(args, x => Math.Sqrt(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Sqrt)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Sqrt)) ??
                Math.Sqrt(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Sign function.
        /// </summary>
        public static readonly Function Sign = new Function(args => 
        {
            return Curry.MinOne(Sign, args) ??
                If.Is<Double>(args, x => Mathx.Sign(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Sign)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Sign)) ??
                Mathx.Sign(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the gamma function.
        /// </summary>
        public static readonly Function Gamma = new Function(args =>
        {
            return Curry.MinOne(Gamma, args) ??
                If.Is<Double>(args, x => Mathx.Gamma(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Gamma)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Gamma)) ??
                Mathx.Gamma(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Ceiling function.
        /// </summary>
        public static readonly Function Ceil = new Function(args => 
        {
            return Curry.MinOne(Ceil, args) ??
                If.Is<Double>(args, x => Math.Ceiling(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Ceiling)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Ceil)) ??
                Math.Ceiling(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Floor function.
        /// </summary>
        public static readonly Function Floor = new Function(args => 
        {
            return Curry.MinOne(Floor, args) ??
                If.Is<Double>(args, x => Math.Floor(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Floor)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Floor)) ??
                Math.Floor(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Round function.
        /// </summary>
        public static readonly Function Round = new Function(args => 
        {
            return Curry.MinOne(Round, args) ??
                If.Is<Double>(args, x => Math.Round(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Round)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Round)) ??
                Math.Round(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Exp function.
        /// </summary>
        public static readonly Function Exp = new Function(args => 
        {
            return Curry.MinOne(Exp, args) ??
                If.Is<Double>(args, x => Math.Exp(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Exp)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Exp)) ??
                Math.Exp(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Log function.
        /// </summary>
        public static readonly Function Log = new Function(args => 
        {
            return Curry.MinOne(Log, args) ??
                If.Is<Double>(args, x => Math.Log(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Log)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Log)) ??
                Math.Log(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Sin function.
        /// </summary>
        public static readonly Function Sin = new Function(args => 
        {
            return Curry.MinOne(Sin, args) ??
                If.Is<Double>(args, x => Math.Sin(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Sin)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Sin)) ??
                Math.Sin(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Cos function.
        /// </summary>
        public static readonly Function Cos = new Function(args => 
        {
            return Curry.MinOne(Cos, args) ??
                If.Is<Double>(args, x => Math.Cos(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Cos)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Cos)) ??
                Math.Cos(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Tan function.
        /// </summary>
        public static readonly Function Tan = new Function(args =>
        {
            return Curry.MinOne(Tan, args) ??
                If.Is<Double>(args, x => Math.Tan(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Tan)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Tan)) ??
                Math.Tan(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the cot function.
        /// </summary>
        public static readonly Function Cot = new Function(args =>
        {
            return Curry.MinOne(Cot, args) ??
                If.Is<Double>(args, x => Mathx.Cot(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Cot)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Cot)) ??
                Mathx.Cot(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the sec function.
        /// </summary>
        public static readonly Function Sec = new Function(args =>
        {
            return Curry.MinOne(Sec, args) ??
                If.Is<Double>(args, x => Mathx.Sec(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Sec)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Sec)) ??
                Mathx.Sec(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the csc function.
        /// </summary>
        public static readonly Function Csc = new Function(args =>
        {
            return Curry.MinOne(Csc, args) ??
                If.Is<Double>(args, x => Mathx.Csc(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Csc)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Csc)) ??
                Mathx.Csc(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Sinh function.
        /// </summary>
        public static readonly Function Sinh = new Function(args =>
        {
            return Curry.MinOne(Sinh, args) ??
                If.Is<Double>(args, x => Math.Sinh(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Sinh)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Sinh)) ??
                Math.Sinh(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Cosh function.
        /// </summary>
        public static readonly Function Cosh = new Function(args =>
        {
            return Curry.MinOne(Cosh, args) ??
                If.Is<Double>(args, x => Math.Cosh(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Cosh)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Cosh)) ??
                Math.Cosh(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Tanh function.
        /// </summary>
        public static readonly Function Tanh = new Function(args =>
        {
            return Curry.MinOne(Tanh, args) ??
                If.Is<Double>(args, x => Math.Tanh(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Tanh)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Tanh)) ??
                Math.Tanh(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the coth function.
        /// </summary>
        public static readonly Function Coth = new Function(args =>
        {
            return Curry.MinOne(Coth, args) ??
                If.Is<Double>(args, x => Mathx.Coth(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Coth)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Coth)) ??
                Mathx.Coth(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the sech function.
        /// </summary>
        public static readonly Function Sech = new Function(args =>
        {
            return Curry.MinOne(Sech, args) ??
                If.Is<Double>(args, x => Mathx.Sech(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Sech)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Sech)) ??
                Mathx.Sech(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the csch function.
        /// </summary>
        public static readonly Function Csch = new Function(args =>
        {
            return Curry.MinOne(Csch, args) ??
                If.Is<Double>(args, x => Mathx.Csch(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Csch)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(Csch)) ??
                Mathx.Csch(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Asin function.
        /// </summary>
        public static readonly Function ArcSin = new Function(args =>
        {
            return Curry.MinOne(ArcSin, args) ??
                If.Is<Double>(args, x => Math.Asin(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Asin)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(ArcSin)) ??
                Math.Asin(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Acos function.
        /// </summary>
        public static readonly Function ArcCos = new Function(args =>
        {
            return Curry.MinOne(ArcCos, args) ??
                If.Is<Double>(args, x => Math.Acos(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Acos)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(ArcCos)) ??
                Math.Acos(args[0].ToNumber());
        });

        /// <summary>
        /// Wraps the Math.Atan function.
        /// </summary>
        public static readonly Function ArcTan = new Function(args =>
        {
            return Curry.MinOne(ArcTan, args) ??
                If.Is<Double>(args, x => Math.Atan(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Math.Atan)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(ArcTan)) ??
                Math.Atan(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the arccot function.
        /// </summary>
        public static readonly Function ArcCot = new Function(args =>
        {
            return Curry.MinOne(ArcCot, args) ??
                If.Is<Double>(args, x => Mathx.Acot(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Acot)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(ArcCot)) ??
                Mathx.Acot(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the asec function.
        /// </summary>
        public static readonly Function ArcSec = new Function(args =>
        {
            return Curry.MinOne(ArcSec, args) ??
                If.Is<Double>(args, x => Mathx.Asec(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Asec)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(ArcSec)) ??
                Mathx.Asec(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the acsc function.
        /// </summary>
        public static readonly Function ArcCsc = new Function(args =>
        {
            return Curry.MinOne(ArcCsc, args) ??
                If.Is<Double>(args, x => Mathx.Acsc(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Acsc)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(ArcCsc)) ??
                Mathx.Acsc(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the arsinh function.
        /// </summary>
        public static readonly Function ArSinh = new Function(args =>
        {
            return Curry.MinOne(ArSinh, args) ??
                If.Is<Double>(args, x => Mathx.Asinh(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Asinh)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(ArSinh)) ??
                Mathx.Asinh(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the arcosh function.
        /// </summary>
        public static readonly Function ArCosh = new Function(args =>
        {
            return Curry.MinOne(ArCosh, args) ??
                If.Is<Double>(args, x => Mathx.Acosh(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Acosh)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(ArCosh)) ??
                Mathx.Acosh(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the artanh function.
        /// </summary>
        public static readonly Function ArTanh = new Function(args =>
        {
            return Curry.MinOne(ArTanh, args) ??
                If.Is<Double>(args, x => Mathx.Atanh(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Atanh)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(ArTanh)) ??
                Mathx.Atanh(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the arcoth function.
        /// </summary>
        public static readonly Function ArCoth = new Function(args =>
        {
            return Curry.MinOne(ArCoth, args) ??
                If.Is<Double>(args, x => Mathx.Acoth(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Acoth)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(ArCoth)) ??
                Mathx.Acoth(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the asech function.
        /// </summary>
        public static readonly Function ArSech = new Function(args =>
        {
            return Curry.MinOne(ArSech, args) ??
                If.Is<Double>(args, x => Mathx.Asech(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Asech)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(ArSech)) ??
                Mathx.Asech(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the acsch function.
        /// </summary>
        public static readonly Function ArCsch = new Function(args =>
        {
            return Curry.MinOne(ArCsch, args) ??
                If.Is<Double>(args, x => Mathx.Acsch(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(Mathx.Acsch)) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(ArCsch)) ??
                Mathx.Acsch(args[0].ToNumber());
        });

        /// <summary>
        /// Contains the random function.
        /// </summary>
        public static readonly Function Rand = new Function(args => 
        {
            return (args.Length > 1 ? If.Is<Double, Double>(args, SimpleRandom.CreateMatrix) : null) ??
                (args.Length > 0 ? If.Is<Double>(args, SimpleRandom.CreateVector) : null) ??
                SimpleRandom.GetNumber();
        });

        /// <summary>
        /// Contains the throw function.
        /// </summary>
        public static readonly Function Throw = new Function(args =>
        {
            return Curry.MinOne(Throw, args) ??
                new Exception(Stringify.This(args[0]));
        });

        /// <summary>
        /// Contains the catch function.
        /// </summary>
        public static readonly Function Catch = new Function(args =>
        {
            return Curry.MinOne(Catch, args) ??
                If.Is<Function>(args, Helpers.SafeExecute);
        });

        /// <summary>
        /// Contains the length function.
        /// </summary>
        public static readonly Function Length = new Function(args =>
        {
            return Curry.MinOne(Length, args) ??
                If.Is<Double[,]>(args, x => (Double)x.GetCount()) ??
                If.Is<IDictionary<String, Object>>(args, x => (Double)x.Count) ??
                If.Is<String>(args, x => (Double)x.Length) ??
                1.0;
        });

        /// <summary>
        /// Contains the sum function.
        /// </summary>
        public static readonly Function Sum = new Function(args =>
        {
            return Curry.MinOne(Sum, args) ??
                If.Is<Double[,]>(args, x => x.Reduce((a, b) => a + b)) ??
                If.Is<Dictionary<String, Object>>(args, obj => obj.Sum(m => m.Value.ToNumber())) ??
                args.Select(m => m.ToNumber()).Sum();
        });

        /// <summary>
        /// Wraps the Math.Min function.
        /// </summary>
        public static readonly Function Min = new Function(args =>
        {
            return Curry.MinOne(Min, args) ??
                If.Is<Double[,]>(args, x => x.Reduce(Math.Min)) ??
                If.Is<Dictionary<String, Object>>(args, obj => obj.Min(m => m.Value.ToNumber())) ??
                args.Select(m => m.ToNumber()).Min();
        });

        /// <summary>
        /// Wraps the Math.Max function.
        /// </summary>
        public static readonly Function Max = new Function(args =>
        {
            return Curry.MinOne(Max, args) ??
                If.Is<Double[,]>(args, x => x.Reduce(Math.Max)) ??
                If.Is<Dictionary<String, Object>>(args, obj => obj.Max(m => m.Value.ToNumber())) ??
                args.Select(m => m.ToNumber()).Max();
        });

        /// <summary>
        /// Wraps the Enumerable.OrderBy function.
        /// </summary>
        public static readonly Function Sort = new Function(args =>
        {
            return Curry.MinOne(Sort, args) ??
                If.Is<Double[,]>(args, x => x.ToVector().OrderBy(y => y).ToMatrix()) ??
                args.Select(m => m.ToNumber()).OrderBy(y => y).ToMatrix();
        });

        /// <summary>
        /// Wraps the Enumerable.Reverse function.
        /// </summary>
        public static readonly Function Reverse = new Function(args =>
        {
            return Curry.MinOne(Reverse, args) ??
                If.Is<Double[,]>(args, x => x.ToVector().Reverse().ToMatrix()) ??
                If.Is<String>(args, x => new String(x.Reverse().ToArray())) ??
                args.Reverse().ToArray();
        });

        /// <summary>
        /// Wraps the Double.IsNaN function.
        /// </summary>
        public static readonly Function IsNaN = new Function(args =>
        {
            return Curry.MinOne(IsNaN, args) ??
                If.Is<Double>(args, x => Double.IsNaN(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(y => Double.IsNaN(y).ToNumber())) ??
                false;
        });

        /// <summary>
        /// Contains the is integer function.
        /// </summary>
        public static readonly Function IsInt = new Function(args =>
        {
            return Curry.MinOne(IsInt, args) ??
                If.Is<Double>(args, x => Logic.IsInteger(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(y => Logic.IsInteger(y).ToNumber())) ??
                false;
        });

        /// <summary>
        /// Contains the is prime function.
        /// </summary>
        public static readonly Function IsPrime = new Function(args =>
        {
            return Curry.MinOne(IsPrime, args) ??
                If.Is<Double>(args, x => Logic.IsPrime(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(y => Logic.IsPrime(y).ToNumber())) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(IsPrime)) ??
                false;
        });

        /// <summary>
        /// Wraps the Double.IsInfinity function.
        /// </summary>
        public static readonly Function IsInfty = new Function(args =>
        {
            return Curry.MinOne(IsPrime, args) ??
                If.Is<Double>(args, x => Double.IsInfinity(x)) ??
                If.Is<Double[,]>(args, x => x.ForEach(y => Double.IsInfinity(y).ToNumber())) ??
                If.Is<IDictionary<String, Object>>(args, o => o.Map(IsInfty)) ??
                false;
        });

        /// <summary>
        /// Contains the any function.
        /// </summary>
        public static readonly Function Any = new Function(args =>
        {
            return Curry.MinOne(Any, args) ??
                If.Is<Double[,]>(args, x => x.AnyTrue()) ??
                If.Is<IDictionary<String, Object>>(args, x => x.AnyTrue()) ??
                args.Any(m => m.ToBoolean());
        });

        /// <summary>
        /// Contains the all function.
        /// </summary>
        public static readonly Function All = new Function(args =>
        {
            return Curry.MinOne(All, args) ??
                If.Is<Double[,]>(args, x => x.AllTrue()) ??
                If.Is<IDictionary<String, Object>>(args, x => x.AllTrue()) ??
                args.All(m => m.ToBoolean());
        });

        /// <summary>
        /// Contains the is function.
        /// </summary>
        public static readonly Function Is = new Function(args =>
        {
            return Curry.MinTwo(Is, args) ?? 
                If.Is<String>(args, type => type == args[1].ToType()) ??
                If.Is<IDictionary<String, Object>>(args, type => type.Satisfies(args[1]));
        });

        /// <summary>
        /// Contains the as function.
        /// </summary>
        public static readonly Function As = new Function(args =>
        {
            return Curry.MinTwo(As, args) ??
                If.Is<String>(args, type => args[1].To(type));
        });

        /// <summary>
        /// Contains the list function.
        /// </summary>
        public static readonly Function List = new Function(Helpers.ToArrayObject);

        /// <summary>
        /// Contains the keys function.
        /// </summary>
        public static readonly Function Keys = new Function(args =>
        {
            return Curry.MinOne(Map, args) ??
                If.Is<IDictionary<String, Object>>(args, x => x.GetKeys()) ??
                If.Is<Double[,]>(args, x => x.GetKeys());
        });

        /// <summary>
        /// Contains the map function.
        /// </summary>
        public static readonly Function Map = new Function(args =>
        {
            return Curry.MinTwo(Map, args) ??
                If.Is<Function, Double[,]>(args, (f, m) => m.Map(f)) ??
                If.Is<Function, IDictionary<String, Object>>(args, (f, o) => o.Map(f)) ??
                If.Is<Function>(args, f => f(new[] { args[1] }));
        });

        /// <summary>
        /// Contains the reduce function.
        /// </summary>
        public static readonly Function Reduce = new Function(args =>
        {
            return Curry.Min(3, Reduce, args) ??
                If.IsAnyT2<Function, Double[,]>(args, (f, s, m) => m.Reduce(f, s)) ??
                If.IsAnyT2<Function, IDictionary<String, Object>>(args, (f, s, o) => o.Reduce(f, s)) ??
                If.Is<Function>(args, f => f(new[] { args[1], args[2] }));
        });

        /// <summary>
        /// Contains the where function.
        /// </summary>
        public static readonly Function Where = new Function(args =>
        {
            return Curry.MinTwo(Where, args) ??
                If.Is<Function, String>(args, (f, m) => m.Where(f)) ??
                If.Is<Function, Double[,]>(args, (f, m) => m.Where(f)) ??
                If.Is<Function, IDictionary<String, Object>>(args, (f, o) => o.Where(f)) ??
                If.Is<Function>(args, f => f(new[] { args[1] }).ToBoolean() ? args[1] : null);
        });

        /// <summary>
        /// Contains the zip function.
        /// </summary>
        public static readonly Function Zip = new Function(args =>
        {
            return Curry.MinTwo(Zip, args) ??
                args[0].ToObject().Zip(args[1].ToObject());
        });

        /// <summary>
        /// Contains the concat function.
        /// </summary>
        public static readonly Function Concat = new Function(args =>
        {
            return Curry.MinTwo(Concat, args) ??
                args[0].ToObject().Merge(args[1].ToObject());
        });

        /// <summary>
        /// Contains the intersection function.
        /// </summary>
        public static readonly Function Intersection = new Function(args =>
        {
            return Curry.MinTwo(Intersection, args) ??
                args[0].ToObject().Intersect(args[1].ToObject()).ToDictionary(m => m.Key, m => m.Value);
        });

        /// <summary>
        /// Contains the union function.
        /// </summary>
        public static readonly Function Union = new Function(args =>
        {
            return Curry.MinTwo(Union, args) ??
                args[0].ToObject().Union(args[1].ToObject()).GroupBy(m => m.Key).
                    ToDictionary(m => m.Key, m => m.Count() == 1 ? m.Single().Value : 
                        Helpers.ToArrayObject(m.Select(n => n.Value).Distinct().ToArray()));
        });

        /// <summary>
        /// Contains the except function.
        /// </summary>
        public static readonly Function Except = new Function(args =>
        {
            return Curry.MinTwo(Except, args) ??
                args[1].ToObject().Except(args[0].ToObject()).ToDictionary(m => m.Key, m => m.Value);
        });

        /// <summary>
        /// Wraps the String.Format function.
        /// </summary>
        public static readonly Function Format = new Function(args =>
        {
            return Curry.MinOne(Format, args) ??
                If.Is<String>(args, s => 
                    String.Format(s, args.Skip(1).Select(Stringify.This).ToArray()));
        });

        /// <summary>
        /// Contains the hasKey function.
        /// </summary>
        public static readonly Function HasKey = new Function(args =>
        {
            return Curry.MinTwo(HasKey, args) ??
                If.Is<String, IDictionary<String, Object>>(args, (name, obj) => obj.ContainsKey(name));
        });

        /// <summary>
        /// Contains the getValue function.
        /// </summary>
        public static readonly Function GetValue = new Function(args =>
        {
            return Curry.MinTwo(GetValue, args) ??
                If.Is<String, IDictionary<String, Object>>(args, (name, obj) => obj.GetProperty(name));
        });

        /// <summary>
        /// Contains the shuffle function.
        /// </summary>
        public static readonly Function Shuffle = new Function(args =>
        {
            return Curry.MinOne(Shuffle, args) ??
                Curry.Shuffle(args) ??
                Curry.Min(args.Length + 1, Shuffle, args);
        });

        /// <summary>
        /// Contains the regex function.
        /// </summary>
        public static readonly Function Regex = new Function(args =>
        {
            return Curry.MinTwo(Regex, args) ??
                If.Is<String, String>(args, (test, value) => Helpers.MatchString(test, value)) ??
                false;
        });

        /// <summary>
        /// Contains the clip function.
        /// </summary>
        public static readonly Function Clip = new Function(args =>
        {
            return Curry.MinThree(Clamp, args) ??
                If.Is<Double, Double, String>(args, (from, to, value) => value.Clip((int)from, (int)to));
        });

        /// <summary>
        /// Contains the clamp function.
        /// </summary>
        public static readonly Function Clamp = new Function(args =>
        {
            return Curry.MinThree(Clamp, args) ??
                If.Is<Double, Double, Double>(args, (min, max, value) => value.Clamp(min, max)) ??
                If.Is<Double, Double, Double[,]>(args, (min, max, mat) => mat.ForEach(x => x.Clamp(min, max))) ??
                If.Is<Double, Double, String>(args, (min, max, value) => value.Clamp((int)min, (int)max));
        });

        /// <summary>
        /// Contains the lerp function.
        /// </summary>
        public static readonly Function Lerp = new Function(args =>
        {
            return Curry.MinThree(Lerp, args) ??
                If.Is<Double, Double, Double>(args, (min, max, value) => value.Lerp(min, max)) ??
                If.Is<Double, Double, Double[,]>(args, (min, max, mat) => mat.ForEach(x => x.Lerp(min, max)));
        });
    }
}
