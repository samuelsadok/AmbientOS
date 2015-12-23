using System;
using System.Collections.Generic;
using System.Linq;

namespace AmbientOS
{
    public class Calculator
    {
        public static Calculator<T> GetDefaultCalculator<T>()
        {
            var t = typeof(T);

            if (t == typeof(sbyte)) return (Calculator<T>)(Calculator)SByteCalculator.DefaultInstance;
            if (t == typeof(byte)) return (Calculator<T>)(Calculator)ByteCalculator.DefaultInstance;
            if (t == typeof(Int16)) return (Calculator<T>)(Calculator)Int16Calculator.DefaultInstance;
            if (t == typeof(UInt16)) return (Calculator<T>)(Calculator)UInt16Calculator.DefaultInstance;
            if (t == typeof(Int32)) return (Calculator<T>)(Calculator)Int32Calculator.DefaultInstance;
            if (t == typeof(UInt32)) return (Calculator<T>)(Calculator)UInt32Calculator.DefaultInstance;
            if (t == typeof(Int64)) return (Calculator<T>)(Calculator)Int64Calculator.DefaultInstance;
            if (t == typeof(UInt64)) return (Calculator<T>)(Calculator)UInt64Calculator.DefaultInstance;
            if (t == typeof(float)) return (Calculator<T>)(Calculator)FloatCalculator.DefaultInstance;
            if (t == typeof(double)) return (Calculator<T>)(Calculator)DoubleCalculator.DefaultInstance;

            // todo: add calculator for complex numbers, large numbers and matrices

            throw new NotSupportedException("no default calculator available for " + t);
        }
    }

    /// <summary>
    /// On some devices (such as iOS), dynamic compilation is not allowed, hence we need to supply precompiled operations for each scalar type.
    /// This class provides a generic implementation of common arithmetic operations.
    /// To support a new arithmetic type, you must inherit from this class, override the applicable functions and provide the new calculator in GetDefaultCalculator.
    /// Some functions are cannot be overriden, because they have a default implementation.
    /// If a good reason surfaces to override these functions, they can be changed to virtual.
    /// </summary>
    public abstract class Calculator<T> : Calculator
    {
        /// <summary>
        /// Returns the default calculator for the specified type argument.
        /// The evaluation is done at compile time, so access to this property is efficient.
        /// </summary>
        public static Calculator<T> DefaultCalculator { get; } = GetDefaultCalculator<T>();

        /// <summary>
        /// Returns the element of this type that is neutral with respect to addition.
        /// For normal numbers this is 0.
        /// </summary>
        public virtual T AdditiveNeutralElement { get { throw new InvalidOperationException("additive neutral element not available for " + typeof(T)); } }

        /// <summary>
        /// Returns the element of this type that is neutral with respect to multiplication.
        /// For normal numbers this is 1.
        /// </summary>
        public virtual T MultiplicativeNeutralElement { get { throw new InvalidOperationException("multiplicative neutral element not available for " + typeof(T)); } }

        /// <summary>
        /// Returns the additive inverse of a number, e.g. for 5 this is -5.
        /// This operation is invalid in cases where negative numbers are not allowed (e.g. unsigned integers).
        /// </summary>
        public virtual T AdditiveInverse(T a)
        {
            throw new InvalidOperationException("negating not supported by " + typeof(T));
        }

        /// <summary>
        /// Returns the multiplicative inverse of a number, e.g. for 5 this is 0.2.
        /// This operation is invalid in cases where rational numbers are not allowed (e.g. integers).
        /// </summary>
        public virtual T MultiplicativeInverse(T a)
        {
            throw new InvalidOperationException("inverting not supported by " + typeof(T));
        }

        /// <summary>
        /// Adds two elements.
        /// </summary>
        public virtual T Add(T a, T b)
        {
            throw new InvalidOperationException("adding not supported by " + typeof(T));
        }

        /// <summary>
        /// Subtracts b from a.
        /// In theory, this should add the additive inverse of b to a.
        /// However, there may be cases where the additive inverse is not available but subtraction is still desired (e.g. positive integers).
        /// </summary>
        public virtual T Subtract(T a, T b)
        {
            throw new InvalidOperationException("subtracting not supported by " + typeof(T));
        }

