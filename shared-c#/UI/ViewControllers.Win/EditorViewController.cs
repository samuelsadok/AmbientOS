using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{
    public partial class EditorViewController<T> : DataViewController<DataSource<T>>
    {
        protected override View ConstructViewEx()
        {
            var stretchedFields = Fields.Select((f) => f as LargeTextFieldView<T>).Where((f) => f != null).ToArray();
            var fixedFields = Fields.Except(stretchedFields).ToArray();

            GridLayout innerGrid = null;

            if (fixedFields.Any()) {
                innerGrid = new GridLayout(fixedFields.Count(), 2);
                innerGrid.RelativeColumnWidths[0] = 0f;
                innerGrid.RelativeColumnWidths[1] = 1f;

                for (int i = 0; i < fixedFields.Count(); i++) {
                    innerGrid.RelativeRowHeights[i] = 1f;
                    innerGrid[i, 0] = new Label() { Text = fixedFields[i].Header };
                    innerGrid[i, 1] = fixedFields[i].Constructor(Data.Data);
                }
            }

            var outerGrid = new GridLayout(stretchedFields.Count() + 3, 1);
            outerGrid.RelativeColumnWidths[0] = 1f;
            outerGrid.RelativeRowHeights[0] = outerGrid.RelativeRowHeights[stretchedFields.Count() + 2] = (stretchedFields.Any() ? 0f : 1f);
            outerGrid.RelativeRowHeights[1] = 0f;
            outerGrid[1, 0] = innerGrid;
            for (int i = 0; i < stretchedFields.Count(); i++) {
                outerGrid.RelativeRowHeights[i + 2] = 1f;
                outerGrid[i + 2, 0] = stretchedFields[i].Constructor(Data.Data);
            }


            var features = new FeatureList(GetFeatures());
            var result = AddFeatures(outerGrid, features);
            features.AssertEmpty();
            return result;
        }
    }
}
