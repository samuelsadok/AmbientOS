using System;
using System.Text;
using UIKit;
using CoreGraphics;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    public class Window : View<UIWindow>
    {

        /// <summary>
        /// Never triggerd on iOS - only provided for compatibility
        /// </summary>
        public event Action Closed;

        class WindowViewController : UIViewController
        {
            public Color frameColor = Color.Black;

            //public Vector4D<float> Padding { get { return new Vector4D<float>(Math.Max(0, keyboardPadding.V1), Math.Max(0, keyboardPadding.V2), Math.Max(TopLayoutGuide.Length, keyboardPadding.V3), Math.Max(BottomLayoutGuide.Length, keyboardPadding.V4)); } }

            public WindowViewController(Window parent, View view)
            {
                // A view layout will happen on interface rotation, keybord change and status bar size change
                this.View = new PlatformViewWrapper(view, (platformView) => {
                    parent.UpdateFrame();
                    var applicationSpace = parent.screen.ApplicationSpace;
                    var platformPadding = new Margin(
                        applicationSpace.V1 - parent.Location.X,
                        parent.Location.X + parent.Size.X - applicationSpace.V1 - applicationSpace.V3,
                        applicationSpace.V2 - parent.Location.Y,
                        parent.Location.Y + parent.Size.Y - applicationSpace.V2 - applicationSpace.V4
                    );
                    view.Padding = new Margin(
                        Math.Max(Math.Max(parent.keyboardPadding.Left, platformPadding.Left), 0),
                        Math.Max(Math.Max(parent.keyboardPadding.Right, platformPadding.Right), 0),
                        Math.Max(Math.Max(parent.keyboardPadding.Top, platformPadding.Top), 0),
                        Math.Max(Math.Max(parent.keyboardPadding.Bottom, platformPadding.Bottom), 0)
                    );
                    Application.UILog.Log("platform padding " + platformPadding + ", keyboard padding " + parent.keyboardPadding + ", actual padding" + view.Padding);
                    //platformView.Frame = platformView.Bounds;
                    //if (view.CustomPadding)
                    platformView.Frame = new CGRect(0, 0, parent.screen.ApplicationSpace.V1 + parent.screen.ApplicationSpace.V3, parent.screen.ApplicationSpace.V2 + parent.screen.ApplicationSpace.V4);
                    //else
                    //    platformView.Frame = new RectangleF(view.Padding.Left, view.Padding.Top, parent.screen.ApplicationSpace.V1 + parent.screen.ApplicationSpace.V3 - view.Padding.Left - view.Padding.Right, parent.screen.ApplicationSpace.V2 + parent.screen.ApplicationSpace.V4 - view.Padding.Top - view.Padding.Bottom);

                    /*if (parent.viewController != null) {
                        var actualView = parent.viewController.ConstructView();
                        ((LayerLayout)view).Insert(actualView, true);
                        parent.viewController = null;
                    }*/

                    return false; // Size and Location not overridden
                }).NativeView;
            }

            public override UIStatusBarStyle PreferredStatusBarStyle()
            {
                return (frameColor == Color.White ? UIStatusBarStyle.LightContent : UIStatusBarStyle.Default);
            }

            public override void ViewWillTransitionToSize(CGSize toSize, IUIViewControllerTransitionCoordinator coordinator)
            {
                
            }
        }

        WindowViewController controller;
        Margin keyboardPadding = new Margin();
        View view;
        ViewController viewController = null; // may be null
        Screen screen;

        public Vector2D<float> Location { get { return nativeView.Frame.Location.ToVector2D(); } set { nativeView.Frame = new CGRect(value.ToCGPoint(), nativeView.Frame.Size); } }

        private void UpdateFrame()
        {
            Size = nativeView.Frame.Size.ToVector2D();
        }

        public Window(Screen screen, ViewController viewController, Color themeColor)
            : base(new UIWindow(new CGRect(screen.Bounds.V1, screen.Bounds.V2, screen.Bounds.V3, screen.Bounds.V4)), true)
        {
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            if (viewController == null) throw new ArgumentNullException(nameof(view));
            if (themeColor == null) throw new ArgumentNullException(nameof(themeColor));
            view = new LayerLayout(); // { CustomPadding = true };
            view.BackgroundColor = Color.Blue;
            this.screen = screen;

            nativeView.TintColor = themeColor.ToUIColor();
            BackgroundColor = Color.Black;

            Show();

            nativeView.RootViewController = (controller = new WindowViewController(this, view));

            UIKeyboard.Notifications.ObserveWillChangeFrame((o, e) => {
                UpdateFrame();
                var frame = e.FrameEnd.ToVector4D().ToRectangleF(); // ok this looks stupid
                var alignedBorders = new Vector4D<bool>(frame.X <= Location.X, frame.X + frame.Width >= Location.X + Size.X, frame.Y <= Location.Y, frame.Y + frame.Height >= Location.Y + Size.Y);
                var paddedBorders = new Vector4D<bool>(alignedBorders.V1 && alignedBorders.V3 && alignedBorders.V4, alignedBorders.V2 && alignedBorders.V3 && alignedBorders.V4, alignedBorders.V3 && alignedBorders.V1 && alignedBorders.V2, alignedBorders.V4 && alignedBorders.V1 && alignedBorders.V2);
                keyboardPadding.Left = (paddedBorders.V1 ? frame.X + frame.Width - Location.X : 0); // negative paddings may result
                keyboardPadding.Right = (paddedBorders.V2 ? Location.X + Size.X - frame.X : 0);
                keyboardPadding.Top = (paddedBorders.V3 ? frame.Y + frame.Height - Location.Y : 0);
                keyboardPadding.Bottom = (paddedBorders.V4 ? Location.Y + Size.Y - frame.Y : 0);
                view.UpdateLayout(); // updates in here will be animated
            });
            

            // This will force a layout to set the views correct initial size and location
            //((LayerLayout)view).Insert(actualView, true);
            nativeView.LayoutSubviews();

            var actualView = viewController.ConstructView();
            ((LayerLayout)view).Insert(actualView, true);
        }


        public Window(ViewController view)
            : this(view, Application.ThemeColor)
        {
        }

        public Window(ViewController view, Color themeColor)
            : this(AppInstall.UI.Screen.MainScreen, view, themeColor)
        {
        }


        /// <summary>
        /// Sets input focus to this window and brings it to the front
        /// </summary>
        public void Show()
        {
            nativeView.MakeKeyAndVisible();
        }

        ViewController modalViewController = null;

        /// <summary>
        /// Shows a view controller on top of all current views.
        /// </summary>
        public void ShowModalViewController(ViewController view)
        {
            if (modalViewController != null)
                throw new NotImplementedException("cannot show more than one dialog at once");
            modalViewController = view;

            WindowViewController dialog = new WindowViewController(this, view.ConstructView());
            controller.PresentViewController(dialog, true, null);
        }

        /// <summary>
        /// Dismisses a view controller previously shown by ShowModalViewController.
        /// </summary>
        /// <param name="view"></param>
        public void DismissModalViewController(ViewController view)
        {
            if (modalViewController != view)
                throw new InvalidOperationException("no such dialog displayed");
            controller.DismissViewController(true, null);
            modalViewController = null;
        }


        protected override Vector2D<float> GetContentSize(Vector2D<float> maxSize)
        {
            return view.GetMinSize(maxSize);
        }

        protected override void UpdateContentLayout()
        {
            view.UpdateLayout();
        }

        public void DumpLayout(LogContext logContext)
        {
            var str = new StringBuilder(256);
            view.DumpLayout(str, "");
            logContext.Log(str.ToString());
        }
    }
}