        /// <summary>
        /// Multiplies two elements.
        /// </summary>
        public virtual T Multiply(T a, T b)
        {
            throw new InvalidOperationException("adding not supported by " + typeof(T));
        }

        /// <summary>
        /// Divides a by b.
        /// In theory, this should multiply a by the multiplicative inverse of b.
        /// However, there may be cases where the multiplicative inverse is not available but division is still desired (e.g. integers).
        /// </summary>
        public virtual T Divide(T a, T b)
        {
            throw new InvalidOperationException("dividing not supported by " + typeof(T));
        }

        /// <summary>
        /// By default returns a multiplied by itself.
        /// </summary>
        public T Square(T a)
        {
            return Multiply(a, a);
        }

        /// <summary>
        /// Returns the square root of a.
        /// </summary>
        public virtual T SquareRoot(T a)
        {
            throw new InvalidOperationException("square root not supported by " + typeof(T));
        }

        /// <summary>
        /// Returns the complex conjugate of a.
        /// For a real number, this is the number itself.
        /// For a complex number a + ib, this is a - ib.
        /// For a matrix, this is the hermitian transpose.
        /// </summary>
        public virtual T Conjugate(T a)
        {
            throw new InvalidOperationException("complex conjugate not supported by " + typeof(T));
        }

        /// <summary>
        /// Returns the absolute value of this number, e.g. for -5 this is 5.
        /// </summary>
        public virtual T AbsoluteValue(T a)
        {
            throw new InvalidOperationException("absolute value not supported by " + typeof(T));
        }

        /// <summary>
        /// Returns true iif a is larger than b.
        /// </summary>
        public virtual bool IsLarger(T a, T b)
        {
            throw new InvalidOperationException("partial ordering not supported by " + typeof(T));
        }

        /// <summary>
        /// Returns true iif a is smaller than b.
        /// </summary>
        public virtual bool IsSmaller(T a, T b)
        {
            throw new InvalidOperationException("partial ordering not supported by " + typeof(T));
        }

        /// <summary>
        /// Bounds a number to a minumum and maximum value.
        /// </summary>
        public T Bound(T input, T min, T max)
        {
            if (IsSmaller(input, min)) return min;
            if (IsLarger(input, max)) return max;
            return input;
        }

        /// <summary>
        /// Returns the sum of the elements. Returns 0 (the additive neutral element) if the collection is empty.
        /// </summary>
        public T Sum(IEnumerable<T> elements)
        {
            if (!elements.Any())
                return AdditiveNeutralElement;
            return elements.Skip(1).Aggregate(elements.First(), (a, b) => Add(a, b));
        }

        /// <summary>
        /// Returns the product of the elements. Returns 1 (the multiplicative neutral element) if the collection is empty.
        /// </summary>
        public T Product(IEnumerable<T> elements)
        {
            if (!elements.Any())
                return MultiplicativeNeutralElement;
            return elements.Skip(1).Aggregate(elements.First(), (a, b) => Multiply(a, b));
        }

        /// <summary>
        /// Returns the element with the largest absolute value.
        /// </summary>
        public TElement Max<TElement>(IEnumerable<TElement> elements, Func<TElement, T> converter)
        {
            return elements.Skip(1).Aggregate(elements.First(), (a, b) => IsLarger(AbsoluteValue(converter(a)), AbsoluteValue(converter(b))) ? a : b);
        }

        /// <summary>
        /// Returns the element with the largest absolute value.
        /// </summary>
        public T Max(IEnumerable<T> elements)
        {
            return Max(elements, x => x);
        }

        /// <summary>
        /// Returns the element with the smallest absolute value.
        /// </summary>
        public TElement Min<TElement>(IEnumerable<TElement> elements, Func<TElement, T> converter)
        {
            return elements.Skip(1).Aggregate(elements.First(), (a, b) => IsSmaller(AbsoluteValue(converter(a)), AbsoluteValue(converter(b))) ? a : b);
        }

