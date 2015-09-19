using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppInstall.UI
{
    /// <typeparam name="F">folder type</typeparam>
    /// <typeparam name="I">item type</typeparam>
    public abstract class NavigationList<F, I> : DialogView<IEnumerable<I>, List<I>>
    {
        List<I> checkedItems;

        protected override List<I> Result { get { return checkedItems; } }
        protected override NavigationPage MainPage { get { return ToNavigationItem(RootFolder); } }

        public abstract F RootFolder { get; }
        public abstract string GetFolderName(F folder);
        public abstract IEnumerable<F> GetSubfolders(F folder);
        public abstract IEnumerable<I> GetItems(F folder);
        public abstract IEnumerable<NavigationPage> GetAdditionalOptions(F folder);

        

        /// <summary>
        /// Shows the navigation page with the specified items being checked and returns a new list of checked items after the dialog was dismissed.
        /// This call blocks until the dialog is dismissed.
        /// </summary>
        protected override void Setup(IEnumerable<I> checkedItems)
        {
            this.checkedItems = checkedItems.ToList();
        }


        private NavigationPage ToNavigationItem(F folder)
        {
            ListView l = new ListView();

            // subfolder section
            ListViewSection<F> subfolderSection = new ListViewSection<F>(false, (f) => {
                var i = new ListViewItem(true, false, false) { Text = GetFolderName(f), IsSelectable = true };
                i.Selected += (o, e) => Parent.NavigateForward(ToNavigationItem(f), true, true);
                return i;
            });
            subfolderSection.AddItems(GetSubfolders(folder));
            if (subfolderSection.Any()) l.AddSection(subfolderSection);

            // item section
            ListViewSection<Tuple<I, CheckListViewItem>> itemSection = new ListViewSection<Tuple<I, CheckListViewItem>>(false, (i) => i.Item2);
            itemSection.AddItems(GetItems(folder).Select((i) => {
                var checkItem = new CheckListViewItem(false) { Text = i.ToString(), IsChecked = checkedItems.Contains(i) };
                checkItem.CheckedChanged += (o, e) => {
                    if (e) checkedItems.Add(i);
                    else checkedItems.Remove(i);
                };
                return new Tuple<I, CheckListViewItem>(i, checkItem);
            }));
            if (itemSection.Any()) l.AddSection(itemSection);

            // additional optinos section
            ListViewSection<NavigationPage> additionalSection = new ListViewSection<NavigationPage>(false, (p) => {
                var i = new ListViewItem(true, false, false) { Text = p.Title, IsSelectable = true };
                i.Selected += (o, e) => Parent.NavigateForward(p, true, true);
                return i;
            });
            additionalSection.AddItems(GetAdditionalOptions(folder));
            if (additionalSection.Any()) l.AddSection(additionalSection);

            return new NavigationPage() {
                View = l,
                Title = GetFolderName(folder),
                NavigationBarItems = new IToolbarItem[] { GetDoneButton() },
            };
        }
    }
}