using System;
using System.Collections.Generic;
using System.Linq;

namespace AmbientOS
{
    /// <summary>
    /// Represents a column vector.
    /// </summary>
    public interface IVector<T>
    {
        /// <summary>
        /// Indicates the number of rows in the column vector.
        /// </summary>
        int Size { get; }

        /// <summary>
        /// Returns true if the vector is a column vector and false if it is a row vector.
        /// For simple operations (such as addition and subtraction), this may not matter
        /// but it becomes relevant for other operations such as multiplication.
        /// </summary>
        bool IsColumn { get; }

        /// <summary>
        /// Returns the element at the specified row index.
        /// </summary>
        T ElementAt(int row);

        /// <summary>
        /// Specifies the calculator to be used for arithmetic operation among the elements of the matrix.
        /// </summary>
        Calculator<T> Calculator { get; }
    }


    public class Vector<T> : IVector<T>
    {
        public static Vector<T> ZeroVector(int size, bool isColumn)
        {
            return new Vector<T>(size, isColumn, index => Calculator<T>.DefaultCalculator.AdditiveNeutralElement);
        }

        public Calculator<T> Calculator { get; }

        private readonly Func<int, T> elementProvider;

        public int Size { get; }
        public bool IsColumn { get; }

        public T this[int index]
        {
            get { return ElementAt(index); }
        }

        public T ElementAt(int index)
        {
            return elementProvider(index);
        }

        /// <summary>
        /// Creates a vector with the specified element evaluation function.
        /// The elements are lazily evaluated as they are needed.
        /// </summary>
        /// <param name="size">The number of elements in the vector</param>
        /// <param name="elementProvider">A function that provides an element for each valid index</param>
        /// <param name="calculator">The calculator that should be used for operations among the elements</param>
        public Vector(int size, bool isColumn, Func<int, T> elementProvider, Calculator<T> calculator)
        {
            Size = size;
            IsColumn = isColumn;
            this.elementProvider = elementProvider;
            Calculator = calculator;
        }

        /// <summary>
        /// Creates a vector with the specified element evaluation function.
        /// The elements are lazily evaluated as they are needed.
        /// </summary>
        /// <param name="size">The number of elements in the vector</param>
        /// <param name="elementProvider">A function that provides an element for each valid index</param>
        public Vector(int size, bool isColumn, Func<int, T> elementProvider)
            : this(size, isColumn, elementProvider, Calculator<T>.DefaultCalculator)
        {
        }

        /// <summary>
        /// Creates a vector from the specified array.
        /// The array is not copied, so if an element in the array is substituted, it's substituted in the vector as well.
        /// </summary>
        protected Vector(T[] content, bool isColumn)
            : this(content.Count(), isColumn, index => content[index])
        {
        }

        /// <summary>
        /// Creates a copy of the specified vector.
        /// This evaluates the elements in the provided vector.
        /// </summary>
        public Vector(IVector<T> template)
            : this(template.ToArray(), template.IsColumn)
        {
        }

        public override string ToString()
        {
            var strings = new string[Size];
            for (int i = 0; i < Size; i++)
                strings[i] = ElementAt(i).ToString();
            return "{ " + string.Join(", ", strings) + " }";
        }
    }


    public class MutableVector<T> : Vector<T>
    {
        private readonly T[] content;

        public new T this[int index]
        {
            get { return content[index]; }
            set { content[index] = value; }
        }

        private MutableVector(T[] content, bool isColumn)
            : base(content, isColumn)
        {
            this.content = content;
        }

        public MutableVector(IVector<T> template)
            : this(template.ToArray(), template.IsColumn)
        {
        }
    }


    public struct Vector2D<T> : IVector<T>
    {
        public Calculator<T> Calculator { get { return Calculator<T>.DefaultCalculator; } }

        public int Size { get { return 2; } }
        public bool IsColumn { get { return true; } }

        public readonly T X;
        public readonly T Y;

        public Vector2D(T x, T y)
        {
            X = x;
            Y = y;
        }

        public Vector2D(IVector<T> template)
        {
            if (template.Size != 2)
                throw new ArgumentException("Cannot create " + typeof(Vector2D<T>) + " from vector of size " + template.Size, $"{template}");
            X = template.ElementAt(0);
            Y = template.ElementAt(1);
        }

        public Vector2D(IMatrix<T> template)
            : this(template.ToVector())
        {
        }

        public T ElementAt(int index)
        {
            if (index == 0) return X;
            else if (index == 1) return Y;
            else throw new ArgumentOutOfRangeException($"{index}", index, "index of 2D vector must be 0 or 1");
        }

