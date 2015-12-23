using System;
using System.Collections.Generic;
using System.Linq;

namespace AmbientOS
{
    /// <summary>
    /// Contains extensions for the IMatrix interface.
    /// </summary>
    public static class MatrixExtensions
    {
        /// <summary>
        /// Creates a row or column vector from this matrix.
        /// For a 1x1 matrix, this returns a column vector.
        /// </summary>
        /// <exception cref="ArgumentException">The matrix represents neither a single row nor a single column.</exception>
        public static IVector<T> ToVector<T>(this IMatrix<T> matrix)
        {
            if (matrix.Columns == 1)
                return new Vector<T>(matrix.Rows, true, index => matrix.ElementAt(index, 0), matrix.Calculator);
            else if (matrix.Rows == 1)
                return new Vector<T>(matrix.Columns, false, index => matrix.ElementAt(0, index), matrix.Calculator);
            else
                throw new ArgumentException("The matrix represents neither a single row nor a single column.", $"{matrix}");
        }

        /// <summary>
        /// Enumerates the elements of a matrix.
        /// </summary>
        public static IEnumerable<T> AsEnumerable<T>(this IMatrix<T> matrix)
        {
            for (int i = 0; i < matrix.Rows; i++)
                for (int j = 0; j < matrix.Columns; j++)
                    yield return matrix.ElementAt(i, j);
        }

        /// <summary>
        /// Creates a two-dimensional array and fills it with the elements of the matrix.
        /// The first index is the row index, the second index is the column index.
        /// </summary>
        public static T[,] ToArray<T>(this IMatrix<T> matrix)
        {
            var array = new T[matrix.Rows, matrix.Columns];
            for (int i = 0; i < matrix.Rows; i++)
                for (int j = 0; j < matrix.Columns; j++)
                    array[i, j] = matrix.ElementAt(i, j);
            return array;
        }

        /// <summary>
        /// This is analogous to the Linq method IEnumerable.Select.
        /// It represents an element-wise operation on each element of a matrix.
        /// </summary>
        public static IMatrix<TResult> Select<TInput, TResult>(this IMatrix<TInput> matrix, Func<TInput, TResult> selector)
        {
            return new Matrix<TResult>(
                matrix.Rows,
                matrix.Columns,
                (row, column) => selector(matrix.ElementAt(row, column))
                );
        }

        /// <summary>
        /// This is analogous to the Linq method IEnumerable.Zip.
        /// It represents an element-wise operation between two matrices.
        /// </summary>
        public static IMatrix<TResult> Zip<TFirst, TSecond, TResult>(this IMatrix<TFirst> first, IMatrix<TSecond> second, Func<TFirst, TSecond, TResult> resultSelector)
        {
            if (first.Rows != second.Rows || first.Columns != second.Columns)
                throw new ArgumentException("Both matrices must have equal sizes");

            return new Matrix<TResult>(
                first.Rows,
                first.Columns,
                (row, column) => resultSelector(first.ElementAt(row, column), second.ElementAt(row, column))
                );
        }

        /// <summary>
        /// Returns the smallest element in the matrix (by absolute value).
        /// </summary>
        /// <param name="row">Input: specifies the number of rows to skip. Output: returns the row index of the smallest element.</param>
        /// <param name="column">Input: specifies the number of columns to skip. Output: returns the column index of the smallest element.</param>
        public static T GetSmallest<T>(this IMatrix<T> matrix, ref int row, ref int column)
        {
            var calc = matrix.Calculator;
            int startRow = row, startColumn = column;
            var smallest = calc.AbsoluteValue(matrix.ElementAt(startRow, startColumn));

            for (int i = startRow; i < matrix.Rows; i++) {
                for (int j = startColumn; j < matrix.Columns; j++) {
                    var current = calc.AbsoluteValue(matrix.ElementAt(i, j));
                    if (calc.IsSmaller(current, smallest)) {
                        smallest = current;
                        row = i;
                        column = j;
                    }
                }
            }

            return smallest;
        }

