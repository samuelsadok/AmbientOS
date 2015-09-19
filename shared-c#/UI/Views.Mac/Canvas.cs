using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using Foundation;
using UIKit;
using CoreGraphics;
using CoreAnimation;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    /// <summary>
    /// A canvas where paths can be drawn on.
    /// </summary>
    public class Canvas : View<UIView>
    {
        private LinkedList<CALayer> layers = new LinkedList<CALayer>();
        private Vector4D<float> boundingBox = new Vector4D<float>();

        public bool PreserveAspectRatio { get; set; }

        public void AddPath(Path2D path, Color fillColor, Color strokeColor, float lineWidth)
        {
            CAShapeLayer layer = new CAShapeLayer(); // todo: respect padding
            layer.Path = path.NativePath;
            layer.FillColor = fillColor.ToCGColor();
            layer.StrokeColor = strokeColor.ToCGColor();
            layer.LineWidth = lineWidth;
            layers.AddLast(layer);
            this.nativeView.Layer.AddSublayer(layer);

            Vector4D<float> b = path.NativePath.BoundingBox.ToVector4D();
            if (b.V1 < boundingBox.V1) boundingBox.V1 = b.V1;
            if (b.V2 < boundingBox.V2) boundingBox.V2 = b.V2;
            boundingBox.V3 = Math.Max(boundingBox.V3, b.V1 + b.V3 - boundingBox.V1);
            boundingBox.V4 = Math.Max(boundingBox.V4, b.V2 + b.V4 - boundingBox.V2);
        }

        public void Clear()
        {
            foreach (var layer in layers)
                layer.RemoveFromSuperLayer();
            layers.Clear();
        }

        protected override Vector2D<float> GetContentSize(Vector2D<float> maxSize)
        {
            var nativeSize = new Vector2D<float>(boundingBox.V3, boundingBox.V4);
            float scaling = Math.Min((maxSize.X == float.MaxValue ? 1 : maxSize.X / nativeSize.X), (maxSize.Y == float.MaxValue ? 1 : maxSize.Y / nativeSize.Y));
            return (scaling < 1 ? scaling : 1) * nativeSize;
        }

        protected override void UpdateContentLayout()
        {
            var nativeSize = new Vector2D<float>(boundingBox.V3, boundingBox.V4);

            float scaleX = Size.X / nativeSize.X, scaleY = Size.Y / nativeSize.Y;
            if (PreserveAspectRatio) scaleX = scaleY = Math.Min(scaleX, scaleY);

            CATransform3D translate = CATransform3D.MakeTranslation(-boundingBox.V1, -boundingBox.V2, 0);
            CATransform3D scale = CATransform3D.MakeScale(scaleX, scaleY, 1);
            CATransform3D transform = translate.Concat(scale);
            foreach (var l in layers)
                l.Transform = transform;
        }
    }



    public class TouchSensitiveView : UIView
    {
        public UIView touchChild;

        private UIEvent currE = null;

        public override bool PointInside(CGPoint point, UIEvent uievent)
        {
            Application.UILog.Log("queried event" + uievent);
            return currE != uievent;
        }

        public override void TouchesBegan(NSSet touches, UIEvent evt)
        {
            currE = evt;
            Application.UILog.Log("touches began " + evt);
            base.TouchesBegan(touches, evt);
            touchChild.TouchesBegan(touches, evt);
        }

        public override void TouchesEnded(NSSet touches, UIEvent evt)
        {
            currE = null;
            Application.UILog.Log("touches ended");
            base.TouchesEnded(touches, evt);
            touchChild.TouchesEnded(touches, evt);
        }

        public override void TouchesCancelled(NSSet touches, UIEvent evt)
        {
            currE = null;
            Application.UILog.Log("touches cancelled");
            base.TouchesCancelled(touches, evt);
            touchChild.TouchesCancelled(touches, evt);
        }

        public override void TouchesMoved(NSSet touches, UIEvent evt)
        {
            Application.UILog.Log("touches moved");
            base.TouchesMoved(touches, evt);
            touchChild.TouchesMoved(touches, evt);
        }
    }


    public class InteractiveCanvas : View<UIView>
    {
        UIWebView webView = new UIWebView();
        TouchSensitiveView touchView = new TouchSensitiveView();


        private string source = "";
        public string Source
        {
            get
            {
                return source;
            }
            set
            {
                Application.UILog.Log("have app path " + ApplicationControl.AppDataPath);
                Application.UILog.Log("have cache path " + ApplicationControl.CachePath);
                //nativeView.LoadRequest(NSUrlRequest.FromUrl(NSUrl.FromFilename(ApplicationControl.AppDataPath + "\\" + value)));
                var path = "./" + value;
                Application.UILog.Log("load " + path + ": " + (System.IO.File.Exists(path) ? "exists" : "doesn't exist"));
                webView.LoadRequest(NSUrlRequest.FromUrl(NSUrl.FromFilename(path)));
                source = value;
            }
        }


        private class del1 : UIGestureRecognizerDelegate
        {
            public override bool ShouldReceiveTouch(UIGestureRecognizer recognizer, UITouch touch)
            {
                Application.UILog.Log("i was asked");
                return base.ShouldReceiveTouch(recognizer, touch);
            }
        }



        private class ScrollViewDelegate : UIScrollViewDelegate
        {
            nfloat scale = 1f;

            public CALayer templateLayer { get; set; }
            public CALayer dynamicLayer { get; set; }
            public override void DidZoom(UIScrollView scrollView)
            {
                if (dynamicLayer != null) {
                    scale = scrollView.ZoomScale;
                    //dynamicLayer.Transform = CATransform3D.MakeScale(scrollView.ZoomScale, scrollView.ZoomScale, scrollView.ZoomScale);
                    //dynamicLayer.LayoutSublayers();
                }
            }

            public override void ZoomingEnded(UIScrollView scrollView, UIView withView, nfloat atScale)
            {
                scale = atScale;
                if (dynamicLayer != null) {
                    Application.UILog.Log("zoom ended: " + scrollView.ZoomScale + ", " + scale + ", " + templateLayer.ContentsScale);

                    foreach (var l in scrollView.Layer.Sublayers.Concat(templateLayer.Sublayers).Concat(new CALayer[] { templateLayer, scrollView.Layer }))
                        Application.UILog.Log("layer " + l + ", " + l.ContentsScale);


                    dynamicLayer.Transform = CATransform3D.MakeScale(scale, scale, scale);
                    dynamicLayer.LayoutSublayers();
                }
            }

            
        }



        public InteractiveCanvas()
            : base(true)
        {
            webView.ScalesPageToFit = true;

            nativeView.AddSubview(webView);
            //nativeView.AddSubview(touchView);

            touchView.touchChild = webView;
            
            var rec = new UIPanGestureRecognizer(r => {
                Application.UILog.Log("pan: " + r.State + ", " + r.TranslationInView(webView));
            }) { MaximumNumberOfTouches = 1, Delegate = new UIGestureRecognizerDelegate() };
            webView.ScrollView.AddGestureRecognizer(rec);







            var path = new Path2D();
            path.AddLine(100, 100);

            CAShapeLayer layer = new CAShapeLayer(); // todo: respect padding
            layer.Path = path.NativePath;
            layer.FillColor = Color.Clear.ToCGColor();
            layer.StrokeColor = Color.Blue.ToCGColor();
            layer.LineWidth = 10f;
            layer.ContentsScale = 0.2f;
            //this.webView.ScrollView.DidZoom += (o, e) => {
            //    Application.UILog.Log("zoom changed");
            //    //layer.ContentsScale = webView.ScrollView.ZoomScale;
            //};



            var daLayer = webView.ScrollView.Layer.Sublayers.Single(l => l.ToString().Contains("UIWebLayer"));

            //var a = new UIWebLayer();
            foreach (var l in webView.ScrollView.Layer.Sublayers)
                Application.UILog.Log("layer: " + l.GetType());

            daLayer.AddSublayer(layer);


            //webView.ScrollView.Layer.AddSublayer(layer);
            var del = new ScrollViewDelegate() { dynamicLayer = layer, templateLayer = webView.Layer };
            webView.ScrollView.Delegate = del;
            del.DidZoom(webView.ScrollView);

            webView.Layer.BorderColor = Color.Green.ToCGColor();
            
            //webView.ScrollView.Layer.Sublayers.Where(l => l is layer BorderWidth = 3f;

            //Application.UILog.Log("changed now: " + webView.ScrollView.ZoomScale);




        }

        protected override void UpdateContentLayout()
        {
            // see also ListView implementation

            touchView.Frame = new CGRect(0, 0, nativeView.Frame.Width, nativeView.Frame.Height);
            webView.Frame = new CGRect(0, 0, nativeView.Frame.Width, nativeView.Frame.Height);
            webView.LayoutSubviews();
            

            var oldInset = webView.ScrollView.ContentInset.Top;
            var oldOffset = webView.ScrollView.ContentOffset.Y;

            webView.ScrollView.ScrollIndicatorInsets = new UIEdgeInsets(Padding.Top, Padding.Left, Padding.Bottom, Padding.Right);
            webView.ScrollView.ContentInset = new UIEdgeInsets(Padding.Top, Padding.Left, Padding.Bottom, Padding.Right);

            //// make sure that the scroll position stays the same when padding changes
            //if (!nativeView.Dragging)
            //    nativeView.ContentOffset = new PointF(nativeView.ContentOffset.X, oldOffset + oldInset - nativeView.ContentInset.Top);

            base.UpdateContentLayout();
        }

    }



    public class GraphicLayer : CALayer
    {
        public Graphics2D Graphics { get; set; }

        public Vector2D<float> GetSize()
        {
            return Graphics.GetSize();
        }


        private class LayerDelegate : CALayerDelegate
        {
            public GraphicLayer parent;

            public override void DrawLayer(CALayer layer, CGContext context)
            {
                var bounds = layer.Bounds;
                var nativeSize = parent.GetSize();
                var scale = new CGSize(bounds.Width / nativeSize.X, bounds.Height / nativeSize.Y);


                Application.UILog.Log("fill layer " + bounds);

                var g = parent.Graphics;
                if (g != null)
                    g.Draw(context);

                /* context.SetFillColor(Color.White.ToCGColor());
                context.FillRect(bounds);


                context.SaveState();
                context.ScaleCTM(1, -1);
                context.TranslateCTM(0, -nativeSize.Height);
                context.DrawPDFPage(parent.page);
                context.RestoreState();

                context.BeginPath();
                context.AddRect(new RectangleF(100f, 100f, 10f, 10f));
                context.ScaleCTM(2, 2);
                context.AddRect(new RectangleF(90f, 90f, 10f, 10f));
                context.AddLineToPoint(0, 0);
                context.ScaleCTM(1, 1);
                context.SetStrokeColor(Color.Black.ToCGColor());
                context.DrawPath(CGPathDrawingMode.Stroke);

                
                //context.RestoreState(); */
            }
        }


        public GraphicLayer()
        {
            var g = new GraphicsCollection();
            g.AddGraphic(new PDFGraphics() { Background = Color.White });
            Graphics = g;
            

            //Bounds = new RectangleF(PointF.Empty, GetSize());

            this.Delegate = new LayerDelegate() { parent = this };
            this.BorderColor = Color.Red.ToCGColor();
            this.BorderWidth = 10f;
            this.CornerRadius = 120f;

            this.ShouldRasterize = false;
            //this.ContentsScale = 2f;
            //this.SetNeedsDisplay();
        }
        
        /*
        public override void DrawInContext(CGContext ctx)
        {

            //base.DrawInContext(ctx);

            var actualSize = this.Bounds.Size;
            var nativeSize = GetSize();
            var scale = new SizeF(actualSize.Width / nativeSize.Width, actualSize.Height / nativeSize.Height);



            Color.White.ToUIColor().SetColor();
            ctx.FillRect(this.Bounds);

            ctx.SaveState();

            ctx.TranslateCTM(0, actualSize.Height);

            Application.UILog.Log("pdf scale: " + scale + ", size " + actualSize);
            //ctx.ScaleCTM(1f, -1f);
            ctx.ScaleCTM(scale.Width, -scale.Height);
            ctx.TranslateCTM(-nativeSize.Width, nativeSize.Height);
            ctx.DrawPDFPage(page);

            ctx.RestoreState();

        }*/
    }



    public class NewCanvas : View<UIScrollView>
    {

        public string Source = "test.pdf";


        public class ScrollViewDelegate : UIScrollViewDelegate
        {
            public UIView dynamicView { get; set; }
            public NewCanvas parent { get; set; }

            public override UIView ViewForZoomingInScrollView(UIScrollView scrollView)
            {
                return dynamicView;
            }

            public override void ZoomingEnded(UIScrollView scrollView, UIView withView, nfloat atScale)
            {
                /*Application.UILog.Log("scroll scale " + scrollView.Window.ContentScaleFactor + ", " + scrollView.Window.Screen.Scale + ", " +  + " content scale factor " + dynamicView.ContentScaleFactor + ", layer scale " + dynamicView.Layer.ContentsScale + " raster " + dynamicView.Layer.RasterizationScale);

                

                dynamicView.Layer.ContentsScale = 2f * atScale;
                dynamicView.Layer.SetNeedsDisplay();*/

                parent.ApplyContentScale(withView, atScale);
            }
        }


        private void ApplyContentScale(UIView view, nfloat zoomScale)
        {
            nfloat windowScale = 1f;
            if (view.Window != null)
                if (view.Window.Screen != null)
                    windowScale = view.Window.Screen.NativeScale;
            var scale = windowScale * zoomScale;

            foreach (var l in view.Layer.Sublayers) {
                l.ContentsScale = scale;
                l.SetNeedsDisplay();
            }

            var nativeSize = graphicsLayer.GetSize();
            nativeView.ContentSize = ((float)nativeView.ZoomScale * nativeSize).ToCGSize();
            nativeView.SetNeedsDisplay();
        }


        UIView graphicsView;
        GraphicLayer graphicsLayer;

        public NewCanvas()
            : base(true)
        {
            nativeView.BackgroundColor = Color.Grey.ToUIColor();




            graphicsView = new UIView();

            graphicsLayer = new GraphicLayer();
            graphicsView.Layer.AddSublayer(graphicsLayer);

            



            var del = new ScrollViewDelegate() { parent = this, dynamicView = graphicsView };

            /* nativeView.MinimumZoomScale = 0.5f;
            nativeView.MaximumZoomScale = 2f; // todo: set these on layout */
            nativeView.Delegate = del;


            nativeView.ContentSize = graphicsView.Frame.Size;
            //graphicsView.Frame = new RectangleF(PointF.Empty, graphicsView.Frame.Size);
            nativeView.AddSubview(graphicsView);

            //del.ZoomingEnded(nativeView, v, nativeView.ZoomScale);


        }



        protected override void UpdateContentLayout()
        {
            // see also ListView implementation

            nativeView.Frame = new CGRect(0, 0, nativeView.Frame.Width, nativeView.Frame.Height);


            var oldInset = nativeView.ContentInset.Top;
            var oldOffset = nativeView.ContentOffset.Y;

            nativeView.ScrollIndicatorInsets = new UIEdgeInsets(Padding.Top, Padding.Left, Padding.Bottom, Padding.Right);
            nativeView.ContentInset = new UIEdgeInsets(Padding.Top, Padding.Left, Padding.Bottom, Padding.Right);


            // these updates are actually related to graphic updates
            var nativeSize = graphicsLayer.GetSize();
            Application.UILog.Log("nativesize is now: " + nativeSize);
            graphicsView.Frame = graphicsLayer.Frame = new CGRect(CGPoint.Empty, nativeSize.ToCGSize());
            nativeView.MaximumZoomScale = 4f * (nativeView.MinimumZoomScale = Math.Min((Size.X - Padding.Left - Padding.Right) / nativeSize.X, (Size.Y - Padding.Top - Padding.Bottom) / nativeSize.Y));
            ApplyContentScale(graphicsView, nativeView.ZoomScale);
            graphicsLayer.SetNeedsDisplay();

            //// make sure that the scroll position stays the same when padding changes
            //if (!nativeView.Dragging)
            //    nativeView.ContentOffset = new PointF(nativeView.ContentOffset.X, oldOffset + oldInset - nativeView.ContentInset.Top);

            nativeView.LayoutSubviews();
            base.UpdateContentLayout();
        }
    }
}