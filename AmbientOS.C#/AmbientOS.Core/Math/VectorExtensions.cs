using System;
using System.Collections.Generic;
using System.Linq;

namespace AmbientOS
{
    /// <summary>
    /// Contains extensions for the IVector interface.
    /// </summary>
    public static class VectorExtensions
    {
        /// <summary>
        /// Creates a matrix from a single column vector.
        /// A vector is a special case of a matrix, so every vector can be converted to a matrix.
        /// </summary>
        public static IMatrix<T> ToMatrix<T>(this IVector<T> vector)
        {
            if (vector.IsColumn)
                return new Matrix<T>(vector.Size, 1, (row, column) => vector.ElementAt(row), vector.Calculator);
            else
                return new Matrix<T>(1, vector.Size, (row, column) => vector.ElementAt(column), vector.Calculator);
        }

        /// <summary>
        /// Enumerates the elements of a matrix.
        /// </summary>
        public static IEnumerable<T> AsEnumerable<T>(this IVector<T> vector)
        {
            for (int i = 0; i < vector.Size; i++)
                yield return vector.ElementAt(i);
        }

        /// <summary>
        /// Creates an array and fills it with the elements of the vector.
        /// </summary>
        public static T[] ToArray<T>(this IVector<T> vector)
        {
            var array = new T[vector.Size];
            for (int i = 0; i < vector.Size; i++)
                array[i] = vector.ElementAt(i);
            return array;
        }

        /// <summary>
        /// This is analogous to the Linq method IEnumerable.Select.
        /// It represents an element-wise operation on each element of a vector.
        /// </summary>
        public static IVector<TResult> Select<TInput, TResult>(this IVector<TInput> vector, Func<TInput, TResult> selector)
        {
            return new Vector<TResult>(
                vector.Size,
                vector.IsColumn,
                index => selector(vector.ElementAt(index))
                );
        }

        /// <summary>
        /// This is analogous to the Linq method IEnumerable.Zip.
        /// It represents an element-wise operation between two matrices.
        /// The result is a column vector if either of the inputs is a column vector.
        /// </summary>
        public static IVector<TResult> Zip<TFirst, TSecond, TResult>(this IVector<TFirst> first, IVector<TSecond> second, Func<TFirst, TSecond, TResult> resultSelector)
        {
            if (first.Size != second.Size)
                throw new ArgumentException("Both vectors must have equal sizes");

            return new Vector<TResult>(
                first.Size,
                first.IsColumn || second.IsColumn,
                index => resultSelector(first.ElementAt(index), second.ElementAt(index))
                );
        }

        /// <summary>
        /// Returns the smallest element in the vector (by absolute value).
        /// </summary>
        /// <param name="index">Input: specifies the number of elements to skip. Output: returns the index of the smallest element.</param>
        public static T GetSmallest<T>(this IVector<T> vector, ref int index)
        {
            var calc = vector.Calculator;
            var smallest = calc.AbsoluteValue(vector.ElementAt(index));

            for (int i = index; i < vector.Size; i++) {
                var current = calc.AbsoluteValue(vector.ElementAt(i));
                if (calc.IsSmaller(current, smallest)) {
                    smallest = current;
                    index = i;
                }
            }

            return smallest;
        }

        /// <summary>
        /// Returns the largest element in the vector (by absolute value).
        /// </summary>
        /// <param name="index">Input: specifies the number of elements to skip. Output: returns the index of the largest element.</param>
        public static T GetLargest<T>(this IVector<T> vector, ref int index)
        {
            var calc = vector.Calculator;
            var largest = calc.AbsoluteValue(vector.ElementAt(index));

            for (int i = index; i < vector.Size; i++) {
                var current = calc.AbsoluteValue(vector.ElementAt(i));
                if (calc.IsLarger(current, largest)) {
                    largest = current;
                    index = i;
                }
            }

            return largest;
        }

        /// <summary>
        /// Returns the smallest element in the vector (by absolute value).
        /// </summary>
        public static T GetSmallest<T>(this IVector<T> vector)
        {
            int index = 0;
            return vector.GetSmallest(ref index);
        }

        /// <summary>
        /// Returns the largest element in the vector (by absolute value).
        /// </summary>
        public static T GetLargest<T>(this IVector<T> vector)
        {
            int index = 0;
            return vector.GetLargest(ref index);
        }

        /// <summary>
        /// Returns true iif both vectors have the same size and all elements are equal.
        /// </summary>
        public static bool ContentEquals<T>(this IVector<T> a, IVector<T> b)
        {
            if (a.Size != b.Size || a.IsColumn != b.IsColumn)
                return false;

            for (int i = 0; i < a.Size; i++)
                if (!Equals(a.ElementAt(i), b.ElementAt(i)))
                    return false;

            return true;
        }

