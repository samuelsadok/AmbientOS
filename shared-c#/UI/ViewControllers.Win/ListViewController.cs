using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{
    public partial class ListViewController<T> : DetailViewController<T, CollectionSource<T>>
        where T : class
    {
        TableView<T> table;

        public override IEnumerable<FeatureController> GetFeatures()
        {
            if (Data.CanConstructItems)
                yield return new CustomFeature() { Text = AddText + "...", Action = () => table.SelectItem(Data.AddNewItem()) };

            foreach (var feature in base.GetFeatures())
                yield return feature;
        }


        protected override View ConstructMainView()
        {
            table = new TableView<T>(Data, Fields, CategoryNameFactory);

            if (Features == null) Features = new List<FeatureController>();
            var builtInCommands = new List<FeatureController>();

            if (Data.CanConstructItems)

            if (DetailViewConstructor != null)
                table.ItemSelected += (i) => ShowDetail(i);

            var features = new FeatureList(GetFeatures());
            var result = AddFeatures(table, features);
            features.AssertEmpty();
            return result;
        }
    }
}