        public static Vector2D<T> operator +(Vector2D<T> a, IVector<T> b)
        {
            return new Vector2D<T>(a.Add(b));
        }

        public static Vector2D<T> operator -(Vector2D<T> a, IVector<T> b)
        {
            return new Vector2D<T>(a.Subtract(b));
        }

        public static Vector2D<T> operator *(Vector2D<T> vector, T scalar)
        {
            return new Vector2D<T>(vector.Multiply(scalar));
        }

        public static Vector2D<T> operator *(T scalar, Vector2D<T> vector)
        {
            return new Vector2D<T>(vector.Multiply(scalar));
        }

        public static Vector2D<T> operator /(Vector2D<T> vector, T scalar)
        {
            return new Vector2D<T>(vector.Divide(scalar));
        }

        public override string ToString()
        {
            return string.Format("{{ X = {0}, Y = {1} }}", X, Y);
        }

        public override bool Equals(object obj)
        {
            return (obj is Vector2D<T>) ? this.ContentEquals((Vector2D<T>)obj) : false;
        }

        public override int GetHashCode()
        {
            return this.GetContentHashCode();
        }

        public static bool operator ==(Vector2D<T> a, Vector2D<T> b)
        {
            return a.ContentEquals(b);
        }

        public static bool operator !=(Vector2D<T> a, Vector2D<T> b)
        {
            return !a.ContentEquals(b);
        }
    }

    public struct Vector3D<T> : IVector<T>
    {
        public Calculator<T> Calculator { get { return Calculator<T>.DefaultCalculator; } }

        public int Size { get { return 3; } }
        public bool IsColumn { get { return true; } }

        public readonly T X;
        public readonly T Y;
        public readonly T Z;

        public Vector3D(T x, T y, T z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3D(IVector<T> template)
        {
            if (template.Size != 3)
                throw new ArgumentException("Cannot create " + typeof(Vector3D<T>) + " from vector of size " + template.Size, $"{template}");
            X = template.ElementAt(0);
            Y = template.ElementAt(1);
            Z = template.ElementAt(2);
        }

        public T ElementAt(int index)
        {
            if (index == 0) return X;
            else if (index == 1) return Y;
            else if (index == 2) return Z;
            else throw new ArgumentOutOfRangeException($"{index}", index, "index of 3D vector must be 0, 1 or 2");
        }

        public static Vector3D<T> operator +(Vector3D<T> a, IVector<T> b)
        {
            return new Vector3D<T>(a.Add(b));
        }

        public static Vector3D<T> operator -(Vector3D<T> a, IVector<T> b)
        {
            return new Vector3D<T>(a.Subtract(b));
        }

        public static Vector3D<T> operator *(Vector3D<T> vector, T scalar)
        {
            return new Vector3D<T>(vector.Multiply(scalar));
        }

        public static Vector3D<T> operator *(T scalar, Vector3D<T> vector)
        {
            return new Vector3D<T>(vector.Multiply(scalar));
        }

        public static Vector3D<T> operator /(Vector3D<T> vector, T scalar)
        {
            return new Vector3D<T>(vector.Divide(scalar));
        }

        public override string ToString()
        {
            return string.Format("{{ X = {0}, Y = {1}, Z = {2} }}", X, Y, Z);
        }

        public override bool Equals(object obj)
        {
            return (obj is Vector3D<T>) ? this.ContentEquals((Vector3D<T>)obj) : false;
        }

        public override int GetHashCode()
        {
            return this.GetContentHashCode();
        }

        public static bool operator ==(Vector3D<T> a, Vector3D<T> b)
        {
            return a.ContentEquals(b);
        }

        public static bool operator !=(Vector3D<T> a, Vector3D<T> b)
        {
            return !a.ContentEquals(b);
        }

        /// <summary>
        /// Returns the cross-product a x b.
        /// </summary>
        public static Vector3D<T> CrossProduct(Vector3D<T> a, Vector3D<T> b)
        {
            var calc = Calculator<T>.DefaultCalculator;
            return new Vector3D<T>(
                calc.Subtract(calc.Multiply(a.Y, b.Z), calc.Multiply(a.Z, b.Y)),
                calc.Subtract(calc.Multiply(a.Z, b.X), calc.Multiply(a.X, b.Z)),
                calc.Subtract(calc.Multiply(a.X, b.Y), calc.Multiply(a.Y, b.X))
                );
        }
    }

    public struct Vector4D<T> : IVector<T>
    {
        public Calculator<T> Calculator { get { return Calculator<T>.DefaultCalculator; } }

        public int Size { get { return 4; } }
        public bool IsColumn { get { return true; } }

        public readonly T X;
        public readonly T Y;
        public readonly T Z;
        public readonly T W;

