using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{
    public partial class TreeViewController<TTree, TItem> : DetailViewController<TItem, TreeSource<TTree, TItem>>
    {
        protected override View ConstructMainView()
        {
            var table = new TreeView<TTree, TItem>(Data, FolderFields, ItemFields);
            var features = new FeatureList(GetFeatures());
            var result = AddFeatures(table, features);
            features.AssertEmpty();
            return result;
        }
    }
}
