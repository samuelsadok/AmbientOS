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
            switch (Modality) {
                case ViewModality.Compact:
                    if (Features.Any())
                        throw new NotSupportedException("a tab view cannot display any commands");

                    var tabView = new TabView((from v in Subviews select new TabPage() {
                        Label = v.Title,
                        Icon = v.Icon,
                        PageConstructor = v.ConstructNavigationPage
                    }).ToArray());
                    return tabView;

                case ViewModality.Expanded:
                    return base.ConstructViewEx();

                default:
                    throw new NotSupportedException();
            }
        }


        protected override IEnumerable<IListViewSection> ConstructListViewSectionsEx(NavigationView nav, NavigationPage page, ListView listView, FeatureList features, bool complete)
        {
            if (complete && Features.Any()) // todo: implement
                throw new NotImplementedException("todo: append commands");

            switch (Modality) {
                case ViewModality.Compact:
                    ListViewSection s = new ListViewSection(false);
                    if (complete)
                        s.Header = this.Title;
                    s.AddItems(Subviews.Select((v) => v.ConstructListViewItem(nav)));
                    return new IListViewSection[] { s };

                case ViewModality.Expanded:
                    var sections = new List<IListViewSection>(Subviews.Count());
                    foreach (var subview in Subviews) {
                        var subFeatures = new FeatureList(subview.GetFeatures());
                        sections.AddRange(subview.ConstructListViewSections(nav, page, listView, subFeatures));
                        features.Features = features.Features.Concat(subFeatures.Features);
                    }
                    return sections;

                default:
                    throw new NotSupportedException();
            }
        }
    }
}
