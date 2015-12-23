using System;
using System.Linq;

namespace AmbientOS.Graphics
{
    public struct Color : IVector<float>
    {
        public static Color Red { get { return new Color(1f, 0f, 0f); } }
        public static Color Green { get { return new Color(0f, 1f, 0f); } }
        public static Color Blue { get { return new Color(0f, 0f, 1f); } }
        public static Color Yellow { get { return new Color(1f, 1f, 0f); } }
        public static Color Cyan { get { return new Color(0f, 1f, 1f); } }
        public static Color Magenta { get { return new Color(1f, 0f, 1f); } }

        public static Color Clear { get { return new Color(1f, 1f, 1f, 0f); } }
        public static Color White { get { return new Color(1f, 1f, 1f); } }
        public static Color LightGrey { get { return new Color(0.8f, 0.8f, 0.8f); } }
        public static Color Grey { get { return new Color(0.5f, 0.5f, 0.5f); } }
        public static Color DarkGrey { get { return new Color(0.2f, 0.2f, 0.2f); } }
        public static Color Black { get { return new Color(0f, 0f, 0f); } }

        public static Color Orange { get { return new Color(1f, 0.5f, 0f); } }


        public Calculator<float> Calculator { get { return Calculator<float>.DefaultCalculator; } }

        public int Size { get { return 4; } }
        public bool IsColumn { get { return true; } }

        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly float A;

        public Color(float red, float green, float blue, float alpha = 1.0f)
        {
            R = red;
            G = green;
            B = blue;
            A = alpha;
        }

        public Color(Color color, float alpha)
            : this(color.R, color.G, color.B, alpha)
        {
        }

        public Color(byte red, byte green, byte blue, byte alpha = 255)
            : this(red / 255f, green / 255f, blue / 255f, alpha / 255f)
        {
        }

        [Obsolete("unclear ordering of R/G/B/A (e.g. android stores A at bit 24)")]
        public Color(uint rgba)
            : this(rgba >> 24 & 0xFF, rgba >> 16 & 0xFF, rgba >> 8 & 0xFF, rgba >> 0 & 0xFF)
        {
        }

        public Color(IMatrix<float> template)
        {
            if (template.Rows != 4 || template.Columns != 1)
                throw new ArgumentException("Cannot create " + typeof(Color) + " from matrix of size " + template.Rows + " x " + template.Columns, $"{template}");
            R = template.ElementAt(0, 0);
            G = template.ElementAt(1, 0);
            B = template.ElementAt(2, 0);
            A = template.ElementAt(3, 0);
        }

        public float ElementAt(int row)
        {
            if (row == 0) return R;
            else if (row == 1) return G;
            else if (row == 2) return B;
            else if (row == 3) return A;
            else throw new ArgumentOutOfRangeException($"{row}", row, "row index of 4D vector must be 0, 1, 2 or 3");
        }

        public override string ToString()
        {
            return string.Format("{{ R = {0}, G = {1}, B = {2}, A = {3} }}", R, G, B, A);
        }

        public override bool Equals(object obj)
        {
            return (obj is Color) ? this.ContentEquals((Color)obj) : false;
        }

        public override int GetHashCode()
        {
            return this.ToMatrix().GetContentHashCode();
        }

        public static bool operator ==(Color a, Color b)
        {
            return a.ContentEquals(b);
        }

        public static bool operator !=(Color a, Color b)
        {
            return !a.ContentEquals(b);
        }

        public Color AdjustBrightness(float brightness)
        {
            const float R_MULT = .55f, G_MULT = .76f, B_MULT = .34f;
            var v = new Vector3D<float>(R_MULT * R, G_MULT * G, B_MULT * B).Normalize();
            v = v.Multiply(brightness);
            v = v.ElementWiseMultiply(new Vector3D<float>(1 / R_MULT, 1 / G_MULT, 1 / B_MULT));
            if (v.GetLargest() > 1)
                v = v.Divide(v.GetLargest());
            return new Color(v.ElementAt(0), v.ElementAt(1), v.ElementAt(2), A);
        }

        /// <summary>
        /// Returns an average of multiple colors.
        /// The average is taken of the square of the RGB values for better visual correctness.
        /// </summary>
        public static Color Blend(params Color[] colors)
        {
            var totalR = colors.Sum(color => color.R * color.R);
            var totalG = colors.Sum(color => color.G * color.G);
            var totalB = colors.Sum(color => color.B * color.B);
            var totalA = colors.Sum(color => color.A);
            return new Color(
                (float)Math.Sqrt(totalR / colors.Count()),
                (float)Math.Sqrt(totalG / colors.Count()),
                (float)Math.Sqrt(totalB / colors.Count()),
                totalA / colors.Count()
                );
        }
    }
}
