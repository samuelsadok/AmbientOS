using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{
    public partial class MultiViewController : DataViewController<DataSource>
    {
        protected override View ConstructViewEx()
        {
            View result;

            switch (Modality) {
                case ViewModality.Compact:
                    var tabView = new TabView((from v in Subviews select new TabViewItem() { Header = v.Title, Content = v.ConstructView() }).ToArray());
                    result = tabView;
                    break;

                case ViewModality.Expanded:
                    string[] rows = (from r in Layout.Split(';') where !string.IsNullOrWhiteSpace(r) select r.Trim()).ToArray();
                    var fullGrid = new GridLayout(rows.Count(), 1);
                    fullGrid.RelativeColumnWidths[0] = 1f;
                    for (int r = 0; r < rows.Count(); r++) {
                        var fixedHeight = rows[r].First() == '[' && rows[r].Last() == ']';
                        if (fixedHeight) rows[r] = rows[r].Substring(1, rows[r].Length - 2);

                        string[] cells = (from c in rows[r].Split(' ') where !string.IsNullOrWhiteSpace(c) select c.Trim()).ToArray();
                        var rowGrid = new GridLayout(1, cells.Count());
                        rowGrid.RelativeRowHeights[0] = 1f;
                        fullGrid.RelativeRowHeights[r] = (fixedHeight ? 0 : 1);

                        for (int c = 0; c < cells.Count(); c++) {
                            var fixedWidth = cells[c].First() == '(' && cells[c].Last() == ')';
                            if (fixedWidth) cells[c] = cells[c].Substring(1, cells[c].Length - 2).Trim();

                            var cell = Subviews[int.Parse(cells[c])];
                            var view = cell.ConstructView();
                            rowGrid[0, c] = (cell.Title == null ? view : new GroupView() { Title = cell.Title, Content = view });
                            rowGrid.RelativeColumnWidths[c] = (fixedWidth ? 0 : 1);
                        }

                        fullGrid[r, 0] = rowGrid;
                    }

                    result = fullGrid;
                    break;

                default:
                    throw new NotSupportedException();
            }

            // add features
            var features = new FeatureList(GetFeatures());
            result = AddFeatures(result, features);
            features.AssertEmpty();
            return result;
        }
    }
}
