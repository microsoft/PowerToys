namespace Mages.Core.Runtime.Converters
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    static class ArrayConverters
    {
        public static Func<Object, Object> Get(Type from, Type to, Func<Object, Object> converter)
        {
            if (typeof(IDictionary<String, Object>).IsAssignableFrom(from))
            {
                return obj =>
                {
                    var items = (IDictionary<String, Object>)obj;
                    return ConvertArray(to, converter, items.Values, items.Count);
                };
            }
            else if (typeof(Double[,]).IsAssignableFrom(from))
            {
                return mat =>
                {
                    var values = (Double[,])mat;
                    return ConvertArray(to, converter, values, values.Length);
                };
            }
            else if (typeof(String).IsAssignableFrom(from))
            {
                return str =>
                {
                    var values = (String)str;
                    return ConvertArray(to, converter, values, values.Length);
                };
            }
            else
            {
                return any =>
                {
                    return ConvertArray(to, converter, new[] { any }, 1);
                };
            }
        }

        private static Object ConvertArray(Type to, Func<Object, Object> converter, IEnumerable values, Int32 length)
        {
            var result = Array.CreateInstance(to, length);
            var i = 0;

            foreach (var value in values)
            {
                result.SetValue(converter.Invoke(value), i++);
            }

            return result;
        }
    }
}
