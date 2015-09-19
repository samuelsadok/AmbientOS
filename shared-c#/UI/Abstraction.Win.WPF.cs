using System;
using System.Collections.Generic;
using System.Linq;
using AppInstall.Framework;
using AppInstall.OS;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    public static partial class Abstraction
    {
        public static Vector2D<float> ToVector2D(this System.Windows.Point point)
        {
            return new Vector2D<float>((float)point.X, (float)point.Y);
        }
        public static Vector2D<float> ToVector2D(this System.Windows.Size size)
        {
            return new Vector2D<float>((float)size.Width, (float)size.Height);
        }
        public static Vector4D<float> ToVector4D(this System.Windows.Rect rectangle)
        {
            return new Vector4D<float>((float)rectangle.Width, (float)rectangle.Height, (float)rectangle.X, (float)rectangle.Y);
        }
        public static System.Windows.Point ToPoint(this Vector2D<float> vector)
        {
            return new System.Windows.Point(vector.X, vector.Y);
        }
        public static System.Windows.Size ToSize(this Vector2D<float> vector)
        {
            return new System.Windows.Size(vector.X, vector.Y);
        }
        public static System.Windows.Rect ToRectangle(this Vector4D<float> vector)
        {
            return new System.Windows.Rect(vector.V1, vector.V2, vector.V3, vector.V4);
        }
        public static System.Windows.Media.Color ToWPFColor(this Color color)
        {
            return new System.Windows.Media.Color() { ScR = color.R, ScG = color.G, ScB = color.B, ScA = color.A };
        }
        public static System.Windows.Media.Brush ToBrush(this Color color)
        {
            return new System.Windows.Media.SolidColorBrush(color.ToWPFColor());
        }
        public static TextAlignment ToTextAlignment(this System.Windows.TextAlignment alignment)
        {
            switch (alignment) {
                case System.Windows.TextAlignment.Left: return TextAlignment.Left;
                case System.Windows.TextAlignment.Center: return TextAlignment.Center;
                case System.Windows.TextAlignment.Right: return TextAlignment.Right;
                default: return TextAlignment.Justified;
            }
        }
        public static System.Windows.TextAlignment ToWPFTextAlignment(this TextAlignment alignment)
        {
            switch (alignment) {
                case TextAlignment.Left: return System.Windows.TextAlignment.Left;
                case TextAlignment.Center: return System.Windows.TextAlignment.Center;
                case TextAlignment.Right: return System.Windows.TextAlignment.Right;
                default: return System.Windows.TextAlignment.Justify;
            }
        }
    }


    public class CustomBinding<T> : System.Windows.Data.Binding
    {
        private Func<T> getter;
        private Action<T> setter;
        public CustomBinding(Func<T> getter, Action<T> setter)
        {
            this.getter = getter;
            this.setter = setter;
        }
        public object Value
        {
            get { return getter(); }
            set { setter((T)value); }
        }
    }

    public partial class Animation
    {
        private static Animation currentAnimation = null;
        private System.Windows.Duration duration;
        private List<Tuple<System.Windows.FrameworkElement, System.Windows.DependencyProperty, System.Windows.Media.Animation.AnimationTimeline>> changes;

        public static void AnimateSize(View view, double from, double to)
        {
            if (currentAnimation == null)
                view.NativeView.Width = to;
            else
                currentAnimation.changes.Add(new Tuple<System.Windows.FrameworkElement, System.Windows.DependencyProperty, System.Windows.Media.Animation.AnimationTimeline>(
                    view.NativeView,
                    System.Windows.FrameworkElement.WidthProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(from, to, currentAnimation.duration)

                //AutoReverse = true,
                    //RepeatBehavior = new System.Windows.Media.Animation.RepeatBehavior()
                    ));
        }

        public static void AnimateOpacity(View view, double to)
        {
            if (currentAnimation == null)
                view.NativeView.Opacity = to;
            else
                currentAnimation.changes.Add(new Tuple<System.Windows.FrameworkElement, System.Windows.DependencyProperty, System.Windows.Media.Animation.AnimationTimeline>(
                    view.NativeView,
                    System.Windows.FrameworkElement.OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(view.NativeView.Opacity, to, currentAnimation.duration) { AccelerationRatio = 0.5f, DecelerationRatio = 0.5f }
                    ));
        }


        public void PlatformExecute(int duration)
        {
            this.duration = new System.Windows.Duration(TimeSpan.FromMilliseconds(duration));
            changes = new List<Tuple<System.Windows.FrameworkElement,System.Windows.DependencyProperty,System.Windows.Media.Animation.AnimationTimeline>>();

            currentAnimation = this;
            InvokeAnimatedAction();
            currentAnimation = null;

            if (changes.Any())
                changes.Last().Item3.Completed += (o, e) => InvokeEndAction();

            foreach (var change in changes)
                change.Item1.BeginAnimation(change.Item2, change.Item3);
        }
    }

    public class Dialog
    {
        ViewController view;
        Window win = null;

        /// <summary>
        /// Constructs a new dialog containing the specified view controller.
        /// This constructor can be used in a non-UI thread.
        /// </summary>
        /// <param name="parent">only used for compatibility</param>
        public Dialog(Window parent, ViewController view)
        {
            this.view = view;
        }


        /// <summary>
        /// Displays the dialog.
        /// This can be called in a non-UI thread.
        /// </summary>
        public void Show()
        {
            Platform.InvokeMainThread(() => {
                if (win == null)
                    win = new Window(view);
                win.ShowDialog();
            });
        }

        /// <summary>
        /// Closes the dialog.
        /// This can be called in a non-UI thread.
        /// </summary>
        public void Close()
        {
            Platform.InvokeMainThread(() => {
                if (win == null)
                    throw new InvalidOperationException();
                win.Close();
            });
        }
    }
}
