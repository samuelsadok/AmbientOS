using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{
    public enum ViewModality
    {
        /// <summary>
        /// User interaction is required to switch between the subviews.
        /// </summary>
        Compact,

        /// <summary>
        /// All subviews are visible at once.
        /// </summary>
        Expanded
    }

    /// <summary>
    /// Represents a collection of multiple subviews, displaying different kinds of content.
    /// In some scenarios the subviews are organized as tabs or in a grid, while in other scenarios they may be
    /// displayed stacked inside a scroll view.
    /// </summary>
    public partial class MultiViewController : DataViewController<DataSource>
    {
        /// <summary>
        /// A list of all views that should be displayed by this view controller
        /// </summary>
        public ViewController[] Subviews { get; set; }

        /// <summary>
        /// Specifies the way how the subviews are organized
        /// Mobile:
        /// Compact: TabView / Items in a ListView, Expanded: Sections in a ListView
        /// Desktop:
        /// Compact: TabView, Expanded: GroupViews
        /// </summary>
        public ViewModality Modality { get; set; }

        /// <summary>
        /// Specifies the arrangement of the subviews when displayed in groupviews.
        /// The Layout string has the format: "[0 1 (2)]; 3; 4;", where the numbers refer to
        /// the indices of the subviews, the spaces are column delimiters and the
        /// semicolons are row delimiters. Use "[]" and "()" to specify rows or columns that
        /// should take on fixed dimensions.
        /// </summary>
        public string Layout { get; set; }
    }
}
