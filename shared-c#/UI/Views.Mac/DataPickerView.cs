using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using Foundation;
using UIKit;
using AppInstall.Framework;

namespace AppInstall.UI
{
    public class DataPickerView : View<UIPickerView>
    {
        private List<Tuple<int, Converter<int, string>, bool, bool>> data = new List<Tuple<int, Converter<int, string>, bool, bool>>(4);
        private List<float> columnWidths = new List<float>();
        private int flexibleColumns;
        private float fixedColumnsWidth;

        public event EventHandler<Tuple<int, int>> SelectionChanged;

        //public string Text { get { return (picker.TitleLabel == null ? "" : picker.TitleLabel.Text); } set { if (picker.TitleLabel != null) { picker.SetTitle(value, UIControlState.Normal); LogSystem.Log("set to " + value); } else { LogSystem.Log("is null"); } } }

        //public List<List<string>> Text { get { return (picker.TitleLabel == null ? "" : picker.TitleLabel.Text); } set { if (picker.TitleLabel != null) { picker.SetTitle(value, UIControlState.Normal); LogSystem.Log("set to " + value); } else { LogSystem.Log("is null"); } } }

        public DataPickerView(params DataPickerColumn[] columns)
            : base(true)
        {
            nativeView.Model = new DataSource(this);
            foreach (var c in columns)
                AddColumn(c);
        }

        public void AddColumn(int itemCount, Converter<int, string> itemConstructor, int defaultSelection, bool loop, bool flexibleWidth)
        {
            data.Add(new Tuple<int, Converter<int, string>, bool, bool>(itemCount, itemConstructor, loop, flexibleWidth));

            // determine required column width
            if (flexibleWidth) {
                columnWidths.Add(0);
                flexibleColumns++;
            } else {
                var maxChars = 0;
                for (int i = 0; i < itemCount; i++)
                    maxChars = Math.Max(maxChars, itemConstructor(i).Count());
                columnWidths.Add(maxChars * 20);
                fixedColumnsWidth += maxChars * 20;
            }

            nativeView.ReloadAllComponents();
            nativeView.Select(defaultSelection, data.Count() - 1, false);
        }
        public void AddColumn(DataPickerColumn column)
        {
            AddColumn(column.Items.Count(), (i) => column.Items[i], column.DefaultSelection, column.Loop, column.FlexibleWidth);
            foreach (var i in column.Items)
                Application.UILog.Log("option: " + i);
        }

        protected override void UpdateContentLayout()
        {
            if (flexibleColumns > 0) {
                float flexibleWidth = (Size.X - 5 * (data.Count() - 1) - fixedColumnsWidth) / (float)flexibleColumns;
                for (int i = 0; i < columnWidths.Count(); i++)
                    if (data[i].Item3) columnWidths[i] = flexibleWidth; // todo: respect padding
            }

            //Application.UILog.Log("data picker layout for width " + Size.X + ", flexible " + flexibleWidth + ": " + string.Join(" - ", columnWidths.Select(w => w.ToString())));
            nativeView.ReloadAllComponents();
            nativeView.SetNeedsLayout();
            
        }


        public void Select(int section, int row, bool animated)
        {
            Application.UILog.Log("select");
            nativeView.Select(row, section, animated);
            Selected(section, row);
        }

        private void Selected(int section, int row)
        {
            Application.UILog.Log("selected");
            SelectionChanged.SafeInvoke(this, new Tuple<int, int>(section, row % data[section].Item1));
        }

        public int GetSelected(int section)
        {
            return (int)nativeView.SelectedRowInComponent((nint)section) % data[section].Item1;
        }

        private class DataSource : UIPickerViewModel
        {
            private DataPickerView parent;
            public DataSource(DataPickerView parent)
            {
                this.parent = parent;
            }
            public override nint GetComponentCount(UIPickerView picker)
            {
                return parent.data.Count();
            }
            public override nint GetRowsInComponent(UIPickerView picker, nint component)
            {
                //if (parent.data[component].Item2) return Int32.MaxValue;
                return parent.data[(int)component].Item1;
            }
            public override string GetTitle(UIPickerView picker, nint row, nint component)
            {
                //return parent.data[component].Item1[row % parent.data[component].Item1.Count()];
                return parent.data[(int)component].Item2((int)row);
            }
            public override nfloat GetComponentWidth(UIPickerView picker, nint component)
            {
                var result = parent.columnWidths[(int)component];
                //result = (320f - 6 * 5) / 7f;
                if (component == 0)
                    result -= 5;
                //if (result <= 0) result = 40f;
                Application.UILog.Log("queried component width for " + component + ": " + result);
                return result;
            }
            public override void DidChange(NSKeyValueChange changeKind, NSIndexSet indexes, NSString forKey)
            {
                Application.UILog.Log("datasource.didChange");
            }
            public override void Selected(UIPickerView picker, nint row, nint component)
            {
                Application.UILog.Log("datasource.selected");
                parent.Selected((int)component, (int)row);
            }
        }
    }

    public class DataPickerColumn
    {
        public string[] Items { get; set; }
        public int DefaultSelection { get; set; }
        public bool Loop { get; set; }
        public bool FlexibleWidth { get; set; }
        public DataPickerColumn(string[] items, int defaultSelection, bool loop, bool flexibleWidth)
        {
            Items = items;
            DefaultSelection = defaultSelection;
            Loop = loop;
            FlexibleWidth = flexibleWidth;
        }
    }

    public class DataPickerDelimiter : DataPickerColumn
    {
        public DataPickerDelimiter(string text, bool flexibleWidth)
            : base(new string[] { text }, 0, false, flexibleWidth)
        {
            if (text == null) throw new ArgumentNullException("text");
        }
    }
}