using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using CoreGraphics;
using CoreAnimation;
using AppInstall.Framework;
using AppInstall.UI;

namespace AppInstall.Graphics
{
    public partial class Graphics2D
    {
        
    }
    

    public abstract partial class Graphics2D
    {
        public Vector2D<float> Scale = new Vector2D<float>(1, 1);
        public Vector2D<float> Location = new Vector2D<float>(0, 0);

        /// <summary>
        /// Shall return the native size of the graphical object.
        /// This is the amount of space that will be used when Scale is set to 1.
        /// </summary>
        public abstract Vector2D<float> GetSize();

        /// <summary>
        /// Shall draw this object to the provided context.
        /// The context is already set up according to the scale and translation properties.
        /// </summary>
        public abstract void DrawEx(CGContext context);

        /// <summary>
        /// Shall draw the current 
        /// </summary>
        /// <param name="context">The context on which to draw the object. This scales the object appropriately according to the scale</param>
        public void Draw(CGContext context) {
            context.SaveState();
            context.TranslateCTM(Location.X, Location.Y);
            context.ScaleCTM(Scale.X, Scale.Y);
            DrawEx(context);
            context.RestoreState();
        }
    }

    public partial class PathGraphics2D : Graphics2D
    {
        public Vector2D<float> PathScale = new Vector2D<float>(1f, 1f);

        public Path2D Path { get; set; }
        public Color Stroke { get; set; }
        public Color Fill { get; set; }
        public float StrokeWidth { get; set; }

        public override Vector2D<float> GetSize()
        {
            return new Vector2D<float>(1f, 1f); // todo: return bounding size of path
        }

        public override void DrawEx(CGContext context)
        {
            context.SaveState();
            context.ScaleCTM(PathScale.X, PathScale.Y);
            context.AddPath(Path.NativePath);
            context.RestoreState();

            context.SetFillColor(Fill.ToCGColor());
            context.SetStrokeColor(Stroke.ToCGColor());
            context.SetLineWidth(StrokeWidth);

            bool fill = Fill != Color.Clear;
            bool stroke = Stroke != Color.Clear;

            if (fill || stroke)
                context.DrawPath(fill ? (stroke ? CGPathDrawingMode.FillStroke : CGPathDrawingMode.Fill) : CGPathDrawingMode.Stroke);
        }
    }

    public partial class GraphicsCollection : Graphics2D
    {
        LinkedList<Graphics2D> graphics = new LinkedList<Graphics2D>();

        public override Vector2D<float> GetSize()
        {
            var bounds = graphics.Select(g => {
                var loc = g.Location;
                var scale = g.Scale;
                var size = g.GetSize();
                return new { MinX = loc.X, MinY = loc.Y, MaxX = loc.X + scale.X * size.X, MaxY = loc.Y + scale.Y * size.Y };
            }).Aggregate(
                (val1, val2) => new { MinX = Math.Min(val1.MinX, val2.MinX), MinY = Math.Min(val1.MinY, val2.MinY), MaxX = Math.Max(val1.MaxX, val2.MaxX), MaxY = Math.Max(val1.MaxY, val2.MaxY) }
            );
            return new Vector2D<float>(bounds.MaxX, bounds.MaxY);
        }

        public void AddGraphic(Graphics2D graphic)
        {
            graphics.AddLast(graphic);
        }

        public override void DrawEx(CGContext context)
        {
            // todo: draw selectively based on layers
            foreach (var graphic in graphics)
                graphic.Draw(context);
        }
    }


    public partial class PDFGraphics : Graphics2D
    {

        public Color Background { get; set; }

        public PDFGraphics()
        {
            // todo: load from source URL

            var doc = CGPDFDocument.FromFile("./test.pdf");
            page = doc.GetPage(1);
        }


        CGPDFPage page;

        public override Vector2D<float> GetSize()
        {
            return page.GetBoxRect(CGPDFBox.Crop).Size.ToVector2D();
        }



        public override void DrawEx(CGContext context)
        {
            var size = GetSize();

            if (Background != Color.Clear) {
                context.SetFillColor(Background.ToCGColor());
                context.FillRect(new CGRect(CGPoint.Empty, size.ToCGSize()));
            }

            context.SaveState();
            context.ScaleCTM(1, -1);
            context.TranslateCTM(0, -size.Y);
            context.DrawPDFPage(page);
            context.RestoreState();
        }
    }


}
