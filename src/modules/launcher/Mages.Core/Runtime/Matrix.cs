namespace Mages.Core.Runtime
{
    using Mages.Core.Runtime.Converters;
    using System;
    using System.Collections.Generic;

    static class Matrix
    {
        public static Int32 GetRows(this Double[,] matrix)
        {
            return matrix.GetLength(0);
        }

        public static Int32 GetColumns(this Double[,] matrix)
        {
            return matrix.GetLength(1);
        }

        public static Int32 GetCount(this Double[,] matrix)
        {
            return matrix.GetLength(0) * matrix.GetLength(1);
        }

        public static Object GetKeys(this Double[,] matrix)
        {
            var result = new Dictionary<String, Object>();
            var length = matrix.Length;

            for (var i = 0; i < length; i++)
            {
                result[i.ToString()] = (Double)i;
            }

            return result;
        }

        public static Boolean TryGetIndices(this Double[,] matrix, Object[] arguments, out Int32 row, out Int32 col)
        {
            var rows = matrix.GetRows();
            var cols = matrix.GetColumns();
            var n = 0;
            row = -1;
            col = -1;

            if (arguments.Length == 1 && arguments[0].TryGetIndex(out n))
            {
                col = n % cols;
                row = n / cols;
            }
            else if (arguments.Length == 2 && arguments[0].TryGetIndex(out row) && arguments[1].TryGetIndex(out col))
            {
            }

            return row >= 0 && col >= 0 && row < rows && col < cols;
        }
        
        public static Object Getter(this Double[,] matrix, Object[] arguments)
        {
            var i = 0;
            var j = 0;

            if (matrix.TryGetIndices(arguments, out i, out j))
            {
                return matrix[i, j];
            }

            return null;
        }

        public static void Setter(this Double[,] matrix, Object[] arguments, Object value)
        {
            var i = 0;
            var j = 0;

            if (matrix.TryGetIndices(arguments, out i, out j))
            {
                matrix[i, j] = value.ToNumber();
            }
        }

        public static Double Abs(Double[,] matrix)
        {
            var sum = 0.0;
            var rows = matrix.GetRows();
            var cols = matrix.GetColumns();

            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < cols; j++)
                {
                    sum += matrix[i ,j] * matrix[i, j];
                }
            }

            return Math.Sqrt(sum);
        }

        public static Boolean Fits(this Double[,] a, Double[,] b)
        {
            var rowsA = a.GetRows();
            var rowsB = b.GetRows();
            var colsA = a.GetColumns();
            var colsB = b.GetColumns();

            return rowsA == rowsB && colsA == colsB;
        }

        public static Double[,] Add(this Double[,] a, Double[,] b)
        {
            if (a.Fits(b))
            {
                var rows = a.GetRows();
                var cols = a.GetColumns();
                var result = new Double[rows, cols];

                for (var i = 0; i < rows; i++)
                {
                    for (var j = 0; j < cols; j++)
                    {
                        result[i, j] = a[i, j] + b[i, j];
                    }
                }

                return result;
            }

            return null;
        }

        public static Double[,] Subtract(this Double[,] a, Double[,] b)
        {
            if (a.Fits(b))
            {
                var rows = a.GetRows();
                var cols = a.GetColumns();
                var result = new Double[rows, cols];

                for (var i = 0; i < rows; i++)
                {
                    for (var j = 0; j < cols; j++)
                    {
                        result[i, j] = a[i, j] - b[i, j];
                    }
                }

                return result;
            }

            return null;
        }

        public static Double[,] Multiply(this Double[,] a, Double[,] b)
        {
            var rows = a.GetRows();
            var cols = b.GetColumns();
            var length = a.GetColumns();

            if (length == b.GetRows())
            {
                var result = new Double[rows, cols];

                for (var i = 0; i < rows; i++)
                {
                    for (var j = 0; j < cols; j++)
                    {
                        var value = 0.0;

                        for (var k = 0; k < length; k++)
                        {
                            value += a[i, k] * b[k, j];
                        }

                        result[i, j] = value;
                    }
                }

                return result;
            }

            return null;
        }

        public static Double[,] Multiply(this Double[,] a, Double b)
        {
            var rows = a.GetRows();
            var cols = a.GetColumns();
            var result = new Double[rows, cols];

            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < cols; j++)
                {
                    result[i, j] = a[i, j] * b;
                }
            }

            return result;
        }

        public static Double[,] Divide(this Double[,] a, Double b)
        {
            return a.Multiply(1.0 / b);
        }

        public static Double[,] Transpose(this Double[,] matrix)
        {
            var rows = matrix.GetRows();
            var cols = matrix.GetColumns();
            var result = new Double[cols, rows];

            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < cols; j++)
                {
                    result[j, i] = matrix[i, j];
                }
            }

            return result;
        }

        public static Boolean IsSquare(this Double[,] matrix)
        {
            return matrix.GetColumns() == matrix.GetRows();
        }

        public static Double[,] Fill(this Double[,] matrix, Double value)
        {
            var rows = matrix.GetRows();
            var cols = matrix.GetColumns();
            var result = new Double[rows, cols];
            var length = Math.Min(rows, cols);

            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < cols; j++)
                {
                    result[i, j] = value;
                }
            }

            return result;
        }

        public static Double[,] Identity(this Double[,] matrix)
        {
            var rows = matrix.GetRows();
            var cols = matrix.GetColumns();
            var result = new Double[rows, cols];
            var length = Math.Min(rows, cols);

            for (var i = 0; i < length; i++)
            {
                result[i, i] = 1.0;
            }

            return result;
        }

        public static Double[,] Pow(this Double[,] a, Double[,] b)
        {
            if (a.Fits(b))
            {
                var rows = a.GetRows();
                var cols = a.GetColumns();
                var result = new Double[rows, cols];

                for (var i = 0; i < rows; i++)
                {
                    for (var j = 0; j < cols; j++)
                    {
                        result[i, j] = Math.Pow(a[i, j], b[i, j]);
                    }
                }

                return result;
            }

            return null;
        }

        public static Double[,] Pow(this Double[,] matrix, Double value)
        {
            if (value.IsInteger() && matrix.IsSquare())
            {
                var n = (Int32)value;
                var result = matrix.Identity();

                while (n-- > 0)
                {
                    result = result.Multiply(matrix);
                }

                return result;
            }

            return null;
        }

        public static Double[,] Pow(this Double value, Double[,] matrix)
        {
            var result = matrix.Fill(value);
            var rows = matrix.GetRows();
            var cols = matrix.GetColumns();

            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < cols; j++)
                {
                    result[i, j] = Math.Pow(result[i, j], matrix[i, j]);
                }
            }

            return result;
        }

        public static Double GetValue(this Double[,] matrix, Int32 row, Int32 col)
        {
            var rows = matrix.GetRows();
            var cols = matrix.GetColumns();

            if (row >= 0 && row < rows && col >= 0 && col < cols)
            {
                return matrix[row, col];
            }

            return 0.0;
        }

        public static void SetValue(this Double[,] matrix, Int32 row, Int32 col, Double value)
        {
            var rows = matrix.GetRows();
            var cols = matrix.GetColumns();

            if (row >= 0 && row < rows && col >= 0 && col < cols)
            {
                matrix[row, col] = value;
            }
        }

        public static Double[,] ForEach(this Double[,] matrix, Func<Double, Double> apply)
        {
            var rows = matrix.GetRows();
            var cols = matrix.GetColumns();
            var result = new Double[rows, cols];

            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < cols; j++)
                {
                    result[i, j] = apply(matrix[i, j]);
                }
            }

            return result;
        }

        public static Object Reduce(this Double[,] matrix, Func<Double, Double, Double> reducer)
        {
            var rows = matrix.GetRows();
            var cols = matrix.GetColumns();

            if (rows == 1 && cols == 1)
            {
                return matrix[0, 0];
            }
            else if (rows == 1)
            {
                var element = matrix[0, 0];

                for (var i = 1; i < cols; i++)
                {
                    element = reducer.Invoke(element, matrix[0, i]);
                }

                return element;
            }
            else if (cols == 1)
            {
                var element = matrix[0, 0];

                for (var i = 1; i < rows; i++)
                {
                    element = reducer.Invoke(element, matrix[i, 0]);
                }

                return element;
            }
            else
            {
                var result = new Double[rows, 1];

                for (var i = 0; i < rows; i++)
                {
                    var element = matrix[i, 0];

                    for (var j = 1; j < cols; j++)
                    {
                        element = reducer.Invoke(element, matrix[i, j]);
                    }

                    result[i, 0] = element;
                }

                return result;
            }
        }

        public static Double[,] And(this Double[,] a, Double b)
        {
            return a.And(a.Fill(b));
        }

        public static Double[,] And(this Double[,] a, Double[,] b)
        {
            if (a.Fits(b))
            {
                var rows = a.GetRows();
                var cols = a.GetColumns();
                var result = new Double[rows, cols];

                for (var i = 0; i < rows; i++)
                {
                    for (var j = 0; j < cols; j++)
                    {
                        result[i, j] = (a[i, j].ToBoolean() && b[i, j].ToBoolean()).ToNumber();
                    }
                }

                return result;
            }

            return null;
        }

        public static Double[,] IsGreaterThan(this Double[,] a, Double b)
        {
            return a.IsGreaterThan(a.Fill(b));
        }

        public static Double[,] IsGreaterThan(this Double[,] a, Double[,] b)
        {
            if (a.Fits(b))
            {
                var rows = a.GetRows();
                var cols = a.GetColumns();
                var result = new Double[rows, cols];

                for (var i = 0; i < rows; i++)
                {
                    for (var j = 0; j < cols; j++)
                    {
                        result[i, j] = (a[i, j] > b[i, j]).ToNumber();
                    }
                }

                return result;
            }

            return null;
        }

        public static Double[,] IsGreaterOrEqual(this Double[,] a, Double b)
        {
            return a.IsGreaterOrEqual(a.Fill(b));
        }

        public static Double[,] IsGreaterOrEqual(this Double[,] a, Double[,] b)
        {
            if (a.Fits(b))
            {
                var rows = a.GetRows();
                var cols = a.GetColumns();
                var result = new Double[rows, cols];

                for (var i = 0; i < rows; i++)
                {
                    for (var j = 0; j < cols; j++)
                    {
                        result[i, j] = (a[i, j] >= b[i, j]).ToNumber();
                    }
                }

                return result;
            }

            return null;
        }

        public static Double[,] AreNotEqual(this Double[,] a, Double b)
        {
            return a.AreNotEqual(a.Fill(b));
        }

        public static Object Map(this Double[,] matrix, Function f)
        {
            var result = new Dictionary<String, Object>();
            var args = new Object[4];
            var rows = matrix.GetRows();
            var columns = matrix.GetColumns();

            for (int i = 0, k = 0; i < rows; i++)
            {
                for (var j = 0; j < columns; j++, k++)
                {
                    args[0] = matrix[i, j];
                    args[1] = (Double)k;
                    args[2] = (Double)i;
                    args[3] = (Double)j;
                    result[k.ToString()] = f(args);
                }
            }

            return result;
        }

        public static Object Where(this Double[,] matrix, Function f)
        {
            var result = new List<Double>();
            var args = new Object[4];
            var rows = matrix.GetRows();
            var columns = matrix.GetColumns();

            for (int i = 0, k = 0; i < rows; i++)
            {
                for (var j = 0; j < columns; j++, k++)
                {
                    var value = matrix[i, j];
                    args[0] = value;
                    args[1] = (Double)k;
                    args[2] = (Double)i;
                    args[3] = (Double)j;

                    if (f(args).ToBoolean())
                    {
                        result.Add(value);
                    }
                }
            }

            return result.ToMatrix();
        }

        public static Object Reduce(this Double[,] matrix, Function f, Object start)
        {
            var args = new Object[5];
            var result = start;
            var rows = matrix.GetRows();
            var columns = matrix.GetColumns();

            for (int i = 0, k = 0; i < rows; i++)
            {
                for (var j = 0; j < columns; j++, k++)
                {
                    args[0] = result;
                    args[1] = matrix[i, j];
                    args[2] = (Double)k;
                    args[3] = (Double)i;
                    args[4] = (Double)j;
                    result = f(args);
                }
            }

            return result;
        }

        public static Double[,] AreNotEqual(this Double[,] a, Double[,] b)
        {
            if (a.Fits(b))
            {
                var rows = a.GetRows();
                var cols = a.GetColumns();
                var result = new Double[rows, cols];

                for (var i = 0; i < rows; i++)
                {
                    for (var j = 0; j < cols; j++)
                    {
                        result[i, j] = (a[i, j] != b[i, j]).ToNumber();
                    }
                }

                return result;
            }

            return null;
        }

        public static Double[,] IsLessThan(this Double[,] a, Double b)
        {
            return a.IsLessThan(a.Fill(b));
        }

        public static Double[,] IsLessThan(this Double[,] a, Double[,] b)
        {
            if (a.Fits(b))
            {
                var rows = a.GetRows();
                var cols = a.GetColumns();
                var result = new Double[rows, cols];

                for (var i = 0; i < rows; i++)
                {
                    for (var j = 0; j < cols; j++)
                    {
                        result[i, j] = (a[i, j] < b[i, j]).ToNumber();
                    }
                }

                return result;
            }

            return null;
        }

        public static Double[,] IsLessOrEqual(this Double[,] a, Double b)
        {
            return a.IsLessOrEqual(a.Fill(b));
        }

        public static Double[,] IsLessOrEqual(this Double[,] a, Double[,] b)
        {
            if (a.Fits(b))
            {
                var rows = a.GetRows();
                var cols = a.GetColumns();
                var result = new Double[rows, cols];

                for (var i = 0; i < rows; i++)
                {
                    for (var j = 0; j < cols; j++)
                    {
                        result[i, j] = (a[i, j] <= b[i, j]).ToNumber();
                    }
                }

                return result;
            }

            return null;
        }

        public static Double[,] AreEqual(this Double[,] a, Double b)
        {
            return a.AreEqual(a.Fill(b));
        }

        public static Double[,] AreEqual(this Double[,] a, Double[,] b)
        {
            if (a.Fits(b))
            {
                var rows = a.GetRows();
                var cols = a.GetColumns();
                var result = new Double[rows, cols];

                for (var i = 0; i < rows; i++)
                {
                    for (var j = 0; j < cols; j++)
                    {
                        result[i, j] = (a[i, j] == b[i, j]).ToNumber();
                    }
                }

                return result;
            }

            return null;
        }

        public static Double[,] Or(this Double[,] a, Double b)
        {
            return a.Or(a.Fill(b));
        }

        public static Double[,] Or(this Double[,] a, Double[,] b)
        {
            if (a.Fits(b))
            {
                var rows = a.GetRows();
                var cols = a.GetColumns();
                var result = new Double[rows, cols];

                for (var i = 0; i < rows; i++)
                {
                    for (var j = 0; j < cols; j++)
                    {
                        result[i, j] = (a[i, j].ToBoolean() || b[i, j].ToBoolean()).ToNumber();
                    }
                }

                return result;
            }

            return null;
        }
    }
}
