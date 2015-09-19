using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppInstall.UI
{
    public class SplitView : View<System.Windows.Controls.Grid>
    {
        private View leftView, rightView;

        public View LeftView
        {
            get { return leftView; }
            set
            {
                if (leftView != null)
                    nativeView.Children.Remove(leftView.NativeView);
                nativeView.Children.Add((leftView = value).ToNativeView());
                System.Windows.Controls.Grid.SetColumn(leftView.NativeView, 0);
            }
        }

        public View RightView
        {
            get { return rightView; }
            set
            {
                if (rightView != null)
                    nativeView.Children.Remove(rightView.NativeView);
                nativeView.Children.Add((rightView = value).ToNativeView());
                System.Windows.Controls.Grid.SetColumn(rightView.NativeView, 2);
            }
        }

        public SplitView()
        {
            var splitter = new System.Windows.Controls.GridSplitter() {
                ResizeDirection = System.Windows.Controls.GridResizeDirection.Columns,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                ResizeBehavior = System.Windows.Controls.GridResizeBehavior.PreviousAndNext
            };
            nativeView.Children.Add(splitter);
            nativeView.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition() { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            nativeView.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition() { Width = new System.Windows.GridLength(5, System.Windows.GridUnitType.Pixel) });
            nativeView.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition() { Width = new System.Windows.GridLength(2, System.Windows.GridUnitType.Star) });
            System.Windows.Controls.Grid.SetColumn(splitter, 1);
        }
    }
}
