using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{

    /// <summary>
    /// Displays a list of items of the same kind.
    /// </summary>
    /// <typeparam name="T">the underlying data type of a single item</typeparam>
    public partial class ListViewController<T> : DetailViewController<T, CollectionSource<T>>
        where T : class
    {

        private ListView listView;



        public override IEnumerable<FeatureController> GetFeatures()
        {
            foreach (var feature in base.GetFeatures())
                yield return feature;

            if (Data.CanConstructItems) {
                yield return new StandardFeature() {
                    Type = StandardFeature.StandardCommandType.Add,
                    Text = AddText,
                    Action = () => ShowDetail(Data.AddNewItem(), true)
                };
            }

            if (Data.CanMoveItems || Data.CanDeleteItems) {
                yield return new StandardFeature() {
                    Type = StandardFeature.StandardCommandType.Edit,
                    AlternativeType = StandardFeature.StandardCommandType.Done,
                    Text = "[edit text]", // todo: customize
                    Action = () => { listView.SetEditingMode(true, true); nav.EnableAlternativeMode(true, true); },
                    AlternativeAction = () => { listView.SetEditingMode(false, true); nav.EnableAlternativeMode(false, true); }
                };
            }
        }


        protected override NavigationPage ConstructMainNavigationPage(NavigationView nav)
        {
            var listView = new ListView(false);

            var features = new FeatureList(GetFeatures());

            NavigationPage page = new NavigationPage() {
                Title = Title,
                View = listView
            };

            foreach (var s in ConstructMainListViewSections(nav, page, listView, features, false))
                listView.AddSection(s);

            AddFeatures(features, listView);
            AddFeatures(features, page);

            features.AssertEmpty();

            return page;
        }

        protected override IEnumerable<IListViewSection> ConstructMainListViewSections(NavigationView nav, NavigationPage page, ListView listView, FeatureList features, bool complete)
        {
            this.listView = listView;


            Platform.DefaultLog.Log("this = " + this.GetHashCode() + ", nav = " + (nav == null ? "null" : nav.GetHashCode().ToString()) + ", this.nav = " + (this.nav == null ? "null" : this.nav.GetHashCode().ToString()));
            Converter<T, ListViewItem> dataItemConstructor = (item) => Field<T>.ConstructListViewItem(item, DetailViewConstructor == null ? (Action<T>)null : ShowDetail, nav, Fields);


            if (complete) {
                // Display complete list in a single list view section, including header and special functions.


                ListViewSection<T> section = new ListViewSection<T>(Data.CanMoveItems, dataItemConstructor, AddFeatures(features));
                section.AddItems(Data);
                section.Header = Title;

                Data.DidAddItem += (item) => {
                    section.AddItem(item);
                };
                Data.DidRemoveItem += (item) => {
                    section.RemoveItems((i) => i == item);
                };
                Data.DidUpdateItem += (item) => {
                    section.UpdateItem(item);
                };
                section.DidRemoveDataItem += (obj, item) => {
                    Data.Remove(item);
                };

                return new ListViewSection<T>[] { section };

            } else {
                // Return multiple sections, depending on categories

                var categories = CategoryNameFactory == null ? new string[] { null } : Data.Select(GetCategory).Distinct();
                var sections = categories.Select((category) => {
                    ListViewSection<T> section = new ListViewSection<T>(Data.CanMoveItems, dataItemConstructor, null) {
                        Header = category
                    };
                    section.DidRemoveDataItem += (obj, i) => { Data.Remove(i); };
                    section.AddItems(Data.Where((item) => GetCategory(item) == category));
                    return section;
                }).ToList();


                // returns the section for the specified category (creates the section if neccessary)
                Func<T, ListViewSection<T>> getSection = (item) => {
                    var category = GetCategory(item);
                    var section = sections.FirstOrDefault((s) => s.Header == category);

                    if (section == null) {
                        section = new ListViewSection<T>(Data.CanMoveItems, dataItemConstructor, null) {
                            Header = category
                        };
                        section.DidRemoveDataItem += (obj, i) => { Data.Remove(i); };
                        listView.AddSection(section);
                        sections.Add(section);
                    }

                    return section;
                };

                // removes the section if empty
                Action<ListViewSection<T>> validateSection = (section) => {
                    if (!section.Any()) {
                        listView.RemoveSection(section, true);
                        sections.Remove(section);
                    }
                };

                Data.DidAddItem += (item) => {
                    getSection(item).AddItem(item);
                };

                Data.DidRemoveItem += (item) => {
                    var section = getSection(item);
                    section.RemoveItems((i) => i == item);
                    validateSection(section);
                };

                Data.DidUpdateItem += (item) => {
                    var oldSection = sections.First((s) => s.Contains(item));
                    var newSection = getSection(item);
                    if (newSection != oldSection) { // todo: make a nice move-animation instead of just removing and adding
                        oldSection.RemoveItems((i) => i == item);
                        validateSection(oldSection);
                        newSection.AddItem(item);
                    }
                    newSection.UpdateItem(item);
                };

                return sections;
            }

        }
    }
}