        /// <summary>
        /// Returns the element with the smallest absolute value.
        /// </summary>
        public T Min(IEnumerable<T> elements)
        {
            return Min(elements, x => x);
        }
    }

    public sealed class SByteCalculator : Calculator<sbyte>
    {
        public static SByteCalculator DefaultInstance { get; } = new SByteCalculator();

        public override sbyte AdditiveNeutralElement { get; } = 0;
        public override sbyte MultiplicativeNeutralElement { get; } = 1;

        public override sbyte AdditiveInverse(sbyte a)
        {
            return (sbyte)(-a);
        }

        public override sbyte Add(sbyte a, sbyte b)
        {
            return (sbyte)(a + b);
        }

        public override sbyte Subtract(sbyte a, sbyte b)
        {
            return (sbyte)(a - b);
        }

        public override sbyte Multiply(sbyte a, sbyte b)
        {
            return (sbyte)(a * b);
        }

        public override sbyte Divide(sbyte a, sbyte b)
        {
            return (sbyte)(a / b);
        }

        public override sbyte SquareRoot(sbyte a)
        {
            return (sbyte)Math.Sqrt(a);
        }

        public override sbyte Conjugate(sbyte a)
        {
            return a;
        }

        public override sbyte AbsoluteValue(sbyte a)
        {
            return Math.Abs(a);
        }

        public override bool IsLarger(sbyte a, sbyte b)
        {
            return a > b;
        }

        public override bool IsSmaller(sbyte a, sbyte b)
        {
            return a < b;
        }
    }

    public sealed class ByteCalculator : Calculator<byte>
    {
        public static ByteCalculator DefaultInstance { get; } = new ByteCalculator();

        public override byte AdditiveNeutralElement { get; } = 0;
        public override byte MultiplicativeNeutralElement { get; } = 1;

        public override byte Add(byte a, byte b)
        {
            return (byte)(a + b);
        }

        public override byte Subtract(byte a, byte b)
        {
            return (byte)(a - b);
        }

        public override byte Multiply(byte a, byte b)
        {
            return (byte)(a * b);
        }

        public override byte Divide(byte a, byte b)
        {
            return (byte)(a / b);
        }

        public override byte SquareRoot(byte a)
        {
            return (byte)Math.Sqrt(a);
        }

        public override byte Conjugate(byte a)
        {
            return a;
        }

        public override byte AbsoluteValue(byte a)
        {
            return a;
        }

        public override bool IsLarger(byte a, byte b)
        {
            return a > b;
        }

        public override bool IsSmaller(byte a, byte b)
        {
            return a < b;
        }
    }

    public sealed class Int16Calculator : Calculator<Int16>
    {
        public static Int16Calculator DefaultInstance { get; } = new Int16Calculator();

        public override Int16 AdditiveNeutralElement { get; } = 0;
        public override Int16 MultiplicativeNeutralElement { get; } = 1;

        public override Int16 AdditiveInverse(Int16 a)
        {
            return (Int16)(-a);
        }

        public override Int16 Add(Int16 a, Int16 b)
        {
            return (Int16)(a + b);
        }

        public override Int16 Subtract(Int16 a, Int16 b)
        {
            return (Int16)(a - b);
        }

        public override Int16 Multiply(Int16 a, Int16 b)
        {
            return (Int16)(a * b);
        }

        public override Int16 Divide(Int16 a, Int16 b)
        {
            return (Int16)(a / b);
        }

        public override Int16 SquareRoot(Int16 a)
        {
            return (Int16)Math.Sqrt(a);
        }

        public override Int16 Conjugate(Int16 a)
        {
            return a;
        }

        public override Int16 AbsoluteValue(Int16 a)
        {
            return Math.Abs(a);
        }

        public override bool IsLarger(Int16 a, Int16 b)
        {
            return a > b;
        }

        public override bool IsSmaller(Int16 a, Int16 b)
        {
            return a < b;
        }
    }

    public sealed class UInt16Calculator : Calculator<UInt16>
    {
        public static UInt16Calculator DefaultInstance { get; } = new UInt16Calculator();

        public override UInt16 AdditiveNeutralElement { get; } = 0;
        public override UInt16 MultiplicativeNeutralElement { get; } = 1;

