using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppInstall.Framework
{

    public class Vector<T> : IEnumerable<T>
    {
        private T[] content;
        public T this[int index] {
            get { return content[index]; }
            set { content[index] = value; }
        }
        public int Dimension { get { return content.Count(); } }
        public bool Orientation { get { return true; } }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)content).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return content.GetEnumerator();
        }

        public static void AssertDimensionMatch(Vector<T> val1, Vector<T> val2)
        {
            if (val1.Dimension != val2.Dimension)
                throw new ArgumentException("dimension mismatch (" + val1.Dimension + " != " + val2.Dimension + ")");
        }
        
        public T GetSmallest(out int index)
        {
            index = 0;
            T smallest = Scalar.AbsoluteValue(content[0]);
            T current;
            for (int i = Dimension - 1; i > 0; i--) {
                current = Scalar.AbsoluteValue(this[i]);
                if (!Scalar.IsLarger(current, smallest)) {
                    smallest = current;
                    index = i;
                }
            }
            return this[index];
        }
        
        public T GetLargest(out int index, int startIndex = 0)
        {
            index = startIndex;
            T largest = Scalar.AbsoluteValue(content[startIndex]);
            T current;
            for (int i = startIndex + 1; i < Dimension; i++) {
                current = Scalar.AbsoluteValue(this[i]);
                if (Scalar.IsLarger(current, largest)) {
                    largest = current;
                    index = i;
                }
            }
            return this[index];
        }

        public T GetSmallest()
        {
            int i;
            return GetSmallest(out i);
        }

        public T GetLargest()
        {
            int i;
            return GetLargest(out i);
        }

        public void Swap(int a, int b)
        {
            T x = this[a];
            this[a] = this[b];
            this[b] = x;
        }

        public Vector(int dimension)
        {
            content = new T[dimension];
        }

        public Vector(params T[] elements)
        {
            content = elements;
        }

        public override bool Equals(object obj)
        {
            // compare types
            if (!typeof(Vector<IComparable>).IsAssignableFrom(obj.GetType()))
                return false;
            // compare dimension
            Vector<IComparable> v = (Vector<IComparable>)obj;
            if (v.Dimension != Dimension)
                return false;
            // compare elements
 	        for (int i = 0; i < Dimension; i++)
                if (!v[i].Equals(this[i]))
                    return false;
            return true;
        }

        public override int GetHashCode()
        {
            return content.GetHashCode();
        }

        public override string ToString()
        {
            // todo: make output culture independent
            if (Dimension > 10) return "{ " + Dimension + " dimensional vector }";
            return "{ " + string.Join(", ", from val in content select val.ToString()) + " }";
        }


        /// <summary>
        /// Constructs a vector of the same type and size as a template vector.
        /// </summary>
        public static Vector<V> MakeVector<V>(Vector<V> template)
        {
            var ctor = template.GetType().GetConstructor(new Type[0]);
            if (ctor != null) return (Vector<V>)ctor.Invoke(new object[0]);
            ctor = template.GetType().GetConstructor(new Type[] { typeof(int) });
            return (Vector<V>)ctor.Invoke(new object[] { (int)template.Dimension });
        }

        public Vector<T> Copy()
        {
            Vector<T> result = MakeVector(this);
            for (int i = 0; i < Dimension; i++)
                result[i] = this[i];
            return result;
        }

        /// <summary>
        /// Compares two vectors element-wise. Returns false if the dimensions are unequal.
        /// </summary>
        public static bool operator ==(Vector<T> val1, Vector<T> val2)
        {
            if ((object)val1 == null) return (object)val2 == null;
            if ((object)val2 == null) return false;
            if (val1.Dimension != val2.Dimension)
                return false;
            for (int i = 0; i < val1.Dimension; i++)
                if (!val1[i].Equals(val2[i]))
                    return false;
            return true;
        }

        public static bool operator !=(Vector<T> val1, Vector<T> val2)
        {
            return !(val1 == val2);
        }

        /// <summary>
        /// Performs an element-wise addition of two vectors.
        /// </summary>
        /// <returns></returns>
        public static Vector<T> operator +(Vector<T> val1, Vector<T> val2)
        {
            AssertDimensionMatch(val1, val2);
            Vector<T> result = MakeVector(val1);
            for (int i = 0; i < val1.Dimension; i++)
                result[i] = Scalar.Add(val1[i], val2[i]);
            return result;
        }

        /// <summary>
        /// Performs an element-wise subtraction of two vectors.
        /// </summary>
        public static Vector<T> operator -(Vector<T> val1, Vector<T> val2)
        {
            AssertDimensionMatch(val1, val2);
            Vector<T> result = MakeVector(val1);
            for (int i = 0; i < val1.Dimension; i++)
                result[i] = Scalar.Subtract(val1[i], val2[i]);
            return result;
        }

        /// <summary>
        /// Multiplies two vectors element-wise.
        /// </summary>
        public Vector<T> ElementWiseMultiply(Vector<T> val2)
        {
            AssertDimensionMatch(this, val2);
            Vector<T> product = MakeVector(this);
            for (int i = 0; i < this.Dimension; i++)
                product[i] = Scalar.Multiply(this[i], val2[i]);
            return product;
        }

        /// <summary>
        /// Returns the scalar product (dot-product) of two vectors.
        /// </summary>
        public static T operator *(Vector<T> val1, Vector<T> val2)
        {
            AssertDimensionMatch(val1, val2);
            Vector<T> products = MakeVector(val1);
            for (int i = 0; i < val1.Dimension; i++)
                products[i] = Scalar.Multiply(val1[i], val2[i]);
            return products.content.Aggregate(Scalar.Add);
        }

        /// <summary>
        /// Multiplies each element of the vector with a scalar.
        /// </summary>
        public static Vector<T> operator *(T val1, Vector<T> val2)
        {
            Vector<T> result = MakeVector(val2);
            for (int i = 0; i < val2.Dimension; i++)
                result[i] = Scalar.Multiply(val1, val2[i]);
            return result;
        }

        /// <summary>
        /// Multiplies each element of the vector with a scalar.
        /// </summary>
        public static Vector<T> operator *(Vector<T> val1, T val2)
        {
            Vector<T> result = MakeVector(val1);
            for (int i = 0; i < val1.Dimension; i++)
                result[i] = Scalar.Multiply(val1[i], val2);
            return result;
        }

        /// <summary>
        /// Divides each element of the vector by a scalar.
        /// </summary>
        public static Vector<T> operator /(Vector<T> val1, T val2)
        {
            Vector<T> result = MakeVector(val1);
            for (int i = 0; i < val1.Dimension; i++)
                result[i] = Scalar.Divide(val1[i], val2);
            return result;
        }

        /// <summary>
        /// Returns the euclidean norm (aka length) of the vector (the square root of the vectors scalar product with itself)
        /// </summary>
        public T EuclideanNorm()
        {
            return Scalar.SquareRoot((from c in content select Scalar.Square(c)).Aggregate(Scalar.Add));
        }

        public Vector<T> Normalize()
        {
            return Scalar.MultiplicationInverse(EuclideanNorm()) * this;
        }

        /// <summary>
        /// Returns the perpendicular of this vector to the vector space spanned by the specified vectors
        /// </summary>
        public Vector<T> Orthagonalize(params Vector<T>[] span)
        {
            if (span.Count() == 0)
                return this;
            return this - (from v in span select (this * v) * v).Aggregate((a, b) => a + b);
        }
    }



    public class Vector2D<T> : Vector<T>
    {
        public T X { get { return base[0]; } set { base[0] = value; } }
        public T Y { get { return base[1]; } set { base[1] = value; } }

        public Vector2D()
            : base(2)
        {
        }

        public Vector2D(T x, T y)
            : base(x, y)
        {
        }

        public new Vector2D<T> Copy()
        {
            return (Vector2D<T>)base.Copy();
        }
        public static Vector2D<T> operator +(Vector2D<T> val1, Vector2D<T> val2)
        {
            return (Vector2D<T>)((Vector<T>)val1 + (Vector<T>)val2);
        }
        public static Vector2D<T> operator -(Vector2D<T> val1, Vector2D<T> val2)
        {
            return (Vector2D<T>)((Vector<T>)val1 - (Vector<T>)val2);
        }
        public static Vector2D<T> operator *(T val1, Vector2D<T> val2)
        {
            return (Vector2D<T>)(val1 * (Vector<T>)val2);
        }
        public static Vector2D<T> operator *(Vector2D<T> val1, T val2)
        {
            return (Vector2D<T>)((Vector<T>)val1 * val2);
        }
        public static Vector2D<T> operator /(Vector2D<T> val1, T val2)
        {
            return (Vector2D<T>)((Vector<T>)val1 / val2);
        }
    }



    public class Vector3D<T> : Vector<T>
    {
        public T X { get { return base[0]; } set { base[0] = value; } }
        public T Y { get { return base[1]; } set { base[1] = value; } }
        public T Z { get { return base[2]; } set { base[2] = value; } }
        
        public Vector3D()
            : base(3)
        {
        }

        public Vector3D(T x, T y, T z)
            : base(x, y, z)
        {
        }

        public new Vector3D<T> Copy()
        {
            return (Vector3D<T>)base.Copy();
        }
        public static Vector3D<T> operator +(Vector3D<T> val1, Vector3D<T> val2)
        {
            return (Vector3D<T>)((Vector<T>)val1 + (Vector<T>)val2);
        }
        public static Vector3D<T> operator -(Vector3D<T> val1, Vector3D<T> val2)
        {
            return (Vector3D<T>)((Vector<T>)val1 - (Vector<T>)val2);
        }
        public static Vector3D<T> operator *(T val1, Vector3D<T> val2)
        {
            return (Vector3D<T>)(val1 * (Vector<T>)val2);
        }
        public static Vector3D<T> operator *(Vector3D<T> val1, T val2)
        {
            return (Vector3D<T>)((Vector<T>)val1 * val2);
        }
        public static Vector3D<T> operator /(Vector3D<T> val1, T val2)
        {
            return (Vector3D<T>)((Vector<T>)val1 / val2);
        }


        /// <summary>
        /// Returns the cross-product val1 x val2.
        /// </summary>
        public static Vector3D<T> CrossProduct(Vector3D<T> val1, Vector3D<T> val2)
        {
            return new Vector3D<T>(
                Scalar.Subtract(Scalar.Multiply(val1.Y, val2.Z), Scalar.Multiply(val1.Z, val2.Y)),
                Scalar.Subtract(Scalar.Multiply(val1.Z, val2.X), Scalar.Multiply(val1.X, val2.Z)),
                Scalar.Subtract(Scalar.Multiply(val1.X, val2.Y), Scalar.Multiply(val1.Y, val2.X))
                );
        }
    }



    public class Vector4D<T> : Vector<T>
    {
        public T V1 { get { return base[0]; } set { base[0] = value; } }
        public T V2 { get { return base[1]; } set { base[1] = value; } }
        public T V3 { get { return base[2]; } set { base[2] = value; } }
        public T V4 { get { return base[3]; } set { base[3] = value; } }
        
        public Vector4D()
            : base(4)
        {
        }

        public Vector4D(T v1, T v2, T v3, T v4)
            : base(v1, v2, v3, v4)
        {
        }

        public new Vector4D<T> Copy()
        {
            return (Vector4D<T>)base.Copy();
        }
        public static Vector4D<T> operator +(Vector4D<T> val1, Vector4D<T> val2)
        {
            return (Vector4D<T>)((Vector<T>)val1 + (Vector<T>)val2);
        }
        public static Vector4D<T> operator -(Vector4D<T> val1, Vector4D<T> val2)
        {
            return (Vector4D<T>)((Vector<T>)val1 - (Vector<T>)val2);
        }
        public static Vector4D<T> operator *(T val1, Vector4D<T> val2)
        {
            return (Vector4D<T>)(val1 * (Vector<T>)val2);
        }
        public static Vector4D<T> operator *(Vector4D<T> val1, T val2)
        {
            return (Vector4D<T>)((Vector<T>)val1 * val2);
        }
        public static Vector4D<T> operator /(Vector4D<T> val1, T val2)
        {
            return (Vector4D<T>)((Vector<T>)val1 / val2);
        }
    }

    public class Margin : Vector<float>
    {
        public float Left { get { return base[0]; } set { base[0] = value; } }
        public float Right { get { return base[1]; } set { base[1] = value; } }
        public float Top { get { return base[2]; } set { base[2] = value; } }
        public float Bottom { get { return base[3]; } set { base[3] = value; } }

        public Margin()
            : base(4)
        {
        }

        public Margin(float margin)
            : base(margin, margin, margin, margin)
        {
        }

        public Margin(float leftRight, float topBottom)
            : base(leftRight, leftRight, topBottom, topBottom)
        {
        }

        public Margin(float left, float right, float top, float bottom)
            : base(left, right, top, bottom)
        {
        }

        public new Margin Copy()
        {
            return (Margin)base.Copy();
        }
        public static Margin operator +(Margin val1, Margin val2)
        {
            return (Margin)((Vector<float>)val1 + (Vector<float>)val2);
        }
        public static Margin operator -(Margin val1, Margin val2)
        {
            return (Margin)((Vector<float>)val1 - (Vector<float>)val2);
        }
    }


    public static class VectorCollections
    {
        /// <summary>
        /// Returns the a vector that reflects both the (absolutely) largest X and Y value found in the collection.
        /// Vectors that are null are ignored.
        /// </summary>
        public static Vector2D<T> Max<T>(this IEnumerable<Vector2D<T>> collection)
        {
            if (collection == null) throw new ArgumentNullException("collection");
            return collection.Where((v) => v != null).Aggregate(new Vector2D<T>(),
                (a, b) => {
                    if (a == null) throw new NullReferenceException("a is null");
                    if (b == null) throw new NullReferenceException("b is null");
                    return new Vector2D<T>(Scalar.Max(a.X, b.X),
                    Scalar.Max(a.Y, b.Y));
                });
        }
    }

}