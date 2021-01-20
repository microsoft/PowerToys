namespace Mages.Core.Runtime.Converters
{
    using System;
    using System.Collections.Generic;

    static class StandardConverters
    {
        public static readonly List<TypeConverter> List = new List<TypeConverter>
        {
            TypeConverter.Create<Double, Single>(x => (Single)x),
            TypeConverter.Create<Double, Decimal>(x => (Decimal)x),
            TypeConverter.Create<Double, Byte>(x => (Byte)Math.Max(0, Math.Min(255, x))),
            TypeConverter.Create<Double, Int16>(x => (Int16)x),
            TypeConverter.Create<Double, UInt16>(x => (UInt16)Math.Max(0, x)),
            TypeConverter.Create<Double, Int32>(x => (Int32)x),
            TypeConverter.Create<Double, UInt32>(x => (UInt32)Math.Max(0, x)),
            TypeConverter.Create<Double, Int64>(x => (Int64)x),
            TypeConverter.Create<Double, UInt64>(x => (UInt64)Math.Max(0, x)),
            TypeConverter.Create<Double, Boolean>(x => x.ToBoolean()),
            TypeConverter.Create<Double, String>(x => Stringify.This(x)),
            TypeConverter.Create<Double, Double[,]>(x => x.ToMatrix()),

            TypeConverter.Create<String, Double>(x => x.ToNumber()),
            TypeConverter.Create<String, Boolean>(x => x.ToBoolean()),
            TypeConverter.Create<String, Char>(x => x.Length > 0 ? x[0] : Char.MinValue),

            TypeConverter.Create<Boolean, Double>(x => x.ToNumber()),
            TypeConverter.Create<Boolean, String>(x => Stringify.This(x)),
            TypeConverter.Create<Boolean, Double[,]>(x => x.ToMatrix()),

            TypeConverter.Create<Double[,], Boolean>(x => x.ToBoolean()),
            TypeConverter.Create<Double[,], Double>(x => x.ToNumber()),
            TypeConverter.Create<Double[,], Double[]>(x => x.ToVector()),
            TypeConverter.Create<Double[,], List<Double>>(x => x.ToList()),

            TypeConverter.Create<IDictionary<String, Object>, String>(x => Stringify.This(x)),
            TypeConverter.Create<IDictionary<String, Object>, Boolean>(x => x.ToBoolean()),
            TypeConverter.Create<IDictionary<String, Object>, Double>(x => x.ToNumber()),

            TypeConverter.Create<Single, Double>(x => (Double)x),
            TypeConverter.Create<Int16, Double>(x => (Double)x),
            TypeConverter.Create<UInt16, Double>(x => (Double)x),
            TypeConverter.Create<Int32, Double>(x => (Double)x),
            TypeConverter.Create<UInt32, Double>(x => (Double)x),
            TypeConverter.Create<Int64, Double>(x => (Double)x),
            TypeConverter.Create<UInt64, Double>(x => (Double)x),
            TypeConverter.Create<Decimal, Double>(x => (Double)x),
            TypeConverter.Create<Byte, Double>(x => (Double)x),
            TypeConverter.Create<Char, String>(x => x.ToString()),
            TypeConverter.Create<Double[], Double[,]>(x => x.ToMatrix()),
            TypeConverter.Create<List<Double>, Double[,]>(x => x.ToMatrix()),
            TypeConverter.Create<Delegate, Function>(Helpers.WrapFunction),
            TypeConverter.Create<Array, Dictionary<String, Object>>(Helpers.WrapArray)
        };

        public static readonly Func<Object, Object> Identity = _ => _;

        public static readonly Func<Object, Object> Default = _ => _ as IDictionary<String, Object> ?? WrapperObject.CreateFor(_);
    }
}