        /// <summary>
        /// Returns the largest element in the matrix (by absolute value).
        /// </summary>
        /// <param name="row">Input: specifies the number of rows to skip. Output: returns the row index of the largest element.</param>
        /// <param name="column">Input: specifies the number of columns to skip. Output: returns the column index of the largest element.</param>
        public static T GetLargest<T>(this IMatrix<T> matrix, ref int row, ref int column)
        {
            var calc = matrix.Calculator;
            int startRow = row, startColumn = column;
            var largest = calc.AbsoluteValue(matrix.ElementAt(startRow, startColumn));

            for (int i = startRow; i < matrix.Rows; i++) {
                for (int j = startColumn; j < matrix.Columns; j++) {
                    var current = calc.AbsoluteValue(matrix.ElementAt(i, j));
                    if (calc.IsLarger(current, largest)) {
                        largest = current;
                        row = i;
                        column = j;
                    }
                }
            }

            return largest;
        }

        /// <summary>
        /// Returns the smallest element in the matrix (by absolute value).
        /// </summary>
        public static T GetSmallest<T>(this IMatrix<T> matrix)
        {
            int row = 0, column = 0;
            return matrix.GetSmallest(ref row, ref column);
        }

        /// <summary>
        /// Returns the largest element in the matrix (by absolute value).
        /// </summary>
        public static T GetLargest<T>(this IMatrix<T> matrix)
        {
            int row = 0, column = 0;
            return matrix.GetLargest(ref row, ref column);
        }

        public static IVector<T> GetRow<T>(this IMatrix<T> matrix, int row)
        {
            return new Vector<T>(matrix.Columns, false, index => matrix.ElementAt(row, index));
        }

        public static IVector<T> GetColumn<T>(this IMatrix<T> matrix, int column)
        {
            return new Vector<T>(matrix.Rows, true, index => matrix.ElementAt(index, column));
        }

        public static IEnumerable<IVector<T>> GetColumns<T>(this IMatrix<T> matrix)
        {
            for (int column = 0; column < matrix.Columns; column++)
                yield return matrix.GetColumn(column);
        }

        /// <summary>
        /// Returns true iif both matrices have the same dimensions and all elements are equal.
        /// </summary>
        public static bool ContentEquals<T>(this IMatrix<T> a, IMatrix<T> b)
        {
            if (a.Rows != b.Rows || a.Columns != b.Columns)
                return false;

            for (int i = 0; i < a.Rows; i++)
                for (int j = 0; j < a.Columns; j++)
                    if (!Equals(a.ElementAt(i, j), b.ElementAt(i, j)))
                        return false;

            return true;
        }

        /// <summary>
        /// Calculates the sum of all hash codes of the matrix elements.
        /// This can be used to override the GetHashCode function of immutable matrix types.
        /// </summary>
        public static int GetContentHashCode<T>(this IMatrix<T> matrix)
        {
            return matrix
                .AsEnumerable()
                .Select(element => element.GetHashCode())
                .Aggregate(unchecked(matrix.Rows + matrix.Columns), (a, b) => unchecked(a + b));
        }

        /// <summary>
        /// Calculates the dot product of two vectors.
        /// For matrices, this returns trace(A* x B), where A* is the hermitian transpose of A.
        /// </summary>
        public static T Dot<T>(this IMatrix<T> a, IMatrix<T> b)
        {
            return a.Transpose().Multiply(b).Trace();
        }

        /// <summary>
        /// Returns the trace of the matrix. This is the sum of all diagonal elements (from top left to bottom right).
        /// </summary>
        public static T Trace<T>(this IMatrix<T> a)
        {
            var trace = a.Calculator.AdditiveNeutralElement;
            for (int i = 0; i < Math.Min(a.Rows, a.Columns); i++)
                trace = a.Calculator.Add(trace, a.ElementAt(i, i));
            return trace;
        }

        /// <summary>
        /// Returns the hermitian transpose of a matrix. This swaps the row and column indices and takes the complex conjugate of each element.
        /// </summary>
        public static IMatrix<T> Transpose<T>(this IMatrix<T> matrix)
        {
            return new Matrix<T>(matrix.Columns, matrix.Rows, (row, column) => matrix.Calculator.Conjugate(matrix.ElementAt(column, row)));
        }

