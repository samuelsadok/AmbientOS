using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppInstall.Framework
{

    /// <summary>
    /// Mono cannot dynamically compile on some devices (like iOS), hence we need to supply precompiled operations for each scalar type
    /// </summary>
    public static class Scalar
    {
        //private static Func<byte, byte, byte> AddInt8 = (a, b) => (byte)(a + b);
        //private static Func<Int16, Int16, Int16> AddInt16 = (a, b) => (Int16)(a + b);
        //private static Func<Int32, Int32, Int32> AddInt32 = (a, b) => (Int32)(a + b);
        //private static Func<Int64, Int64, Int64> AddInt64 = (a, b) => (Int64)(a + b);
        //private static Func<UInt16, UInt16, UInt16> AddInt16 = (a, b) => (UInt16)(a + b);
        //private static Func<UInt32, UInt32, UInt32> AddInt32 = (a, b) => (UInt32)(a + b);
        //private static Func<UInt64, UInt64, UInt64> AddInt64 = (a, b) => (UInt64)(a + b);
        //private static Func<float, float, float> AddFloat32 = (a, b) => (float)(a + b);
        //private static Func<double, double, double> AddFloat64 = (a, b) => (double)(a + b);
        //
        //private Func<byte, byte> NegateInt8 = (a) => (byte)(-a);
        //private Func<Int16, Int16> NegateInt16 = (a) => (Int16)(-a);
        //private Func<Int32, Int32> NegateInt32 = (a) => (Int32)(-a);
        //private Func<Int64, Int64> NegateInt64 = (a) => (Int64)(-a);
        //private static Func<float, float> AddFloat32 = (a) => (float)(-a);
        //private static Func<double, double> AddFloat64 = (a) => (double)(-a);


        public static T Add<T>(T a, T b)
        {
            object x = a, y = b;
            Type t = typeof(T);

            if (t == typeof(float)) return (T)(object)((float)x + (float)y);
            if (t == typeof(double)) return (T)(object)((double)x + (double)y);
            if (t == typeof(byte)) return (T)(object)((byte)x + (byte)y);
            if (t == typeof(Int16)) return (T)(object)((Int16)x + (Int16)y);
            if (t == typeof(Int32)) return (T)(object)((Int32)x + (Int32)y);
            if (t == typeof(Int64)) return (T)(object)((Int64)x + (Int64)y);
            if (t == typeof(UInt16)) return (T)(object)((UInt16)x + (UInt16)y);
            if (t == typeof(UInt32)) return (T)(object)((UInt32)x + (UInt32)y);
            if (t == typeof(UInt64)) return (T)(object)((UInt64)x + (UInt64)y);

            throw new InvalidOperationException("adding not supported by " + t.Name);
        }

        public static T AdditiveNeutralElement<T>()
        {
            Type t = typeof(T);

            if (t == typeof(float)) return (T)(object)((float)0);
            if (t == typeof(double)) return (T)(object)((double)0);
            if (t == typeof(byte)) return (T)(object)((byte)0);
            if (t == typeof(Int16)) return (T)(object)((Int16)0);
            if (t == typeof(Int32)) return (T)(object)((Int32)0);
            if (t == typeof(Int64)) return (T)(object)((Int64)0);
            if (t == typeof(UInt16)) return (T)(object)((UInt16)0);
            if (t == typeof(UInt32)) return (T)(object)((UInt32)0);
            if (t == typeof(UInt64)) return (T)(object)((UInt64)0);

            throw new InvalidOperationException("additive neutral element not supported by " + t.Name);
        }

        public static T AdditionInverse<T>(T a)
        {
            object x = a;
            Type t = typeof(T);

            if (t == typeof(float)) return (T)(object)(-(float)x);
            if (t == typeof(double)) return (T)(object)(-(double)x);
            if (t == typeof(byte)) return (T)(object)(-(byte)x);
            if (t == typeof(Int16)) return (T)(object)(-(Int16)x);
            if (t == typeof(Int32)) return (T)(object)(-(Int32)x);
            if (t == typeof(Int64)) return (T)(object)(-(Int64)x);

            throw new InvalidOperationException("negating not supported by " + t.Name);
        }

        public static T Subtract<T>(T a, T b)
        {
            return Add(a, AdditionInverse(b));
        }

        /// <summary>
        /// Returns the absolute value of this number
        /// </summary>
        public static T AbsoluteValue<T>(T a)
        {
            object x = a;
            Type t = typeof(T);

            if (t == typeof(float)) return (T)(object)(Math.Abs((float)x));
            if (t == typeof(double)) return (T)(object)(Math.Abs((double)x));
            if (t == typeof(byte)) return (T)(object)(Math.Abs((byte)x));
            if (t == typeof(Int16)) return (T)(object)(Math.Abs((Int16)x));
            if (t == typeof(Int32)) return (T)(object)(Math.Abs((Int32)x));
            if (t == typeof(Int64)) return (T)(object)(Math.Abs((Int64)x));
            if (t == typeof(UInt16)) return a;
            if (t == typeof(UInt32)) return a;
            if (t == typeof(UInt64)) return a;

            throw new InvalidOperationException("absolute value not supported by " + t.Name);
        }


        /// <summary>
        /// Returns true if a is larger than b
        /// </summary>
        public static bool IsLarger<T>(T a, T b)
        {
            object x = a, y = b;
            Type t = typeof(T);

            if (t == typeof(float)) return ((float)x > (float)y);
            if (t == typeof(double)) return ((double)x > (double)y);
            if (t == typeof(byte)) return ((byte)x > (byte)y);
            if (t == typeof(Int16)) return ((Int16)x > (Int16)y);
            if (t == typeof(Int32)) return ((Int32)x > (Int32)y);
            if (t == typeof(Int64)) return ((Int64)x > (Int64)y);
            if (t == typeof(UInt16)) return ((UInt16)x > (UInt16)y);
            if (t == typeof(UInt32)) return ((UInt32)x > (UInt32)y);
            if (t == typeof(UInt64)) return ((UInt64)x > (UInt64)y);

            throw new InvalidOperationException("comparision not supported by " + t.Name);
        }

        /// <summary>
        /// Bounds a number to a minumum and maximum value
        /// </summary>
        public static T Bound<T>(T input, T min, T max)
        {
            if (IsLarger(input, max)) return max;
            if (IsLarger(min, input)) return min;
            return input;
        }

        /// <summary>
        /// Returns either a or b - whichever one has the larger absolute value
        /// </summary>
        public static T Max<T>(T a, T b)
        {
            return (IsLarger(AbsoluteValue(a), AbsoluteValue(b)) ? a : b);
        }

        public static T Multiply<T>(T a, T b)
        {
            object x = a, y = b;
            Type t = typeof(T);

            if (t == typeof(float)) return (T)(object)((float)x * (float)y);
            if (t == typeof(double)) return (T)(object)((double)x * (double)y);
            if (t == typeof(byte)) return (T)(object)((byte)x * (byte)y);
            if (t == typeof(Int16)) return (T)(object)((Int16)x * (Int16)y);
            if (t == typeof(Int32)) return (T)(object)((Int32)x * (Int32)y);
            if (t == typeof(Int64)) return (T)(object)((Int64)x * (Int64)y);
            if (t == typeof(UInt16)) return (T)(object)((UInt16)x * (UInt16)y);
            if (t == typeof(UInt32)) return (T)(object)((UInt32)x * (UInt32)y);
            if (t == typeof(UInt64)) return (T)(object)((UInt64)x * (UInt64)y);

            throw new InvalidOperationException("multiplying not supported by " + t.Name);
        }

        public static T MultiplicationInverse<T>(T a)
        {
            object x = a;
            Type t = typeof(T);

            if (t == typeof(float)) return (T)(object)(1 / (float)x);
            if (t == typeof(double)) return (T)(object)(1 / (double)x);

            throw new InvalidOperationException("inverting not supported by " + t.Name);
        }

        public static T MultiplicativeNeutralElement<T>()
        {
            Type t = typeof(T);

            if (t == typeof(float)) return (T)(object)((float)1);
            if (t == typeof(double)) return (T)(object)((double)1);
            if (t == typeof(byte)) return (T)(object)((byte)1);
            if (t == typeof(Int16)) return (T)(object)((Int16)1);
            if (t == typeof(Int32)) return (T)(object)((Int32)1);
            if (t == typeof(Int64)) return (T)(object)((Int64)1);
            if (t == typeof(UInt16)) return (T)(object)((UInt16)1);
            if (t == typeof(UInt32)) return (T)(object)((UInt32)1);
            if (t == typeof(UInt64)) return (T)(object)((UInt64)1);

            throw new InvalidOperationException("multiplicative neutral element not supported by " + t.Name);
        }

        public static T Divide<T>(T a, T b)
        {
            return Multiply(a, MultiplicationInverse(b));
        }

        public static T Square<T>(T a)
        {
            return Multiply(a, a);
        }

        public static T SquareRoot<T>(T a)
        {
            object x = a;
            Type t = typeof(T);

            if (t == typeof(float)) return (T)(object)(float)(Math.Sqrt((float)x));
            if (t == typeof(double)) return (T)(object)(double)(Math.Sqrt((double)x));

            throw new InvalidOperationException("square root not supported by " + t.Name);
        }

        /// <summary>
        /// Returns the complex conjugate of a scalar.
        /// </summary>
        public static T Conjugate<T>(T a)
        {
            Type t = typeof(T);

            if (t == typeof(float)) return a;
            if (t == typeof(double)) return a;
            if (t == typeof(byte)) return a;
            if (t == typeof(Int16)) return a;
            if (t == typeof(Int32)) return a;
            if (t == typeof(Int64)) return a;
            if (t == typeof(UInt16)) return a;
            if (t == typeof(UInt32)) return a;
            if (t == typeof(UInt64)) return a;
            // todo: add complex numbers and matrix
            // complex number: change sign of imaginary part
            // matrix: return hermitian transpose

            throw new InvalidOperationException("multiplicative neutral element not supported by " + t.Name);
        }

    }

}