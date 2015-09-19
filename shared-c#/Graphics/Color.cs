using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using AppInstall.Framework;

namespace AppInstall.Graphics
{
    public class Color : Vector4D<float>
    {
        public float R { get { return this[0]; } }
        public float G { get { return this[1]; } }
        public float B { get { return this[2]; } }
        public float A { get { return this[3]; } }

        public Color()
            : base()
        {
        }

        public Color(float red, float green, float blue, float alpha = 1.0f)
            : base(red, green, blue, alpha)
        {
        }

        public Color(Color color, float alpha)
            : base(color.R, color.G, color.B, alpha)
        {
        }

        public Color(byte red, byte green, byte blue, byte alpha = 255)
            : base(red / 255f, green / 255f, blue / 255f, alpha / 255f)
        {
        }

        [Obsolete("unclear ordering of R/G/B/A (e.g. android stores A at bit 24)")]
        public Color(uint rgba)
            : base(rgba >> 24 & 0xFF, rgba >> 16 & 0xFF, rgba >> 8 & 0xFF, rgba >> 0 & 0xFF)
        {
        }

        public override string ToString()
        {
            return "{ R = " + R + ", G = " + G + ", B = " + B + ", A = " + A + " }";
        }

        public Color AdjustBrightness(float brightness)
        {
            const float R_MULT = .55f, G_MULT = .76f, B_MULT = .34f;
            Vector3D<float> v = brightness * (Vector3D<float>)(new Vector3D<float>(R_MULT * R, G_MULT * G, B_MULT * B).Normalize());
            v = new Vector3D<float>(v[0] / R_MULT, v[1] / G_MULT, v[2] / B_MULT);
            if (v.GetLargest() > 1) v = (1 / v.GetLargest()) * v;
            return new Color(v[0], v[1], v[2], A);
            //var v = (brightness / Math.Max(Math.Max(R, G), B)) * this;
            //return new Color(v.V1, v.V2, v.V3, A);
        }

        /// <summary>
        /// Returns an average of multiple colors.
        /// The average is taken of the square of the RGB values for better visual correctness.
        /// </summary>
        public static Color Blend(params Color[] colors)
        {
            var totalR = colors.Sum((color) => color.R * color.R);
            var totalG = colors.Sum((color) => color.G * color.G);
            var totalB = colors.Sum((color) => color.B * color.B);
            var totalA = colors.Sum((color) => color.A);
            return new Color(
                (float)Math.Sqrt(totalR / colors.Count()),
                (float)Math.Sqrt(totalG / colors.Count()),
                (float)Math.Sqrt(totalB / colors.Count()),
                totalA / colors.Count()
                );
        }


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
    }
}
