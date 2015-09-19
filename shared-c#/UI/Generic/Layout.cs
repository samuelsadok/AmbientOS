using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{

    /// <summary>
    /// Represents a layout that arranges subviews in a grid pattern.
    /// The dimensioning of the rows and columns is done automatically and can be configured by setting the relative or absolute constraints or both.
    /// </summary>
    public class GridLayout : ContainerView
    {

        private Matrix<View> views;

        public float[] AbsoluteColumnWidths { get; private set; }
        public float[] AbsoluteRowHeights { get; private set; }
        public float[] RelativeColumnWidths { get; private set; }
        public float[] RelativeRowHeights { get; private set; }

        public int Columns { get { return views.Columns; } }
        public int Rows { get { return views.Rows; } }

        public View this[int i, int j] { get { return views[i, j]; } set { views[i, j] = ReplaceSubview(views[i, j], value); } }

        public GridLayout(int rows, int columns)
        {
            AbsoluteColumnWidths = new float[columns];
            AbsoluteRowHeights = new float[rows];
            RelativeColumnWidths = new float[columns];
            RelativeRowHeights = new float[rows];
            views = new Matrix<View>(rows, columns);
        }

        public static Vector2D<float> RequiredSpace(Vector2D<float> size, View child)
        {
            var margin = child.Margin;
            return new Vector2D<float>(size.X + margin.Left + margin.Right, size.Y + margin.Top + margin.Bottom);
        }

        /// <summary>
        /// Finds the widths of all columns and the heights of all rows that is required to fit all elements while respecting the size constraints
        /// </summary>
        /// <param name="widths">outputs the width of each column</param>
        /// <param name="heights">outputs the height of each row</param>
        /// <param name="normalizedRelativeWidths">outputs RelativeColumnWidths normalized to a sum of 1 (or 0)</param>
        /// <param name="normalizedRelativeHeights">outputs RelativeRowHeights normalized to a sum of 1 (or 0)</param>
        /// <returns>the sum of all column widths and row heights</returns>
        private Vector2D<float> GetMinCellSizes(Vector2D<float> maxSize, out float[] widths, out float[] heights, out float[] normalizedRelativeWidths, out float[] normalizedRelativeHeights)
        {
            widths = new float[Columns];
            heights = new float[Rows];

            // preload minimum size for columns/rows
            for (int i = 0; i < Columns; i++)
                widths[i] = AbsoluteColumnWidths[i];
            for (int j = 0; j < Rows; j++)
                heights[j] = AbsoluteRowHeights[j];

            float absoluteWidthSum = AbsoluteColumnWidths.Sum();
            float absoluteHeightSum = AbsoluteRowHeights.Sum();
            float relativeWidthSum = RelativeColumnWidths.Sum();
            float relativeHeightSum = RelativeRowHeights.Sum();


            // calculate the share of the width/height that each column/row gets (normally only columns/rows with a relative constraint are considered)
            if (maxSize.X != float.MaxValue) maxSize.X -= absoluteWidthSum;
            if (maxSize.Y != float.MaxValue) maxSize.Y -= absoluteHeightSum;
            normalizedRelativeWidths = (relativeWidthSum == 0 ? (absoluteWidthSum == 0 ? Enumerable.Repeat(0f, Columns) : (from w in AbsoluteColumnWidths select w / absoluteWidthSum)) : (from w in RelativeColumnWidths select w / relativeWidthSum)).ToArray();
            normalizedRelativeHeights = (relativeHeightSum == 0 ? (absoluteHeightSum == 0 ? Enumerable.Repeat(0f, Rows) : (from h in AbsoluteRowHeights select h / absoluteHeightSum)) : (from h in RelativeRowHeights select h / relativeHeightSum)).ToArray();
            var availColumnWidths = (maxSize.X == float.MaxValue ? Enumerable.Repeat(float.MaxValue, Columns) : (from w in normalizedRelativeWidths select maxSize.X * (w == 0 ? 1 : w))).ToArray();
            var availColumnHeights = (maxSize.Y == float.MaxValue ? Enumerable.Repeat(float.MaxValue, Rows) : (from h in normalizedRelativeHeights select maxSize.Y * (h == 0 ? 1 : h))).ToArray();


            // expand columns/rows to fit the largest elements
            for (int j = 0; j < Columns; j++) {
                for (int i = 0; i < Rows; i++) {
                    if (views[i, j] != null) {
                        var space = RequiredSpace(views[i, j].GetMinSize(new Vector2D<float>(availColumnWidths[j], availColumnHeights[i])), views[i, j]);
                        widths[j] = Math.Max(widths[j], space.X);
                        heights[i] = Math.Max(heights[i], space.Y);
                    }
                }
            }


            // expand columns to respect relative width constraints
            if (relativeWidthSum != 0) {
                float effectiveRelativeWidth = 0;
                for (int i = 0; i < Columns; i++)
                    if (normalizedRelativeWidths[i] != 0)
                        effectiveRelativeWidth = Math.Max(effectiveRelativeWidth, widths[i] / normalizedRelativeWidths[i]);
                for (int i = 0; i < Columns; i++)
                    widths[i] = Math.Max(widths[i], effectiveRelativeWidth * normalizedRelativeWidths[i]);
            }


            // expand rows to respect relative height constraints
            if (relativeHeightSum != 0) {
                float effectiveRelativeHeight = 0;
                for (int j = 0; j < Rows; j++)
                    if (normalizedRelativeHeights[j] != 0)
                        effectiveRelativeHeight = Math.Max(effectiveRelativeHeight, heights[j] / normalizedRelativeHeights[j]);
                for (int j = 0; j < Rows; j++)
                    heights[j] = Math.Max(heights[j], effectiveRelativeHeight * normalizedRelativeHeights[j]);
            }


            // sum up column widths and row heights
            return new Vector2D<float>(widths.Sum(), heights.Sum());
        }

        protected override Vector2D<float> GetContentSize(Vector2D<float> maxSize)
        {
            float[] widths, heights, normalizedRelativeWidths, normalizedRelativeHeights;
            return GetMinCellSizes(maxSize, out widths, out heights, out normalizedRelativeWidths, out normalizedRelativeHeights);
        }

        protected override void UpdateContentLayout()
        {
            // distribute additional size between flexible cells
            float[] widths, heights, normalizedRelativeWidths, normalizedRelativeHeights;
            Vector2D<float> deltaSize = Size - GetMinCellSizes(Size, out widths, out heights, out normalizedRelativeWidths, out normalizedRelativeHeights);
            for (int i = 0; i < Columns; i++)
                widths[i] = Math.Max(0, widths[i] + deltaSize.X * normalizedRelativeWidths[i]);
            for (int j = 0; j < Rows; j++)
                heights[j] = Math.Max(0, heights[j] + deltaSize.Y * normalizedRelativeHeights[j]);

            // arrange and update all subviews
            float currentX = 0;
            for (int j = 0; j < Columns; j++) {
                float currentY = 0;
                for (int i = 0; i < Rows; i++) {
                    if (views[i, j] != null) {
                        var cellMargin = views[i, j].Margin;
                        SetLocation(views[i, j], new Vector2D<float>(currentX + cellMargin.Left, currentY + cellMargin.Top));
                        views[i, j].Size = new Vector2D<float>(widths[j] - cellMargin.Left - cellMargin.Right, heights[i] - cellMargin.Top - cellMargin.Bottom);
                        views[i, j].UpdateLayout();
                    }
                    currentY += heights[i];
                }
                currentX += widths[j];
            }
        }
        

        //public override bool IsOpaque()
        //{
        //    for (int j = 0; j < Columns; j++)
        //        for (int i = 0; i < Rows; i++)
        //            if (!(views[i, j] == null ? false : views[i, j].IsOpaque()))
        //                return false; // todo: respect padding & margin
        //    return true;
        //}

        public override void DumpLayout(StringBuilder dump, string indent, string tag = null)
        {
            base.DumpLayout(dump, indent, tag);
            indent += DUMP_INDENT_STEP;
            for (int i = 0; i < Rows; i++)
                for (int j = 0; j < Columns; j++)
                    this[i, j].DumpLayout(dump, indent, "r=" + i + ", c=" + j);
        }
    }


    /// <summary>
    /// Provides a layout view that holds up to five subviews: top, bottom and two side bars and a content view.
    /// The four bars are sized appropriately while the content fills the remaining space in the middle.
    /// The top and bottom bars have higher priority than the side bars.
    /// The paddings of all views are set to accommodate for the instances own padding.
    /// If auto padding is enabled, padding automatically pads for OS elements (i.e. the status bar on iOS)
    /// </summary>
    public class FramedLayout : ContainerView
    {
        private View mainContent;
        private Vector4D<View> bars = new Vector4D<View>();
        public View LeftSideBar { get { return bars[0]; } set { bars[0] = ReplaceSubview(bars[0], value); BringToFront(value); } }
        public View RightSideBar { get { return bars[1]; } set { bars[1] = ReplaceSubview(bars[1], value); BringToFront(value); } }
        public View TopBar { get { return bars[2]; } set { bars[2] = ReplaceSubview(bars[2], value); BringToFront(value); } }
        public View BottomBar { get { return bars[3]; } set { bars[3] = ReplaceSubview(bars[3], value); BringToFront(value); } }
        public View Content { get { return mainContent; } set { mainContent = ReplaceSubview(mainContent, value); SendToBack(value); } }
        //public bool AutoPadding { get; set; }

        public FramedLayout()
            : this(new Vector4D<bool>(false, false, true, true))
        {
        }

        public FramedLayout(Vector4D<bool> translucentBars)
        {
            //AutoPadding = autoPadding;
        }

        protected override Vector2D<float> GetContentSize(Vector2D<float> maxSize)
        {
            return new Vector2D<float>(); // todo: implement min content size
        }
        

        private Vector2D<float> AlignBar(Vector2D<float> maxSize, Margin transparentBars, Margin solidBars, int edge, int oppositeEdge, int orient, bool apply)
        {
            View bar = (edge == -1 ? mainContent : bars[edge]);
            if (bar == null) return new Vector2D<float>(0, 0);

            var barPadding = transparentBars - solidBars;

            if (oppositeEdge != -1) barPadding[oppositeEdge] = 0;
            var minSize = bar.GetMinSize(maxSize, barPadding);
            float dimension = (orient == -1 ? 0 : minSize[orient]);

            if (apply) {
                bar.Padding = barPadding;
                bar.Size = new Vector2D<float>((orient == 0 ? dimension : Size.X - solidBars.Left - solidBars.Right), (orient == 1 ? dimension : Size.Y - solidBars.Top - solidBars.Bottom)); // for top/bottom
                SetLocation(bar, new Vector2D<float>((oppositeEdge == 0 ? Size.X - solidBars.Right - dimension : solidBars.Left), (oppositeEdge == 2 ? Size.Y - solidBars.Bottom - dimension : solidBars.Top)));
                bar.UpdateLayout();
            }

            if (edge != -1) {
                transparentBars[edge] += dimension - barPadding[edge];
                if (bar.IsOpaque()) solidBars[edge] = transparentBars[edge];
            }

            return minSize;
        }

        protected override void UpdateContentLayout()
        {
            Margin transparentBars = Padding.Copy();
            Margin solidBars = new Margin();

            AlignBar(Size, transparentBars, solidBars, 2, 3, 1, true); // top bar
            AlignBar(Size, transparentBars, solidBars, 3, 2, 1, true); // bottom bar
            AlignBar(Size, transparentBars, solidBars, 0, 1, 0, true); // left side bar
            AlignBar(Size, transparentBars, solidBars, 1, 0, 0, true); // right side bar
            AlignBar(Size, transparentBars, solidBars, -1, -1, -1, true); // content
        }


        public override void DumpLayout(StringBuilder dump, string indent, string tag = null)
        {
            base.DumpLayout(dump, indent, tag);
            indent += DUMP_INDENT_STEP;
            if (LeftSideBar != null) LeftSideBar.DumpLayout(dump, indent, "left bar");
            if (RightSideBar != null) RightSideBar.DumpLayout(dump, indent, "right bar");
            if (TopBar != null) TopBar.DumpLayout(dump, indent, "top bar");
            if (BottomBar != null) BottomBar.DumpLayout(dump, indent, "bottom bar");
            if (Content != null) Content.DumpLayout(dump, indent, "content");
        }
    }

    /// <summary>
    /// Represents a view container that arranges views in multiple layers on top of each other.
    /// Views may be added and removed dynamically using different animation styles.
    /// </summary>
    public class LayerLayout : ContainerView
    {
        private LinkedList<View> layers = new LinkedList<View>(); // topmost views are first

        public LayerLayout()
            : base()
        {
        }

        protected override Vector2D<float> GetContentSize(Vector2D<float> maxSize)
        {
            return (from v in layers select v.GetMinSize(maxSize)).Max();
        }


        /// <summary>
        /// Inserts a view on top of this layer. No layout update is required.
        /// </summary>
        /// <param name="fadeIn">When true, the opacity is animated</param>
        /// <param name="origin">The relative geometric origin of the view for a translational animation: (-1, 0) would mean from the left.</param>
        /// <param name="duration">the duration of the animation</param>
        public void Insert(View view, bool fadeIn, Vector2D<float> origin = null, int duration = 500)
        {
            Replace(null, view, fadeIn, false, (origin == null ? null : -1 * origin), duration);
        }

        /// <summary>
        /// Removes a view from this layer. No layout update is required.
        /// </summary>
        /// <param name="fadeOut">When true, the opacity is animated</param>
        /// <param name="destination">The relative geometric destination of the view for a translational animation: (-1, 0) would mean to the left.</param>
        /// <param name="duration">the duration of the animation</param>
        public void Remove(View view, bool fadeOut, Vector2D<float> destination = null, int duration = 500)
        {
            Replace(view, null, false, fadeOut, destination, duration);
        }


        public void Replace(View newView)
        {
            Replace(newView, false, false, null, 0);
        }

        public void Replace(View newView, bool fadeIn, bool fadeOut, Vector2D<float> motionDirection = null, int duration = 500)
        {
            Replace((layers.Any() ? layers.First() : null), newView, fadeIn, fadeOut, motionDirection, duration);
        }

        /// <summary>
        /// Removes a layer and inserts a new layer using the specified animation.
        /// </summary>
        /// <param name="oldView">The layer to be removed. Can be null.</param>
        /// <param name="newView">The layer to be inserted. Can be null.</param>
        public void Replace(View oldView, View newView, bool fadeIn, bool fadeOut, Vector2D<float> motionDirection = null, int duration = 500)
        {
            if (oldView == newView)
                return;

            if (newView != null) {
                var v = layers.Find(newView);
                if (v != null)
                    layers.Remove(v);
                else
                    AddSubview(newView);
                BringToFront(newView);
                layers.AddFirst(newView);

                SetLocation(newView, motionDirection == null ? new Vector2D<float>(0, 0) : new Vector2D<float>(-Size.X * motionDirection.X, -Size.Y * motionDirection.Y));
                newView.Opacity = (fadeIn ? 0f : 1f);
                newView.Shadow = true;
            }

            UpdateVisibleViews(oldView);
            
            //Application.UILog.Log("brought to front at offset " + newView.Location + ", ");

            new Animation(() => {
                if (oldView != null) {
                    if (motionDirection != null) SetLocation(oldView, new Vector2D<float>(Size.X * motionDirection.X, Size.Y * motionDirection.Y));
                    if (fadeOut) oldView.Opacity = 0f;
                    oldView.UpdateLayout();
                }
                if (newView != null) {
                    SetLocation(newView, new Vector2D<float>(0, 0));
                    newView.Opacity = 1f;
                    newView.UpdateLayout();
                    Application.UILog.Log("made opaque at " + GetLocation(newView));
                }
            }, () => {
                if (oldView != null) {
                    RemoveSubview(oldView);
                    layers.Remove(oldView);
                }
            }).Execute(duration);
        }


        protected override void UpdateContentLayout()
        {
            UpdateVisibleViews(null);
        }

        /// <summary>
        /// Applies the current size and padding of the layer view to all visible layers.
        /// </summary>
        public void UpdateVisibleViews(View transparentView)
        {
            bool visible = true;

            foreach (var v in layers) {
                if (visible) {
                    Application.UILog.Log("view " + v + " visible");
                    v.Size = Size;
                    v.Padding = Padding.Copy();
                    v.UpdateLayout();
                    if (v.IsOpaque() && (GetLocation(v) == new Vector2D<float>(0, 0)) && (v != transparentView)) visible = false;
                }
            }
        }


        public override void DumpLayout(StringBuilder dump, string indent, string tag = null)
        {
            base.DumpLayout(dump, indent, tag);
            indent += DUMP_INDENT_STEP;
            foreach (var view in layers)
                view.DumpLayout(dump, indent);
        }
    }

}