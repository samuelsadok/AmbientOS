using System;
using System.Collections.Generic;
using System.Linq;

namespace AmbientOS
{
    /// <summary>
    /// Represents a matrix.
    /// </summary>
    public interface IMatrix<T>
    {
        /// <summary>
        /// Indicates the number of rows in the matrix.
        /// </summary>
        int Rows { get; }

        /// <summary>
        /// Indicates the number of columns in the matrix.
        /// </summary>
        int Columns { get; }

        /// <summary>
        /// Returns the element at the specified (row, column) index.
        /// </summary>
        T ElementAt(int row, int column);

        /// <summary>
        /// Specifies the calculator to be used for arithmetic operation among the elements of the matrix.
        /// </summary>
        Calculator<T> Calculator { get; }
    }


    public class Matrix<T> : IMatrix<T>
    {
        public static Matrix<T> IdentityMatrix(int size)
        {
            return new Matrix<T>(size, size, (row, column) => row == column ? Calculator<T>.DefaultCalculator.MultiplicativeNeutralElement : Calculator<T>.DefaultCalculator.AdditiveNeutralElement);
        }

        public static Matrix<T> ZeroMatrix(int rows, int columns)
        {
            return new Matrix<T>(rows, columns, (row, column) => Calculator<T>.DefaultCalculator.AdditiveNeutralElement);
        }

        public Calculator<T> Calculator { get; }

        private readonly Func<int, int, T> elementProvider;

        public int Rows { get; }
        public int Columns { get; }

        public T this[int row, int column]
        {
            get { return ElementAt(row, column); }
        }

        public T ElementAt(int row, int column)
        {
            return elementProvider(row, column);
        }

        /// <summary>
        /// Creates a matrix with the specified element evaluation function.
        /// The elements are lazily evaluated as they are needed.
        /// </summary>
        /// <param name="rows">The number of rows in the matrix</param>
        /// <param name="columns">The number of columns in the matrix</param>
        /// <param name="elementProvider">A function that provides an element for each valid (row, column) index</param>
        /// <param name="calculator">The calculator that should be used for operations among the elements</param>
        public Matrix(int rows, int columns, Func<int, int, T> elementProvider, Calculator<T> calculator)
        {
            Rows = rows;
            Columns = columns;
            this.elementProvider = elementProvider;
            Calculator = calculator;
        }

        /// <summary>
        /// Creates a matrix with the specified element evaluation function.
        /// The elements are lazily evaluated as they are needed.
        /// </summary>
        /// <param name="rows">The number of rows in the matrix</param>
        /// <param name="columns">The number of columns in the matrix</param>
        /// <param name="elementProvider">A function that provides an element for each valid (row, column) index</param>
        public Matrix(int rows, int columns, Func<int, int, T> elementProvider)
            : this(rows, columns, elementProvider, Calculator<T>.DefaultCalculator)
        {
        }

        /// <summary>
        /// Creates a matrix from the specified two-dimensional array.
        /// The array is not copied, so if an element in the array is substituted, it's substituted in the matrix as well.
        /// </summary>
        protected Matrix(T[,] content)
            : this(content.GetLength(0), content.GetLength(1), (row, column) => content[row, column])
        {
        }

        /// <summary>
        /// Creates a copy of the specified matrix.
        /// This evaluates the elements in the provided matrix.
        /// </summary>
        public Matrix(IMatrix<T> template)
            : this(template.ToArray())
        {
        }

        /// <summary>
        /// Creates a matrix from a set of column vectors.
        /// The elements are lazily evaluated as they are needed.
        /// Each column must have the same length.
        /// </summary>
        public Matrix(IVector<T>[] columns)
            : this(columns.Select(column => column.Size).FirstOrDefault(), columns.Count(), (row, column) => columns[column].ElementAt(row))
        {
            if (!columns.Any())
                throw new ArgumentException("At least one column must be provided.", $"{columns}");
            if (columns.Any(column => column.Size != Rows))
                throw new ArgumentException("All columns must have the same length.", $"{columns}");
        }

        public override string ToString()
        {
            string[] r = new string[Rows];
            for (int i = 0; i < Rows; i++)
                r[i] = this.GetRow(i).ToString();
            return "{ " + string.Join(", ", r) + " }";
        }
    }


    public class MutableMatrix<T> : Matrix<T>
    {
        private readonly T[,] content;

        public new T this[int row, int column]
        {
            get { return content[row, column]; }
            set { content[row, column] = value; }
        }

        private MutableMatrix(T[,] content)
            : base(content)
        {
            this.content = content;
        }

        public MutableMatrix(IMatrix<T> template)
            : this(template.ToArray())
        {
        }

        public void SwapRows(int row1, int row2)
        {
            for (int j = 0; j < Columns; j++) {
                var temp = content[row1, j];
                content[row1, j] = content[row2, j];
                content[row2, j] = temp;
            }
        }

        public void SwapColumns(int column1, int column2)
        {
            for (int i = 0; i < Rows; i++) {
                var temp = content[i, column1];
                content[i, column1] = content[i, column2];
                content[i, column2] = temp;
            }
        }
    }
}