        /// <summary>
        /// Calculates the sum of all hash codes of the vector elements.
        /// This can be used to override the GetHashCode function of immutable vector types.
        /// </summary>
        public static int GetContentHashCode<T>(this IVector<T> vector)
        {
            return vector
                .AsEnumerable()
                .Select(element => element.GetHashCode())
                .Aggregate(unchecked(vector.Size + vector.IsColumn.GetHashCode()), (a, b) => unchecked(a + b));
        }

        /// <summary>
        /// Calculates the dot-product of two column vectors.
        /// </summary>
        public static T Dot<T>(this IVector<T> a, IVector<T> b)
        {
            return a.ToMatrix().Dot(b.ToMatrix());
        }

        /// <summary>
        /// Returns the hermitian transpose of a vector. This makes a row vector from a column vector and vice versa and takes the complex conjugate of each element.
        /// </summary>
        public static IVector<T> Transpose<T>(this IVector<T> vector)
        {
            return new Vector<T>(vector.Size, !vector.IsColumn, index => vector.Calculator.Conjugate(vector.ElementAt(index)));
        }

        /// <summary>
        /// Calculates the element-wise sum of two vectors. The result is evaluated lazily.
        /// </summary>
        public static IVector<T> Add<T>(this IVector<T> a, IVector<T> b)
        {
            if (a.Size != b.Size || a.IsColumn != b.IsColumn)
                throw new Exception("Vector dimensions must agree for addition");

            return new Vector<T>(
                a.Size,
                a.IsColumn,
                index => a.Calculator.Add(a.ElementAt(index), b.ElementAt(index)),
                a.Calculator
                );
        }

        /// <summary>
        /// Calculates the element-wise difference of two vectors. The result is evaluated lazily.
        /// </summary>
        public static IVector<T> Subtract<T>(this IVector<T> a, IVector<T> b)
        {
            if (a.Size != b.Size || a.IsColumn != b.IsColumn)
                throw new Exception("Vector dimensions must agree for subtraction");

            return new Vector<T>(
                a.Size,
                a.IsColumn,
                index => a.Calculator.Subtract(a.ElementAt(index), b.ElementAt(index)),
                a.Calculator
                );
        }

        /// <summary>
        /// Multiplies every element in this vector by a scalar and returns the result.
        /// </summary>
        public static IVector<T> Multiply<T>(this IVector<T> vector, T scalar)
        {
            return new Vector<T>(vector.Size, vector.IsColumn, index => vector.Calculator.Multiply(vector.ElementAt(index), scalar));
        }

        /// <summary>
        /// Multiplies every element in the first vector with the corresponding element in the second vector.
        /// Both vectors must have equal dimensions.
        /// </summary>
        public static IVector<T> ElementWiseMultiply<T>(this IVector<T> a, IVector<T> b)
        {
            if (a.Size != b.Size || a.IsColumn != b.IsColumn)
                throw new ArgumentException("Vector dimensions must agree");

            return new Vector<T>(
                a.Size,
                a.IsColumn,
                index => a.Calculator.Multiply(a.ElementAt(index), b.ElementAt(index))
                );
        }

        /// <summary>
        /// Divides every element in this vector by a scalar and returns the result.
        /// </summary>
        public static IVector<T> Divide<T>(this IVector<T> vector, T scalar)
        {
            return new Vector<T>(vector.Size, vector.IsColumn, index => vector.Calculator.Divide(vector.ElementAt(index), scalar));
        }

        /// <summary>
        /// Calculates the Euclidean norm of this vector.
        /// For column vectors, this is equal to the Frobenius norm of the corresponding matrix.
        /// </summary>
        public static T Norm<T>(this IVector<T> vector)
        {
            return vector.Calculator.SquareRoot(vector.Dot(vector));
        }

        /// <summary>
        /// Divides every element in this vector by the Euclidean norm of the vector.
        /// This normalizes the vector to a length of 1.
        /// </summary>
        public static IVector<T> Normalize<T>(this IVector<T> vector)
        {
            return vector.Multiply(vector.Calculator.MultiplicativeInverse(vector.Norm()));
        }

        /// <summary>
        /// Returns the perpendicular component of this vector to the vector space spanned by the specified vectors.
        /// </summary>
        public static IVector<T> Orthogonalize<T>(this IVector<T> vector, params IVector<T>[] span)
        {
            if (span.Count() == 0)
                return vector;
            return vector.Subtract(span.Select(v => v.Multiply(vector.Dot(v))).Aggregate((a, b) => a.Add(b)));
        }
    }
}