        public Vector4D(T x, T y, T z, T w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public Vector4D(IVector<T> template)
        {
            if (template.Size != 4)
                throw new ArgumentException("Cannot create " + typeof(Vector4D<T>) + " from vector of size " + template.Size, $"{template}");
            X = template.ElementAt(0);
            Y = template.ElementAt(1);
            Z = template.ElementAt(2);
            W = template.ElementAt(3);
        }

        public T ElementAt(int index)
        {
            if (index == 0) return X;
            else if (index == 1) return Y;
            else if (index == 2) return Z;
            else if (index == 3) return W;
            else throw new ArgumentOutOfRangeException($"{index}", index, "index of 4D vector must be 0, 1, 2 or 3");
        }

        public static Vector4D<T> operator +(Vector4D<T> a, IVector<T> b)
        {
            return new Vector4D<T>(a.Add(b));
        }

        public static Vector4D<T> operator -(Vector4D<T> a, IVector<T> b)
        {
            return new Vector4D<T>(a.Subtract(b));
        }

        public static Vector4D<T> operator *(Vector4D<T> vector, T scalar)
        {
            return new Vector4D<T>(vector.Multiply(scalar));
        }

        public static Vector4D<T> operator *(T scalar, Vector4D<T> vector)
        {
            return new Vector4D<T>(vector.Multiply(scalar));
        }

        public static Vector4D<T> operator /(Vector4D<T> vector, T scalar)
        {
            return new Vector4D<T>(vector.Divide(scalar));
        }

        public override string ToString()
        {
            return string.Format("{{ X = {0}, Y = {1}, Z = {2}, W = {3} }}", X, Y, Z, W);
        }

        public override bool Equals(object obj)
        {
            return (obj is Vector4D<T>) ? this.ContentEquals((Vector4D<T>)obj) : false;
        }

        public override int GetHashCode()
        {
            return this.GetContentHashCode();
        }

        public static bool operator ==(Vector4D<T> a, Vector4D<T> b)
        {
            return a.ContentEquals(b);
        }

        public static bool operator !=(Vector4D<T> a, Vector4D<T> b)
        {
            return !a.ContentEquals(b);
        }
    }

    public struct Margin : IVector<float>
    {
        public Calculator<float> Calculator { get { return Calculator<float>.DefaultCalculator; } }

        public int Size { get { return 4; } }
        public bool IsColumn { get { return true; } }

        public readonly float Left;
        public readonly float Right;
        public readonly float Top;
        public readonly float Bottom;

        public Margin(float margin)
            : this(margin, margin, margin, margin)
        {
        }

        public Margin(float leftRight, float topBottom)
            : this(leftRight, leftRight, topBottom, topBottom)
        {
        }

        public Margin(float left, float right, float top, float bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }

        public Margin(IVector<float> template)
        {
            if (template.Size != 4)
                throw new ArgumentException("Cannot create " + typeof(Margin) + " from vector of size " + template.Size, $"{template}");
            Left = template.ElementAt(0);
            Right = template.ElementAt(1);
            Top = template.ElementAt(2);
            Bottom = template.ElementAt(3);
        }

        public float ElementAt(int index)
        {
            if (index == 0) return Left;
            else if (index == 1) return Right;
            else if (index == 2) return Top;
            else if (index == 3) return Bottom;
            else throw new ArgumentOutOfRangeException($"{index}", index, "index of 4D vector must be 0, 1, 2 or 3");
        }

        public static Margin operator +(Margin a, IVector<float> b)
        {
            return new Margin(a.Add(b));
        }

        public static Margin operator -(Margin a, IVector<float> b)
        {
            return new Margin(a.Subtract(b));
        }

        public static Margin operator *(Margin vector, float scalar)
        {
            return new Margin(vector.Multiply(scalar));
        }

        public static Margin operator *(float scalar, Margin vector)
        {
            return new Margin(vector.Multiply(scalar));
        }

        public static Margin operator /(Margin vector, float scalar)
        {
            return new Margin(vector.Divide(scalar));
        }

        public override string ToString()
        {
            return string.Format("{{ Left = {0}, Right = {1}, Top = {2}, Bottom = {3} }}", Left, Right, Top, Bottom);
        }

        public override bool Equals(object obj)
        {
            return (obj is Margin) ? this.ContentEquals((Margin)obj) : false;
        }

        public override int GetHashCode()
        {
            return this.GetContentHashCode();
        }

        public static bool operator ==(Margin a, Margin b)
        {
            return a.ContentEquals(b);
        }

        public static bool operator !=(Margin a, Margin b)
        {
            return !a.ContentEquals(b);
        }
    }
}