        public override UInt16 Add(UInt16 a, UInt16 b)
        {
            return (UInt16)(a + b);
        }

        public override UInt16 Subtract(UInt16 a, UInt16 b)
        {
            return (UInt16)(a - b);
        }

        public override UInt16 Multiply(UInt16 a, UInt16 b)
        {
            return (UInt16)(a * b);
        }

        public override UInt16 Divide(UInt16 a, UInt16 b)
        {
            return (UInt16)(a / b);
        }

        public override UInt16 SquareRoot(UInt16 a)
        {
            return (UInt16)Math.Sqrt(a);
        }

        public override UInt16 Conjugate(UInt16 a)
        {
            return a;
        }

        public override UInt16 AbsoluteValue(UInt16 a)
        {
            return a;
        }

        public override bool IsLarger(UInt16 a, UInt16 b)
        {
            return a > b;
        }

        public override bool IsSmaller(UInt16 a, UInt16 b)
        {
            return a < b;
        }
    }

    public sealed class Int32Calculator : Calculator<Int32>
    {
        public static Int32Calculator DefaultInstance { get; } = new Int32Calculator();

        public override Int32 AdditiveNeutralElement { get; } = 0;
        public override Int32 MultiplicativeNeutralElement { get; } = 1;

        public override Int32 AdditiveInverse(Int32 a)
        {
            return -a;
        }

        public override Int32 Add(Int32 a, Int32 b)
        {
            return a + b;
        }

        public override Int32 Subtract(Int32 a, Int32 b)
        {
            return a - b;
        }

        public override Int32 Multiply(Int32 a, Int32 b)
        {
            return a * b;
        }

        public override Int32 Divide(Int32 a, Int32 b)
        {
            return a / b;
        }

        public override Int32 SquareRoot(Int32 a)
        {
            return (Int32)Math.Sqrt(a);
        }

        public override Int32 Conjugate(Int32 a)
        {
            return a;
        }

        public override Int32 AbsoluteValue(Int32 a)
        {
            return Math.Abs(a);
        }

        public override bool IsLarger(Int32 a, Int32 b)
        {
            return a > b;
        }

        public override bool IsSmaller(Int32 a, Int32 b)
        {
            return a < b;
        }
    }

    public sealed class UInt32Calculator : Calculator<UInt32>
    {
        public static UInt32Calculator DefaultInstance { get; } = new UInt32Calculator();

        public override UInt32 AdditiveNeutralElement { get; } = 0;
        public override UInt32 MultiplicativeNeutralElement { get; } = 1;

        public override UInt32 Add(UInt32 a, UInt32 b)
        {
            return a + b;
        }

        public override UInt32 Subtract(UInt32 a, UInt32 b)
        {
            return a - b;
        }

        public override UInt32 Multiply(UInt32 a, UInt32 b)
        {
            return a * b;
        }

        public override UInt32 Divide(UInt32 a, UInt32 b)
        {
            return a / b;
        }

        public override UInt32 SquareRoot(UInt32 a)
        {
            return (UInt32)Math.Sqrt(a);
        }

        public override UInt32 Conjugate(UInt32 a)
        {
            return a;
        }

        public override UInt32 AbsoluteValue(UInt32 a)
        {
            return a;
        }

        public override bool IsLarger(UInt32 a, UInt32 b)
        {
            return a > b;
        }

        public override bool IsSmaller(UInt32 a, UInt32 b)
        {
            return a < b;
        }
    }

    public sealed class Int64Calculator : Calculator<Int64>
    {
        public static Int64Calculator DefaultInstance { get; } = new Int64Calculator();

        public override Int64 AdditiveNeutralElement { get; } = 0;
        public override Int64 MultiplicativeNeutralElement { get; } = 1;

        public override Int64 AdditiveInverse(Int64 a)
        {
            return -a;
        }

        public override Int64 Add(Int64 a, Int64 b)
        {
            return a + b;
        }

        public override Int64 Subtract(Int64 a, Int64 b)
        {
            return a - b;
        }

        public override Int64 Multiply(Int64 a, Int64 b)
        {
            return a * b;
        }

