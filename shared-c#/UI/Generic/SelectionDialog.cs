using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppInstall.Framework;

namespace AppInstall.UI
{
    class SingleSelectionDialog<T> : DialogView<int, int>
    {
        NavigationPage page;
        Converter<T, CheckListViewItem> converter;
        Tuple<T, CheckListViewItem>[] items;

        private int selection = -1;
        public int Selection
        {
            get { return selection; }
            set
            {
                if (selection != -1) items[selection].Item2.IsChecked = false;
                selection = value;
                if (selection != -1) items[selection].Item2.IsChecked = true;
            }
        }

        protected override NavigationPage MainPage { get { return page; } }
        protected override int Result { get { return Selection; } }

        public SingleSelectionDialog(string title, T[] options, Converter<T, CheckListViewItem> converter)
        {
            page = new NavigationPage() {
                Title = title,
                NavigationBarItems = new IToolbarItem[] { GetDoneButton() }
            };

            items = new Tuple<T, CheckListViewItem>[options.Count()];
            for (int i = 0; i < options.Count(); i++) {
                var listViewItem = converter(options[i]);
                var index = i;
                listViewItem.Selected += (o, e) => Selection = index;
                items[i] = new Tuple<T, CheckListViewItem>(options[i], listViewItem);
            }

            this.converter = converter;
        }


        protected override void Setup(int args)
        {
            Selection = args;

            ListViewSection list = new ListViewSection(false);
            foreach (var i in items)
                list.AddItem(i.Item2);

            ListView listView = new ListView();
            listView.AddSection(0, list);
            page.View = listView;
        }
    }


    class MultiSelectionDialog<T> : DialogView<IEnumerable<T>, IEnumerable<T>>
    {
        NavigationPage page;
        Converter<T, CheckListViewItem> converter;
        IEnumerable<Tuple<T, CheckListViewItem>> items;

        public MultiSelectionDialog(string title, Converter<T, CheckListViewItem> converter)
        {
            page = new NavigationPage() {
                Title = title,
                NavigationBarItems = new IToolbarItem[] { GetDoneButton() }
            };

            this.converter = converter;
        }

        protected override NavigationPage MainPage { get { return page; } }
        protected override IEnumerable<T> Result { get { return (from i in items where i.Item2.IsChecked select i.Item1); } }

        protected override void Setup(IEnumerable<T> args)
        {
            items = (from i in args select new Tuple<T, CheckListViewItem>(i, converter(i))).ToArray(); // ToArray forces the immediate conversion

            ListViewSection list = new ListViewSection(false);
            foreach (var i in items)
                list.AddItem(i.Item2);

            ListView listView = new ListView();
            listView.AddSection(0, list);
            page.View = listView;
        }
    }
}