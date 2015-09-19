using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UIKit;
using CoreGraphics;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    public abstract class View<T> : View where T : UIView, new()
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
            nativeView.UserInteractionEnabled = true;
            nativeView.Hidden = false;
            nativeView.Opaque = true;
        }

        protected View(T view, bool builtinPadding)
            : base(view, builtinPadding)
        {
            nativeView = view;
        }

        protected override Vector2D<float> GetContentSize(Vector2D<float> maxSize)
        {
            return nativeView.SizeThatFits(maxSize.ToCGSize()).ToVector2D();
        }

        protected override void UpdateContentLayout()
        {

        }
    }


    public abstract class View
    {
        protected UIView nativeView;

        /// <summary>
        /// Returns the native view object associated with this wrapper.
        /// </summary>
        public UIView NativeView { get { return nativeView; } }

        public Margin Padding { get; set; } // left - right - top - bottom
        public Margin Margin { get; set; } // left - right - top - bottom
        //public Vector2D<float> Location { get; set; } // X - Y
        public Vector2D<float> Size { get; set; } // width - height
        public float Opacity { get { return (float)nativeView.Alpha; } set { nativeView.Alpha = value; } }

        [Obsolete("a background is not neccessarily a solid color")]
        public Color BackgroundColor { get { return nativeView.BackgroundColor == null ? Color.Clear : nativeView.BackgroundColor.ToColor(); } set { nativeView.BackgroundColor = value.ToUIColor(); } }

        public Color BorderColor { get { return nativeView.Layer.BorderColor.ToColor(); } set { nativeView.Layer.BorderColor = value.ToCGColor(); } }
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
        /// <param name="content"></param>
        /// <param name="builinPadding"></param>
        protected View(UIView content, bool builinPadding)
        {
            if (content == null) throw new ArgumentNullException("content");
            this.nativeView = content;
            content.UserInteractionEnabled = true;
            content.Hidden = false;
            content.Opaque = true;
            BuiltinPadding = builinPadding;
            Padding = new Margin();
            Margin = new Margin();
            Size = content.Frame.Size.ToVector2D();
        }

        /// <summary>
        /// Returns true if there is no way to see through the view
        /// </summary>
        public virtual bool IsOpaque()
        {
            return (Opacity == 1) && (BackgroundColor.A == 1);
        }

        /// <summary>
        /// This method must be overridden by an inheriting class and should return the minimum size of the content
        /// given the specified max size and respecting the childrens's margins.
        /// todo: consider a separate measure and layout pass: store the content size instead of returning it
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
            if (maxSize == null) throw new ArgumentNullException($"{maxSize}");
            if (padding == null) throw new ArgumentNullException($"{padding}");

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

            var old = nativeView.Frame; // the frame location is already adjusted for padding (whether built-in or not)
            if (BuiltinPadding)
                nativeView.Frame = new CGRect(nativeView.Frame.Location, Size.ToCGSize());
            else
                nativeView.Frame = new CGRect(nativeView.Frame.Location.X, nativeView.Frame.Location.Y, Size.X - Padding.Left - Padding.Right, Size.Y - Padding.Top - Padding.Bottom);
            Application.UILog.Log("frame of " + this.GetHashCode() + " adjusted from " + old + " to " + nativeView.Frame, LogType.Debug);

            if (Shadow) {
                nativeView.Layer.MasksToBounds = false;
                nativeView.Layer.ShadowOffset = new CGSize(0, 0f);
                nativeView.Layer.ShadowRadius = 6f;
                nativeView.Layer.ShadowOpacity = 0.5f;
                nativeView.Layer.ShadowColor = Color.Black.ToCGColor();
                nativeView.Layer.ShadowPath = CoreGraphics.CGPath.FromRect(nativeView.Frame);
            }
            UpdateContentLayout();
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


    public class LayoutSensitiveView : UIView
    {
        public event Action WillUpdateLayout;

        public override void LayoutSubviews()
        {
            WillUpdateLayout.SafeInvoke();
            base.LayoutSubviews();
        }
    }

    public class PlatformViewWrapper : View<LayoutSensitiveView>
    {
        private View innerView;
        private Func<UIView, bool> layoutActions;

        /// <summary>
        /// Creates a platform wrapper for the specified view.
        /// If the platform resizes the wrapper view, the inner view is resized accordingly.
        /// If the code resizes or moves the inner view, the wrapper view is resized and moved accordingly.
        /// Note that the location of the inner view will always be reset to zero and only the wrapper view is actually moved.
        /// Do not directly modify properties (especially Location and Size) on this wrapper instance.
        /// Note: the location property is currently not properly transferred from the inner view.
        /// </summary>
        /// <param name="innerView">The view that should be encapsulated.</param>
        /// <param name="layoutActions">This handler can be used to enforce layout constraints. It will be invoked prior to every layout update and should return true if it altered the Size property of the inner view.</param>
        public PlatformViewWrapper(View innerView, Func<UIView, bool> layoutActions = null)
            : base(true)
        {
            this.layoutActions = layoutActions;
            this.innerView = innerView;
            nativeView.AddSubview(innerView.NativeView);
            Size = innerView.GetMinSize(new Vector2D<float>(float.MaxValue, float.MaxValue));
            UpdateLayout();

            this.nativeView.WillUpdateLayout += () => TransferLayout(true);
            innerView.WillUpdateLayout += (o) => TransferLayout(false);
        }

        private void TransferLayout(bool fromPlatform)
        {
            Application.UILog.Log("layout of " + innerView + " from " + (fromPlatform ? "platform" : "code"));

            var oldPadding = innerView.Padding.Copy();

            if (layoutActions != null)
                fromPlatform = !layoutActions(nativeView) && fromPlatform;

            // only sync properties if they became inconsistent to prevent infinite recursion.
            var zeroVector = new Vector2D<float>(0, 0);
            var platformLocation = nativeView.Frame.Location.ToVector2D();
            var platformSize = nativeView.Frame.Size.ToVector2D();
            Application.UILog.Log("inner size " + innerView.Size + " and platform frame " + nativeView.Frame);
            if (platformSize != innerView.Size || Size != innerView.Size || oldPadding != innerView.Padding) {
                Application.UILog.Log("size of " + innerView + " changed from " + (fromPlatform ? "platform" : "code") + ", location is " + platformLocation);

                if (fromPlatform)
                    innerView.Size = Size = platformSize;
                else
                    Size = innerView.Size;

                UpdateLayout();
                innerView.UpdateLayout();
                //content.LayoutSubviews();
            }
        }

        protected override Vector2D<float> GetContentSize(Vector2D<float> maxSize)
        {
            return innerView.GetMinSize(maxSize);
        }

        protected override void UpdateContentLayout()
        {
        }
    }
}