        public override Int64 Divide(Int64 a, Int64 b)
        {
            return a / b;
        }

        public override Int64 SquareRoot(Int64 a)
        {
            return (Int64)Math.Sqrt(a);
        }

        public override Int64 Conjugate(Int64 a)
        {
            return a;
        }

        public override Int64 AbsoluteValue(Int64 a)
        {
            return Math.Abs(a);
        }

        public override bool IsLarger(Int64 a, Int64 b)
        {
            return a > b;
        }

        public override bool IsSmaller(Int64 a, Int64 b)
        {
            return a < b;
        }
    }

    public sealed class UInt64Calculator : Calculator<UInt64>
    {
        public static UInt64Calculator DefaultInstance { get; } = new UInt64Calculator();

        public override UInt64 AdditiveNeutralElement { get; } = 0;
        public override UInt64 MultiplicativeNeutralElement { get; } = 1;

        public override UInt64 Add(UInt64 a, UInt64 b)
        {
            return a + b;
        }

        public override UInt64 Subtract(UInt64 a, UInt64 b)
        {
            return a - b;
        }

        public override UInt64 Multiply(UInt64 a, UInt64 b)
        {
            return a * b;
        }

        public override UInt64 Divide(UInt64 a, UInt64 b)
        {
            return a / b;
        }

        public override UInt64 SquareRoot(UInt64 a)
        {
            return (UInt64)Math.Sqrt(a);
        }
        
        public override UInt64 Conjugate(UInt64 a)
        {
            return a;
        }
        
        public override UInt64 AbsoluteValue(UInt64 a)
        {
            return a;
        }

        public override bool IsLarger(UInt64 a, UInt64 b)
        {
            return a > b;
        }

        public override bool IsSmaller(UInt64 a, UInt64 b)
        {
            return a < b;
        }
    }

    public sealed class FloatCalculator : Calculator<float>
    {
        public static FloatCalculator DefaultInstance { get; } = new FloatCalculator();

        public override float AdditiveNeutralElement { get; } = 0;
        public override float MultiplicativeNeutralElement { get; } = 1;

        public override float AdditiveInverse(float a)
        {
            return -a;
        }

        public override float MultiplicativeInverse(float a)
        {
            return 1 / a;
        }

        public override float Add(float a, float b)
        {
            return a + b;
        }

        public override float Subtract(float a, float b)
        {
            return a - b;
        }

        public override float Multiply(float a, float b)
        {
            return a * b;
        }

        public override float Divide(float a, float b)
        {
            return a / b;
        }

        public override float SquareRoot(float a)
        {
            return (float)Math.Sqrt(a);
        }

        public override float Conjugate(float a)
        {
            return a;
        }

        public override float AbsoluteValue(float a)
        {
            return Math.Abs(a);
        }

        public override bool IsLarger(float a, float b)
        {
            return a > b;
        }

        public override bool IsSmaller(float a, float b)
        {
            return a < b;
        }
    }

    public sealed class DoubleCalculator : Calculator<double>
    {
        public static DoubleCalculator DefaultInstance { get; } = new DoubleCalculator();

        public override double AdditiveNeutralElement { get; } = 0;
        public override double MultiplicativeNeutralElement { get; } = 1;

        public override double AdditiveInverse(double a)
        {
            return -a;
        }

        public override double MultiplicativeInverse(double a)
        {
            return 1 / a;
        }

        public override double Add(double a, double b)
        {
            return a + b;
        }

        public override double Subtract(double a, double b)
        {
            return a - b;
        }

        public override double Multiply(double a, double b)
        {
            return a * b;
        }

        public override double Divide(double a, double b)
        {
            return a / b;
        }

        public override double SquareRoot(double a)
        {
            return Math.Sqrt(a);
        }

        public override double Conjugate(double a)
        {
            return a;
        }

        public override double AbsoluteValue(double a)
        {
            return Math.Abs(a);
        }

        public override bool IsLarger(double a, double b)
        {
            return a > b;
        }

        public override bool IsSmaller(double a, double b)
        {
            return a < b;
        }
    }
}