using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    public class PlotView : Canvas
    {
        private LinkedList<Tuple<float[], Color>> plots = new LinkedList<Tuple<float[], Color>>();

        public PlotView()
            : base()
        {

        }

        public void Plot(float[] data, Color color)
        {
            plots.AddLast(new Tuple<float[], Color>(data, color));
        }

        protected override void UpdateContentLayout()
        {
            // draw each plot
            base.Clear();
            foreach (var p in plots) {
                Path2D plot = new Path2D();
                float minVal = p.Item1.Min(), span = p.Item1.Max() - minVal;
                Func<float, float> transformX = (val) => val / (p.Item1.Count() - 1) * (Size.X - Padding.Top - Padding.Bottom) + Padding.Top;
                Func<float, float> transformY = (val) => (val - minVal) / span * (Size.Y - Padding.Left - Padding.Right) + Padding.Left;
                plot.MoveToPoint(0, transformY(p.Item1[0]));
                for (int i = 1; i < p.Item1.Count(); i++)
                    plot.AddLine(transformX(i), transformY(p.Item1[i]));
                base.AddPath(plot, Color.Clear, p.Item2, 1f);
            }
        }

        public new void Clear()
        {
            plots.Clear();
            base.Clear();
        }
    }
}
