namespace Mages.Core.Runtime.Converters
{
    using System;
    using System.Collections.Generic;

    static class TypeCategories
    {
        public static readonly Dictionary<Type, List<Type>> Mapping = new Dictionary<Type, List<Type>>
        {
            { typeof(Double), new List<Type> { typeof(Double), typeof(Single), typeof(Decimal), typeof(Byte), typeof(UInt16), typeof(UInt32), typeof(UInt64), typeof(Int16), typeof(Int32), typeof(Int64) } },
            { typeof(Boolean), new List<Type> { typeof(Boolean) } },
            { typeof(String), new List<Type> { typeof(String), typeof(Char) } },
            { typeof(Double[,]), new List<Type> { typeof(Double[,]), typeof(Double[]), typeof(List<Double>) } },
            { typeof(Function), new List<Type> { typeof(Function), typeof(Delegate) } },
            { typeof(IDictionary<String, Object>), new List<Type> { typeof(IDictionary<String, Object>), typeof(Object) } }
        };

        public static Type FindPrimitive(this Type type)
        {
            foreach (var category in Mapping)
            {
                foreach (var value in category.Value)
                {
                    if (value.IsAssignableFrom(type))
                    {
                        return category.Key;
                    }
                }
            }

            return type;
        }
    }
}
