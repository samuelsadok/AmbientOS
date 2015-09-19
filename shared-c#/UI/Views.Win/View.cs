using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{

    public abstract class View
    {
        protected System.Windows.FrameworkElement nativeView;

        /// <summary>
        /// Returns the native view object associated with this wrapper.
        /// </summary>
        public System.Windows.FrameworkElement NativeView { get { return nativeView; } }

        public Margin Padding { get; set; } // left - right - top - bottom
        public Margin Margin { get; set; }
        public Vector2D<float> Size { get; set; } // width - height
        public bool Enabled { get { return nativeView.IsEnabled; } set { nativeView.IsEnabled = value; } }
        public double Opacity { get { return nativeView.Opacity; } set { Animation.AnimateOpacity(this, value); } }
        //public Color BackgroundColor { get { return backgroundColor; } set { nativeView.Background = new System.Windows.Media.SolidColorBrush((backgroundColor = value).ToMediaColor()); } }
        private Color backgroundColor;
        public bool Shadow { get; set; }
        public bool Autosize { get; set; }

        /// <summary>
        /// Specifies if this view should use built-in padding of the underlying platform view.
        /// </summary>
        public bool BuiltinPadding { get; private set; }


        public event Action<View> WillUpdateLayout;


        /// <summary>
        /// Creates a new view from a platform specific view object.
        /// If custom padding is requested, the UpdateLayout method aligns the contents
        /// frame with the specified Location and Size and leaves padding to the overridden UpdateContentLayout method.
        /// Else, the the content size is set to the specified size minus the padding.
        /// </summary>
        protected View(System.Windows.FrameworkElement content, bool builtinPadding)
        {
            if (content == null) throw new ArgumentNullException("content");
            this.nativeView = content;
            //content.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            //content.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            content.Visibility = System.Windows.Visibility.Visible;
            Padding = new Margin();
            Margin = new Margin();
            Size = new Vector2D<float>((float)content.ActualWidth, (float)content.ActualHeight);
            BuiltinPadding = builtinPadding;
        }


        /// <summary>
        /// Returns true if there is no way to see through the view
        /// </summary>
        public virtual bool IsOpaque()
        {
            return (Opacity == 1); //&& (BackgroundColor.A == 1); // todo: take background into account
        }

        /// <summary>
        /// This method must be overridden by an inheriting class and should return the minimum size of the content
        /// given the specified max size and respecting the childrens's margins.
        /// </summary>
        protected abstract Vector2D<float> GetContentSize(Vector2D<float> maxSize);

        /// <summary>
        /// Returns the minimum size of the view so that it would enclose the content, respecting the own padding and the children's margins.
        /// </summary>
        public Vector2D<float> GetMinSize(Vector2D<float> maxSize)
        {
            return GetMinSize(maxSize, Padding);
        }

        /// <summary>
        /// Returns the minimum size of the view so that it would enclose the content, respecting the specified padding and the children's margins.
        /// </summary>
        public Vector2D<float> GetMinSize(Vector2D<float> maxSize, Margin padding)
        {
            if (maxSize == null) throw new ArgumentNullException("maxSize");
            if (padding == null) throw new ArgumentNullException("padding");

            var paddingOverhead = new Vector2D<float>(padding.Left + padding.Right, padding.Top + padding.Bottom);
            var contentSize = GetContentSize(maxSize - paddingOverhead);

            if (contentSize == null) throw new MethodAccessException("the derived class of type " + this.GetType() + " returned a content size of null.");
            return contentSize + paddingOverhead;
        }

        /// <summary>
        /// This method must be overridden by an inheriting class and should update the layout of the view content.
        /// </summary>
        protected abstract void UpdateContentLayout();

        /// <summary>
        /// Arranges the subviews to fit the new layout
        /// </summary>
        public void UpdateLayout()
        {
            if (Autosize)
                Size = GetMinSize(new Vector2D<float>(float.MaxValue, float.MaxValue));

            WillUpdateLayout.SafeInvoke(this);

            if (BuiltinPadding) {
                nativeView.Width = this.Size.X - this.Padding.Left - this.Padding.Right;
                nativeView.Height = this.Size.Y - this.Padding.Top - this.Padding.Bottom;
            } else {
                nativeView.Width = Math.Max(0, this.Size.X - this.Padding.Left - this.Padding.Right);
                nativeView.Height = Math.Max(0, this.Size.Y - this.Padding.Top - this.Padding.Bottom);
            }

            // todo: draw shadow
            // todo: set location

            UpdateContentLayout();
        }


        public System.Windows.FrameworkElement ToNativeView()
        {
            //nativeView.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            //nativeView.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            //nativeView.SizeChanged += (o, e) => {
            //    Size = new Vector2D<float>((float)nativeView.ActualWidth, (float)nativeView.ActualHeight);
            //    UpdateLayout();
            //};



            Action didResize = () => {
                Application.UILog.Log("height " + nativeView.ActualHeight);
            };
            EventHandler didResizeX = (o, e) => {
                Application.UILog.Log("width " + nativeView.ActualWidth);
                if (nativeView.ActualWidth != Size.X) {
                    Size = new Vector2D<float>((float)nativeView.ActualWidth, Size.Y);
                    UpdateContentLayout();
                }
                //didResize();
            };
            EventHandler didResizeY = (o, e) => {
                Application.UILog.Log("height " + nativeView.ActualHeight);
                if (nativeView.ActualHeight != Size.Y) {
                    Size = new Vector2D<float>(Size.X, (float)nativeView.ActualHeight);
                    UpdateContentLayout();
                }

                //didResize();
            };



            var pdX = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(System.Windows.FrameworkElement.ActualWidthProperty, typeof(System.Windows.FrameworkElement));
            var pdY = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(System.Windows.FrameworkElement.ActualHeightProperty, typeof(System.Windows.FrameworkElement));
            pdX.AddValueChanged(nativeView, didResizeX);
            pdY.AddValueChanged(nativeView, didResizeY);

            //nativeView.LayoutUpdated += (o, e) => {
            //    Application.UILog.Log("height " + nativeView.ActualHeight);
            //    var newSize = new Vector2D<float>((float)nativeView.ActualWidth, (float)nativeView.ActualHeight);
            //    if (Size != newSize)
            //        Size = newSize;
            //};
            //nativeView.SizeChanged += (o, e) => {
            //    WillUpdateLayout.SafeInvoke(this);
            //    UpdateContentLayout();
            //};

            return nativeView;
        }




        /// <summary>
        /// Generates a dump that describes the layout of this view and it's children.
        /// </summary>
        /// <param name="dump">A string builder to which the dump is appended</param>
        /// <param name="indent">The indentation prefix to be used for each dump line</param>
        /// <param name="tag">A tag that describes the role of this view</param>
        public virtual void DumpLayout(StringBuilder dump, string indent, string tag = null)
        {
            dump.AppendLine(indent + this.GetType() + (tag == null ? "" : " (" + tag + ")") + ", size: " + this.Size);
        }
        protected const string DUMP_INDENT_STEP = "|   ";
    }



    public abstract class View<T> : View where T : System.Windows.FrameworkElement, new()
    {
        protected new T nativeView;

        /// <summary>
        /// Creates a new view based on the specified platform view and using built in padding.
        /// By default, built-in functions are used to determine the minimum size and update the layout.
        /// This behavior can be customized by overriding the GetContentSize and UpdateContentLayout methods.
        /// </summary>
        protected View()
            : this(false)
        {
        }

        protected View(bool builtinPadding)
            : this(new T(), builtinPadding)
        {
            nativeView.Visibility = System.Windows.Visibility.Visible;
        }

        protected View(T view, bool builtinPadding)
            : base(view, builtinPadding)
        {
            nativeView = view;
        }

        protected override Vector2D<float> GetContentSize(Vector2D<float> maxSize)
        {
            nativeView.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            return nativeView.DesiredSize.ToVector2D();
        }

        protected Vector2D<float> GetContentConstrainedSize(Vector2D<float> maxSize)
        {
            nativeView.Measure(maxSize.ToSize());
            return nativeView.DesiredSize.ToVector2D();
        }

        protected override void UpdateContentLayout()
        {

        }
    }
}