        /// <summary>
        /// Calculates the element-wise sum of two matrices. The result is evaluated lazily.
        /// </summary>
        public static IMatrix<T> Add<T>(this IMatrix<T> a, IMatrix<T> b)
        {
            if (a.Rows != b.Rows || a.Columns != b.Columns)
                throw new Exception("Matrix dimensions must agree for addition");

            return new Matrix<T>(
                a.Rows,
                a.Columns,
                (row, column) => a.Calculator.Add(a.ElementAt(row, column), b.ElementAt(row, column))
                );
        }

        /// <summary>
        /// Calculates the element-wise difference of two matrices. The result is evaluated lazily.
        /// </summary>
        public static IMatrix<T> Subtract<T>(this IMatrix<T> a, IMatrix<T> b)
        {
            if (a.Rows != b.Rows || a.Columns != b.Columns)
                throw new Exception("Matrix dimensions must agree for subtraction");

            return new Matrix<T>(
                a.Rows,
                a.Columns,
                (row, column) => a.Calculator.Subtract(a.ElementAt(row, column), b.ElementAt(row, column))
                );
        }

        /// <summary>
        /// Multiplies every element in this matrix by a scalar and returns the result.
        /// </summary>
        public static IMatrix<T> Multiply<T>(this IMatrix<T> matrix, T scalar)
        {
            return new Matrix<T>(matrix.Rows, matrix.Columns, (row, column) => matrix.Calculator.Multiply(matrix.ElementAt(row, column), scalar));
        }

        /// <summary>
        /// Calculates the product of two matrices. The result is evaluated lazily.
        /// </summary>
        public static IMatrix<T> Multiply<T>(this IMatrix<T> a, IMatrix<T> b)
        {
            if (a.Columns != b.Rows)
                throw new Exception("Matrix dimensions not suitable for multiplication");

            return new Matrix<T>(
                a.Rows,
                b.Columns,
                (row, column) => a.Calculator.Sum(a.GetRow(row).Zip(b.GetColumn(column), (scalarA, scalarB) => a.Calculator.Multiply(scalarA, scalarB)).AsEnumerable())
                );
        }

        /// <summary>
        /// Multiplies every element in the first matrix with the corresponding element in the second matrix.
        /// Both matrices must have equal dimensions.
        /// </summary>
        public static IMatrix<T> ElementWiseMultiply<T>(this IMatrix<T> a, IMatrix<T> b)
        {
            if (a.Rows != b.Rows || a.Columns != b.Columns)
                throw new ArgumentException("Matrix dimensions must agree");

            return new Matrix<T>(
                a.Rows,
                a.Columns,
                (row, column) => a.Calculator.Multiply(a.ElementAt(row, column), b.ElementAt(row, column))
                );
        }

        /// <summary>
        /// Divides every element in this matrix by a scalar and returns the result.
        /// </summary>
        public static IMatrix<T> Divide<T>(this IMatrix<T> matrix, T scalar)
        {
            return new Matrix<T>(matrix.Rows, matrix.Columns, (row, column) => matrix.Calculator.Divide(matrix.ElementAt(row, column), scalar));
        }

        /// <summary>
        /// Calculates the Frobenius norm of a matrix.
        /// For column vectors, this is equal to the Euclidean norm, aka length of the vector.
        /// </summary>
        public static T Norm<T>(this IMatrix<T> matrix)
        {
            return matrix.Calculator.SquareRoot(matrix.Dot(matrix));
        }

        /// <summary>
        /// Orthonormalizes all columns of the matrix.
        /// </summary>
        public static IMatrix<T> Orthonormalize<T>(this IMatrix<T> matrix)
        {
            var normalColumns = new IVector<T>[matrix.Columns];
            for (int j = 0; j < matrix.Columns; j++)
                normalColumns[j] = new Vector<T>(matrix.GetColumn(j).Orthogonalize(normalColumns.Take(j).ToArray()).Normalize());
            return new Matrix<T>(normalColumns);
        }

