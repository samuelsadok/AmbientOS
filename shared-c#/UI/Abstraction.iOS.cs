using System;
using System.Drawing;
using UIKit;
using CoreGraphics;
using AppInstall.Framework;
using AppInstall.OS;
using AppInstall.Graphics;

namespace AppInstall.UI
{

    /// <summary>
    /// Provides operators for casting between platform specific and platform independent datatypes
    /// </summary>
    public static class Abstraction
    {
        public static Vector2D<float> ToVector2D(this SizeF size)
        {
            return new Vector2D<float>(size.Width, size.Height);
        }
        public static Vector2D<float> ToVector2D(this PointF point)
        {
            return new Vector2D<float>(point.X, point.Y);
        }
        public static Vector2D<float> ToVector2D(this CGSize size)
        {
            return new Vector2D<float>((float)size.Width, (float)size.Height);
        }
        public static Vector2D<float> ToVector2D(this CGPoint point)
        {
            return new Vector2D<float>((float)point.X, (float)point.Y);
        }
        public static Vector4D<float> ToVector4D(this RectangleF rectangle)
        {
            return new Vector4D<float>(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }
        public static Vector4D<float> ToVector4D(this CGRect rectangle)
        {
            return new Vector4D<float>((float)rectangle.X, (float)rectangle.Y, (float)rectangle.Width, (float)rectangle.Height);
        }
        public static SizeF ToSizeF(this Vector2D<float> vector)
        {
            return new SizeF(vector.X, vector.Y);
        }
        public static PointF ToPointF(this Vector2D<float> vector)
        {
            return new PointF(vector.X, vector.Y);
        }
        public static CGSize ToCGSize(this Vector2D<float> vector)
        {
            return new CGSize(vector.X, vector.Y);
        }
        public static CGPoint ToCGPoint(this Vector2D<float> vector)
        {
            return new CGPoint(vector.X, vector.Y);
        }
        public static RectangleF ToRectangleF(this Vector4D<float> vector)
        {
            return new RectangleF(vector.V1, vector.V2, vector.V3, vector.V4);
        }
        public static CGRect ToCGRect(this Vector4D<float> vector)
        {
            return new CGRect(vector.V1, vector.V2, vector.V3, vector.V4);
        }

        public static Color ToColor(this UIColor color)
        {
            if (color == null)
                return null;
            nfloat r, g, b, a;
            color.GetRGBA(out r, out g, out b, out a);
            return new Color((float)r, (float)g, (float)b, (float)a);
        }
        public static Color ToColor(this CGColor color)
        {
            if (color == null)
                return null;
            return UIColor.FromCGColor(color).ToColor();
        }
        public static UIColor ToUIColor(this Color color)
        {
            if (color == null)
                return null;
            return new UIColor(color.R, color.G, color.B, color.A);
        }
        public static CGColor ToCGColor(this Color color)
        {
            if (color == null)
                return null;
            return new CGColor(color.R, color.G, color.B, color.A);
        }

        public static TextAlignment ToTextAlignment(this UITextAlignment alignment)
        {
            switch (alignment) {
                case UITextAlignment.Left: return TextAlignment.Left;
                case UITextAlignment.Center: return TextAlignment.Center;
                case UITextAlignment.Right: return TextAlignment.Right;
                default: return TextAlignment.Justified;
            }
        }
        public static UITextAlignment ToUITextAlignment(this TextAlignment alignment)
        {
            switch (alignment) {
                case TextAlignment.Left: return UITextAlignment.Left;
                case TextAlignment.Center: return UITextAlignment.Center;
                case TextAlignment.Right: return UITextAlignment.Right;
                default: return UITextAlignment.Justified;
            }
        }
    }


    public partial class Animation
    {
        private void PlatformExecute(int duration)
        {
            UIView.Animate(((float)duration) / 1000f, 0, UIViewAnimationOptions.BeginFromCurrentState, () => { Application.UILog.Log("animating: " + duration); InvokeAnimatedAction(); Application.UILog.Log("animate block end"); }, () => { Application.UILog.Log("animating complete"); InvokeEndAction(); });
        }
    }

    public class Dialog
    {
        Window parent;
        ViewController view;

        public Dialog(Window parent, ViewController view)
        {
            this.parent = parent;
            this.view = view;
        }

        /// <summary>
        /// On large screens: displays a view controller in a new pop-over window.
        /// On small screens: displays a view on top of the current window.
        /// This can be called in a non-UI thread.
        /// </summary>
        public void Show()
        {
            Platform.InvokeMainThread(() => {
                parent.ShowModalViewController(view);
            });
        }

        /// <summary>
        /// Closes the dialog.
        /// This can be called in a non-UI thread.
        /// </summary>
        public void Close()
        {
            Platform.InvokeMainThread(() => {
                parent.DismissModalViewController(view);
            });
        }
    }
}