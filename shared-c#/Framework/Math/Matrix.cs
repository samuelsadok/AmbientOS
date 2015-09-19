using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppInstall.Framework
{

    public class Matrix<T> : IEnumerable<Vector<T>>
    {
        /// <summary>
        /// list of column vectors
        /// todo: change to a list of row vectors (multiplication becomes faster)
        /// </summary>
        private Vector<Vector<T>> content;

        public T this[int i, int j]
        {
            get { return content[j][i]; }
            set { content[j][i] = value; }
        }
        public Vector<T> this[int j]
        {
            get { return content[j]; }
            set { content[j] = value; }
        }

        public int Rows { get { return content[0].Dimension; } }
        public int Columns { get { return content.Dimension; } }


        public Vector<T> GetRow(int i)
        {
            return new Vector<T>((from c in content select c[i]).ToArray());
        }
        public Vector<T> GetColumn(int j)
        {
            return content[j];
        }

        public IEnumerator<Vector<T>> GetEnumerator()
        {
            return ((IEnumerable<Vector<T>>)content).GetEnumerator();
        }

        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()
        {
            return content.GetEnumerator();
        }

        public void SwapRows(int a, int b)
        {
            for (int i = 0; i < Columns; i++)
                content[i].Swap(a, b);
        }

        public void SwapColumns(int a, int b)
        {
            Vector<T> x = content[a];
            content[a] = content[b];
            content[b] = x;
        }

        public static Matrix<T> IdentityMatrix(int dimension)
        {
            Matrix<T> result = new Matrix<T>(dimension, dimension);
            for (int i = 0; i < dimension; i++)
                result[i, i] = Scalar.MultiplicativeNeutralElement<T>();
            return result;
        }

        /// <summary>
        /// Throws an exception if this is not a square matrix
        /// </summary>
        public void AssertSquare()
        {
            if (Columns != Rows)
                throw new Exception("The matrix is not square (has dimensions " + Rows + "x" + Columns + ")");
        }

        /// <summary>
        /// Constructs a matrix with the specified dimensions
        /// </summary>
        public Matrix(int rows, int columns)
        {
            if (columns < 1) throw new ArgumentException("a matrix must have one or more columns", "columns");
            if (rows < 1) throw new ArgumentException("a matrix must have one or more rows", "rows");
            content = new Vector<Vector<T>>(columns);
            while (columns-- > 0)
                content[columns] = new Vector<T>(rows);
        }

        /// <summary>
        /// Constructs a matrix from a two dimensional array.
        /// The array represents a set of column vectors.
        /// </summary>
        public Matrix(params T[][] elements)
        {
            content = new Vector<Vector<T>>(elements.Count());
            for (int i = 0; i < elements.Count(); i++)
                content[i] = new Vector<T>(elements[i]);
        }

        public override string ToString()
        {
            string[] r = new string[Rows];
            for (int i = 0; i < Rows; i++) r[i] = this.GetRow(i).ToString();
            return "{ " + string.Join(", ", r) + " }";
        }

        /// <summary>
        /// Constructs a matrix from a list of column vectors
        /// </summary>
        public Matrix(Vector<Vector<T>> columns)
        {
            content = columns;
        }

        public Matrix<T> Copy()
        {
            Matrix<T> result = new Matrix<T>(Rows, Columns);
            for (int j = 0; j < Columns; j++)
                for (int i = 0; i < Rows; i++)
                    result[i, j] = this[i, j];
            return result;
        }

        /// <summary>
        /// Returns the hermitian transpose of the matrix (swaps the row-column indices while taking the complex conjugate of each element)
        /// </summary>
        public Matrix<T> Transpose()
        {
            Matrix<T> result = new Matrix<T>(Columns, Rows);
            for (int j = 0; j < Columns; j++)
                for (int i = 0; i < Rows; i++)
                    result[j, i] = Scalar.Conjugate(this[i, j]);
            return result;
        }


        public static Vector<T> operator *(Matrix<T> matrix, Vector<T> vector)
        {
            Vector<T> result;
            if (matrix.Rows == matrix.Columns)
                result = Vector<T>.MakeVector(vector);
            else
                result = new Vector<T>(matrix.Rows);
            for (int i = 0; i < matrix.Rows; i++)
                result[i] = matrix.GetRow(i) * vector;
            return result;
        }

        public static Matrix<T> operator *(Matrix<T> a, Matrix<T> b)
        {
            return new Matrix<T>(new Vector<Vector<T>>((from c in b.content select a * c).ToArray()));
        }


        /// <summary>
        /// Orthonormalizes all columns of the matrix
        /// </summary>
        public Matrix<T> Orthonormalize()
        {
            Vector<Vector<T>> result = new Vector<Vector<T>>(Columns);
            for (int i = 0; i < Columns; i++)
                result[i] = this[i].Orthagonalize(result.Take(i).ToArray()).Normalize();
            return new Matrix<T>(result);
        }





        /// <summary>
        /// Returns three matrices that satisfy the equation: A*P = L*R ("A" being the current instance)
        /// </summary>
        /// <param name="L">a matrix in the upper triangular form</param>
        /// <param name="R">a matrix in the lower triangular form</param>
        /// <param name="P">a permutation matrix</param>
        public void LUDecomposition(out Matrix<T> L, out Matrix<T> R, out Matrix<T> P)
        {
            AssertSquare();
            L = Matrix<T>.IdentityMatrix(Columns);
            R = this.Copy();
            P = Matrix<T>.IdentityMatrix(Columns);

            for (int k = 0; k < Rows - 1; k++) {
                int pivotIndex;
                T pivot = R.GetColumn(k).GetLargest(out pivotIndex, k);
                R.SwapRows(k, pivotIndex); P.SwapRows(k, pivotIndex);

                for (int l = k + 1; l < Rows; l++) {
                    T multiplier = (L[l, k] = Scalar.Divide(R[l, k], pivot));
                    for (int m = k; m < Columns; m++)
                        R[l, m] = Scalar.Subtract(R[l, m], Scalar.Multiply(R[k, m], multiplier));
                }
            }
        }


        /// <summary>
        /// Returns two matrices that satisfy the equation: A = Q*R
        /// </summary>
        /// <param name="Q">A matrix with orthagonal columns</param>
        public void QRDecomposition(out Matrix<T> Q, out Matrix<T> R)
        {
            Q = Orthonormalize();
            R = new Matrix<T>(Q.Columns, Q.Columns);
            for (int i = 0; i < Q.Columns; i++)
                for (int j = i; j < Q.Columns; j++)
                    R[i, j] = Q.GetColumn(i) * this.GetColumn(j);
        }


        /// <summary>
        /// Solves an equation of the form A*x = y when A is in the upper or lower triangular form
        /// </summary>
        private Vector<T> SolveFromTrianguarForm(Vector<T> y, bool upperTriangular)
        {
            Vector<T> result = new Vector<T>(Rows);
            Action<int> calc = (i) => result[i] = Scalar.Divide(Scalar.Subtract(y[i], GetRow(i) * result), this[i, i]);
            if (upperTriangular)
                for (int i = Rows - 1; i >= 0; i--) calc(i);
            else
                for (int i = 0; i < Rows; i++) calc(i);
            return result;
        }

        public Matrix<T> Inverse()
        {
            return Solve(IdentityMatrix(Columns));
        }

        /// <summary>
        /// Solves an equation of the type A * X = B (i.e. X = A^-1 * B)
        /// </summary>
        /// <returns></returns>
        public Matrix<T> Solve(Matrix<T> b)
        {
            Matrix<T> L, R, P;
            LUDecomposition(out L, out R, out P);
            return new Matrix<T>(new Vector<Vector<T>>((from c in (P * b) select R.SolveFromTrianguarForm(L.SolveFromTrianguarForm(c, false), true)).ToArray()));
        }

        /// <summary>
        /// Solves an overdetermined linear system of the type A * x = b by means of the smallest sum of squares
        /// </summary>
        public Vector<T> LinearRegression(Vector<T> b)
        {
            // method 1: take the projection of b onto the space spanned by this matrix
            //return (Transpose() * this).Inverse() * this.Transpose() * b; // this introduces rounding errors for closely related columns

            // method 2: orthagonalize matrix first
            Matrix<T> Q, R;
            QRDecomposition(out Q, out R);
            return R.Inverse() * Q.Transpose() * b;

        }
    }
}