        /// <summary>
        /// Returns three matrices that satisfy the equation: A*P = L*R ("A" being the current instance)
        /// </summary>
        /// <param name="L">a matrix in the upper triangular form</param>
        /// <param name="R">a matrix in the lower triangular form</param>
        /// <param name="P">a permutation matrix</param>
        public static void LUDecomposition<T>(this IMatrix<T> matrix, out MutableMatrix<T> L, out MutableMatrix<T> R, out MutableMatrix<T> P)
        {
            if (matrix.Columns != matrix.Rows)
                throw new Exception("The matrix is not square (has dimensions " + matrix.Rows + "x" + matrix.Columns + ")");

            L = new MutableMatrix<T>(Matrix<T>.IdentityMatrix(matrix.Columns));
            R = new MutableMatrix<T>(matrix);
            P = new MutableMatrix<T>(Matrix<T>.IdentityMatrix(matrix.Columns));

            for (int k = 0; k < matrix.Rows - 1; k++) {
                int pivotIndex = k;

                var pivot = R.GetColumn(k).GetLargest(ref pivotIndex);
                R.SwapRows(k, pivotIndex);
                P.SwapRows(k, pivotIndex);

                for (int l = k + 1; l < matrix.Rows; l++) {
                    T multiplier = (L[l, k] = matrix.Calculator.Divide(R[l, k], pivot));
                    for (int m = k; m < matrix.Columns; m++)
                        R[l, m] = matrix.Calculator.Subtract(R[l, m], matrix.Calculator.Multiply(R[k, m], multiplier));
                }
            }
        }

        /// <summary>
        /// Returns two matrices that satisfy the equation: A = Q*R
        /// </summary>
        /// <param name="Q">A matrix with orthogonal columns</param>
        public static void QRDecomposition<T>(this IMatrix<T> matrix, out IMatrix<T> Q, out IMatrix<T> R)
        {
            var q = matrix.Orthonormalize();
            Q = q; // required because of language limitations
            R = new Matrix<T>(Q.Columns, Q.Columns, (row, column) => q.GetColumn(row).Dot(matrix.GetColumn(column)));
        }

        /// <summary>
        /// Solves an equation of the form A*x = y when A is in the upper or lower triangular form.
        /// The result is evaluated eagerly.
        /// </summary>
        private static IVector<T> SolveFromTrianguarForm<T>(this IMatrix<T> a, IVector<T> y, bool upperTriangular)
        {
            if (!y.IsColumn)
                throw new ArgumentException("Result vector must be a column vector", $"{y}");

            var result = new MutableVector<T>(Vector<T>.ZeroVector(a.Rows, true));

            Action<int> calc = (i) => result[i] = a.Calculator.Divide(a.Calculator.Subtract(y.ElementAt(i), a.GetRow(i).ToMatrix().Multiply(result.ToMatrix()).Trace()), a.ElementAt(i, i));

            if (upperTriangular)
                for (int i = a.Rows - 1; i >= 0; i--) calc(i);
            else
                for (int i = 0; i < a.Rows; i++) calc(i);

            return result;
        }

        /// <summary>
        /// Returns the inverse of the matrix.
        /// </summary>
        public static IMatrix<T> Inverse<T>(this IMatrix<T> matrix)
        {
            return matrix.Solve(Matrix<T>.IdentityMatrix(matrix.Columns));
        }

        /// <summary>
        /// Solves an equation of the type A * X = B (i.e. X = A^-1 * B)
        /// </summary>
        public static IMatrix<T> Solve<T>(this IMatrix<T> a, IMatrix<T> b)
        {
            MutableMatrix<T> L, R, P;
            a.LUDecomposition(out L, out R, out P);
            return new Matrix<T>(P.Multiply(b).GetColumns().Select(c => R.SolveFromTrianguarForm(L.SolveFromTrianguarForm(c, false), true)).ToArray());
        }

        /// <summary>
        /// Solves an overdetermined linear system of the type A * x = b by means of the smallest sum of squares
        /// </summary>
        public static IMatrix<T> LinearRegression<T>(this IMatrix<T> matrix, IMatrix<T> b)
        {
            // method 1: take the projection of b onto the space spanned by this matrix
            //return (Transpose() * this).Inverse() * this.Transpose() * b; // this introduces rounding errors for closely related columns

            // method 2: orthagonalize matrix first
            IMatrix<T> Q, R;
            matrix.QRDecomposition(out Q, out R);
            return R.Inverse().Multiply(Q.Transpose()).Multiply(b);
        }
    }
}
