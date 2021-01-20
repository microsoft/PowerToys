namespace Mages.Core.Runtime.Converters
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// A set of useful extension methods for type conversions.
    /// </summary>
    public static class ConverterExtensions
    {
        /// <summary>
        /// Returns the type of the given value.
        /// </summary>
        /// <param name="value">The value to get the type of.</param>
        /// <returns>The MAGES type string.</returns>
        public static String ToType(this Object value)
        {
            if (value is Double) return "Number";
            if (value is String) return "String";
            if (value is Boolean) return "Boolean";
            if (value is Double[,]) return "Matrix";
            if (value is Function) return "Function";
            if (value is IDictionary<String, Object>) return "Object";
            return "Undefined";
        }

        /// <summary>
        /// Converts the given value to the specified type.
        /// </summary>
        /// <param name="value">The type to convert.</param>
        /// <param name="type">The destination type.</param>
        /// <returns>The converted value.</returns>
        public static Object To(this Object value, String type)
        {
            switch (type)
            {
                case "Number": return value.ToNumber();
                case "String": return Stringify.This(value);
                case "Boolean": return value.ToBoolean();
                case "Matrix": return value.ToNumber().ToMatrix();
                case "Function": return value as Function;
                case "Object": return value as IDictionary<String, Object>;
                case "Undefined": return null;
            }

            return null;
        }

        /// <summary>
        /// Returns the boolean representation of the given value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The boolean representation of the value.</returns>
        public static Boolean ToBoolean(this Object value)
        {
            if (value is Boolean)
            {
                return (Boolean)value;
            }
            else if (value != null)
            {
                var nval = value as Double?;
                var sval = value as String;
                var mval = value as Double[,];
                var oval = value as IDictionary<String, Object>;

                if (nval.HasValue)
                {
                    return nval.Value.ToBoolean();
                }
                else if (sval != null)
                {
                    return sval.ToBoolean();
                }
                else if (mval != null)
                {
                    return mval.ToBoolean();
                }
                else if (oval != null)
                {
                    return oval.ToBoolean();
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the boolean representation of the given numeric value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The boolean representation of the value.</returns>
        public static Boolean ToBoolean(this Double value)
        {
            return value != 0.0;;
        }

        /// <summary>
        /// Returns the boolean representation of the given string value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The boolean representation of the value.</returns>
        public static Boolean ToBoolean(this String value)
        {
            return value.Length > 0;
        }

        /// <summary>
        /// Returns the boolean representation of the given matrix value.
        /// </summary>
        /// <param name="matrix">The matrix to convert.</param>
        /// <returns>The boolean representation of the value.</returns>
        public static Boolean ToBoolean(this Double[,] matrix)
        {
            return matrix.AnyTrue();
        }

        /// <summary>
        /// Returns the boolean representation of the given object value.
        /// </summary>
        /// <param name="obj">The obj to convert.</param>
        /// <returns>The boolean representation of the value.</returns>
        public static Boolean ToBoolean(this IDictionary<String, Object> obj)
        {
            return obj.Count > 0;
        }

        /// <summary>
        /// Returns the object representation of the given value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The object representation of the value.</returns>
        public static IDictionary<String, Object> ToObject(this Object value)
        {
            if (value is IDictionary<String, Object>)
            {
                return (IDictionary<String, Object>)value;
            }
            else if (value is Double[,])
            {
                var matrix = (Double[,])value;
                var result = new Dictionary<String, Object>();
                var rows = matrix.GetRows();
                var columns = matrix.GetColumns();

                for (int i = 0, k = 0; i < rows; i++)
                {
                    for (var j = 0; j < columns; j++, k++)
                    {
                        result[k.ToString()] = matrix[i, j];
                    }
                }

                return result;
            }
            else
            {
                var result = new Dictionary<String, Object>();

                if (value != null)
                {
                    result["0"] = value;
                }

                return result;
            }
        }

        /// <summary>
        /// Returns the number representation of the given value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The number representation of the value.</returns>
        public static Double ToNumber(this Object value)
        {
            if (value is Double)
            {
                return (Double)value;
            }
            else if (value != null)
            {
                var bval = value as Boolean?;
                var sval = value as String;
                var mval = value as Double[,];

                if (bval.HasValue)
                {
                    return bval.Value.ToNumber();
                }
                else if (sval != null)
                {
                    return sval.ToNumber();
                }
                else if (mval != null)
                {
                    return mval.ToNumber();
                }
            }

            return Double.NaN;
        }

        /// <summary>
        /// Returns the number representation of the given boolean value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The number representation of the value.</returns>
        public static Double ToNumber(this Boolean value)
        {
            return value ? 1.0 : 0.0;
        }

        /// <summary>
        /// Returns the number representation of the given string value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The number representation of the value.</returns>
        public static Double ToNumber(this String value)
        {
            var result = default(Double);

            if (!Double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            {
                return Double.NaN;
            }

            return result;
        }

        /// <summary>
        /// Returns the number representation of the given matrix value.
        /// </summary>
        /// <param name="matrix">The matrix to convert.</param>
        /// <returns>The number representation of the value.</returns>
        public static Double ToNumber(this Double[,] matrix)
        {
            if (matrix.GetRows() == 1 && matrix.GetColumns() == 1)
            {
                return matrix[0, 0];
            }

            return Double.NaN;
        }

        /// <summary>
        /// Returns the matrix representation of the given number value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The matrix representation of the value.</returns>
        public static Double[,] ToMatrix(this Double value)
        {
            return new Double[1, 1] { { value } };
        }

        /// <summary>
        /// Returns the matrix representation of the given boolean value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The matrix representation of the value.</returns>
        public static Double[,] ToMatrix(this Boolean value)
        {
            return new Double[1, 1] { { value.ToNumber() } };
        }

        /// <summary>
        /// Returns the matrix representation of the given numeric values.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The matrix representation of the value.</returns>
        public static Double[,] ToMatrix(this IEnumerable<Double> value)
        {
            var source = value.ToList();
            return source.ToMatrix();
        }

        /// <summary>
        /// Returns the matrix representation of the given numeric values.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The matrix representation of the value.</returns>
        public static Double[,] ToMatrix(this List<Double> value)
        {
            var length = value.Count;
            var matrix = new Double[1, length];

            for (var i = 0; i < length; i++)
            {
                matrix[0, i] = value[i];
            }

            return matrix;
        }

        /// <summary>
        /// Returns the vector representation of the given matrix value.
        /// </summary>
        /// <param name="matrix">The matrix to convert.</param>
        /// <returns>The matrix representation of the value.</returns>
        public static Double[] ToVector(this Double[,] matrix)
        {
            var rows = matrix.GetLength(0);
            var cols = matrix.GetLength(1);
            var vec = new Double[rows * cols];
            var k = 0;

            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < cols; j++)
                {
                    vec[k++] = matrix[i, j];
                }
            }

            return vec;
        }

        /// <summary>
        /// Returns the list representation of the given matrix value.
        /// </summary>
        /// <param name="matrix">The matrix to convert.</param>
        /// <returns>The list representation of the value.</returns>
        public static List<Double> ToList(this Double[,] matrix)
        {
            var rows = matrix.GetRows();
            var cols = matrix.GetColumns();
            var list = new List<Double>(rows * cols);

            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < cols; j++)
                {
                    list.Add(matrix[i, j]);
                }
            }

            return list;
        }

        /// <summary>
        /// Tries to get the index supplied from the given object.
        /// </summary>
        /// <param name="obj">The object to convert.</param>
        /// <param name="value">The retrieved index.</param>
        /// <returns>True if the index could be retrieved, otherwise false.</returns>
        public static Boolean TryGetIndex(this Object obj, out Int32 value)
        {
            if (obj is Double && ((Double)obj).IsInteger())
            {
                value = (Int32)(Double)obj;
                return true;
            }
            else
            {
                value = -1;
                return false;
            }   
        }
    }
}
