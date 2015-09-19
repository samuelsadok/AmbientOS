using UIKit;
using AppInstall.Framework;

namespace AppInstall.UI
{
    public class ContainerView : View<UIView>
    {
        protected ContainerView()
            : base(true)
        {
        }

        /// <summary>
        /// Adds the specified subview to this view in front of all other subviews.
        /// Prior to adding the subview, it is removed from its previous parent.
        /// If the subview is already contained by this view, it is only brought to the front.
        /// </summary>
        /// <param name="view">the subview to be added</param>
        /// <param name="toFront">if false, the view is added behind all other views</param>
        protected void AddSubview(View view, bool toFront = true)
        {
            if (view.NativeView.Superview != null)
                view.NativeView.RemoveFromSuperview();
            
            nativeView.AddSubview(view.NativeView);

            if (!toFront)
                nativeView.SendSubviewToBack(view.NativeView);
        }

        /// <summary>
        /// Removes a subview.
        /// No action is taken if the specified subview is not a subview of this view. todo: verify
        /// </summary>
        protected void RemoveSubview(View view)
        {
            if (view.NativeView.Superview == nativeView)
                view.NativeView.RemoveFromSuperview();
        }

        /// <summary>
        /// Replaces a subview of this view by a new view.
        /// </summary>
        /// <param name="oldView">The view to be removed. Can be null.</param>
        /// <param name="newView">The view to be added.</param>
        /// <returns>The view that was added</returns>
        protected View ReplaceSubview(View oldView, View newView)
        {
            if (oldView != null) RemoveSubview(oldView);
            AddSubview(newView);
            return newView;
        }

        /// <summary>
        /// Returns the current location of the specified subview.
        /// </summary>
        protected Vector2D<float> GetLocation(View subview)
        {
            var location = subview.NativeView.Frame.Location.ToVector2D();
            if (subview.BuiltinPadding) return location;
            return new Vector2D<float>(location.X - subview.Padding.Left, location.Y - subview.Padding.Top);
        }
        /// <summary>
        /// Moves the specified subview to the specified location.
        /// </summary>
        protected void SetLocation(View subview, Vector2D<float> location)
        {
            if (!subview.BuiltinPadding) location += new Vector2D<float>(subview.Padding.Left, subview.Padding.Top);
            subview.NativeView.Frame = new CoreGraphics.CGRect(location.ToCGPoint(), subview.NativeView.Frame.Size);
        }

        /// <summary>
        /// Brings the specified view to the front.
        /// </summary>
        public void BringToFront(View view)
        {
            nativeView.BringSubviewToFront(view.NativeView);
        }

        /// <summary>
        /// Sends the specified view to the back.
        /// </summary>
        public void SendToBack(View view)
        {
            nativeView.SendSubviewToBack(view.NativeView);
        }
    }
}
