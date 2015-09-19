using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.UI
{
    public class ContainerView : View<System.Windows.Controls.Canvas>
    {
        private int topmostView = 0;
        private int bottommostView = 0;

        protected void AddSubview(View view)
        {
            if (view == null) throw new ArgumentNullException("view");
            nativeView.Children.Add(view.NativeView);
            
        }

        protected void RemoveSubview(View view)
        {
            if (view == null) throw new ArgumentNullException("view");
            nativeView.Children.Remove(view.NativeView);
        }

        /// <summary>
        /// Replaces a subview of this view by a new view
        /// </summary>
        /// <param name="oldView">the view to be removed</param>
        /// <param name="newView">the view to be added</param>
        /// <returns>the view that was added</returns>
        protected View ReplaceSubview(View oldView, View newView)
        {
            if (oldView != null) RemoveSubview(oldView);
            if (newView != null) AddSubview(newView);
            return newView;
        }

        /// <summary>
        /// Returns the current location of the specified subview.
        /// </summary>
        protected Vector2D<float> GetLocation(View subview)
        {
            var location = new Vector2D<float>((float)System.Windows.Controls.Canvas.GetLeft(subview.NativeView), (float)System.Windows.Controls.Canvas.GetTop(subview.NativeView));
            if (subview.BuiltinPadding) return location;
            return new Vector2D<float>(location.X - subview.Padding.Left, location.Y - subview.Padding.Top);
        }
        /// <summary>
        /// Moves the specified subview to the specified location.
        /// </summary>
        protected void SetLocation(View subview, Vector2D<float> location)
        {
            if (!subview.BuiltinPadding) location += new Vector2D<float>(subview.Padding.Left, subview.Padding.Top);
            System.Windows.Controls.Canvas.SetLeft(subview.NativeView, location.X);
            System.Windows.Controls.Canvas.SetTop(subview.NativeView, location.Y);
        }

        /// <summary>
        /// Brings the specified view to the front.
        /// </summary>
        public void BringToFront(View view)
        {
            System.Windows.Controls.Canvas.SetZIndex(view.NativeView, ++topmostView);
        }

        /// <summary>
        /// Sends the specified view to the back.
        /// </summary>
        public void SendToBack(View view)
        {
            System.Windows.Controls.Canvas.SetZIndex(view.NativeView, --bottommostView);
        }
    }
}
