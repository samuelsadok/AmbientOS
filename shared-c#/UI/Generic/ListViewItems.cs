using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{


    // todo: most of the listview item types are now deprecated since the view controller model is used to generate any kind of listview item => remove deprecated types





    //public class ButtonListViewItem : ListViewItem
    //{
    //    private IPhysicalQuantity quantity;
    //    private Label quantityLabel = new Label() { FontSize = 16f, TextAlignment = new Directions(false, true, false, false) };

    //    public ButtonListViewItem(IPhysicalQuantity quantity)
    //        : base(false, false, false)
    //    {
    //        this.quantity = quantity;

    //        IsSelectable = true;
    //        quantityLabel.SizeSampleText = "00" + quantity.ToString();
    //        AccessoryView = quantityLabel;
    //        UpdateQuantityLabel();
    //    }

    //    public override ListViewItem ConstructAccessoryItem()
    //    {
    //        quantityLabel.TextColor = Application.ThemeColor;
    //        var picker = new QuantitySelector(quantity);
    //        picker.QuantityChanged += (o) => { LogSystem.Log("fired"); };
    //        picker.QuantityChanged += (o) => { UpdateQuantityLabel(); };
    //        return new PlainListViewItem(new QuantitySelector(quantity));
    //    }
    //    public override void DestructAccessoryItem()
    //    {
    //        quantityLabel.TextColor = Color.Black;
    //    }

    //    private void UpdateQuantityLabel()
    //    {
    //        LogSystem.Log("q = " + quantity.GetHashCode());
    //        quantityLabel.Text = quantity.ToString();
    //    }
    //}


    [Obsolete("use the built-in listview generator of the ListViewController", true)]
    public class TimeRangeListViewItem : ListViewItem
    {
        private string delimiter;
        private ITimeRange data;
        private Label timeLabel = new Label() { FontSize = 14f, TextAlignment = TextAlignment.Right };

        public TimeRangeListViewItem(ITimeRange data, string timeDelimiter)
            : base(false, false, false)
        {
            TimeRangePicker.RectifyTimeRange(data);
            this.data = data;
            this.delimiter = timeDelimiter;

            IsSelectable = true;
            timeLabel.SizeSampleText = "00:00\n" + timeDelimiter + " 00:00";
            AccessoryView = timeLabel;
            UpdateTimeLabel();
        }

        public override ListViewItem ConstructAccessoryItem()
        {
            timeLabel.TextColor = Application.ThemeColor;
            var picker = new TimeRangePicker(data, delimiter);
            picker.TimeChanged += (o) => { UpdateTimeLabel(); };
            return new PlainListViewItem(picker);
        }
        public override void DestructAccessoryItem()
        {
            timeLabel.TextColor = Color.Black;
        }

        private void UpdateTimeLabel()
        {
            timeLabel.Text = data.Time1.ToString("HH:mm") + "\n" + delimiter + " " + data.Time2.ToString("HH:mm");
        }
    }

    //public class QuantitySelectorListViewItem : ListViewItem
    //{
    //    private IPhysicalQuantity quantity;
    //    private Label quantityLabel = new Label() { FontSize = 16f, TextAlignment = new Directions(false, true, false, false) };
    //
    //    public QuantitySelectorListViewItem(IPhysicalQuantity quantity)
    //        : base(false, false, false)
    //    {
    //        this.quantity = quantity;
    //
    //        IsSelectable = true;
    //        quantityLabel.SizeSampleText = "00" + quantity.ToString();
    //        AccessoryView = quantityLabel;
    //        UpdateQuantityLabel();
    //    }
    //
    //    public override ListViewItem ConstructAccessoryItem()
    //    {
    //        quantityLabel.TextColor = Application.ThemeColor;
    //        var picker = new QuantitySelector(quantity);
    //        picker.QuantityChanged += (o) => { Application.UILog.Log("fired"); };
    //        picker.QuantityChanged += (o) => { UpdateQuantityLabel(); };
    //        return new PlainListViewItem(picker);
    //    }
    //    public override void DestructAccessoryItem()
    //    {
    //        quantityLabel.TextColor = Color.Black;
    //    }
    //
    //    private void UpdateQuantityLabel()
    //    {
    //        Application.UILog.Log("q = " + quantity.GetHashCode());
    //        quantityLabel.Text = quantity.ToString();
    //    }
    //}


    [Obsolete("use the built-in listview generator of the ListViewController", true)]
    public class TextFieldListViewItem : PlainListViewItem
    {
        private TextField textField;

        public event Action TextChanged;
        public new string Text { get { return textField.Text; } set { textField.Text = value; } }
        public string PlaceholderText { get { return textField.PlaceholderText; } set { textField.PlaceholderText = value; } }

        public TextFieldListViewItem(BaseListView parent)
            : this(new TextField(true), parent)
        {
        }
        private TextFieldListViewItem(TextField t, BaseListView parent)
            : base(t)
        {
            this.textField = t;
            t.EditingStarted += (o) => parent.MakeVisible(this, true);
            t.TextChanged += (o) => TextChanged.SafeInvoke();
            Height = 150f;
        }
    }


    [Obsolete("use the built-in listview generator of the ListViewController", true)]
    public class AccessoryListViewItem<T> : ListViewItem
        where T : View, new()
    {
        private T accessoryView;

        public new T AccessoryView { get { return accessoryView; } set { base.AccessoryView = (accessoryView = value); } }

        public AccessoryListViewItem(T accessoryView, bool subtitle)
            : base(false, false, subtitle)
        {
            if (accessoryView == null)
                accessoryView = new T();
            AccessoryView = accessoryView;
            TextChanged += () => LayoutSubviews();
        }

        public AccessoryListViewItem(bool subtitle)
            : this(null, subtitle)
        {
        }

        public override void LayoutSubviews()
        {
            //var rightMargin = Frame.Size.Width - AccessoryView.Location.X - AccessoryView.Size.X;
            //Application.UILog.Log("left margin " + LeftMargin + ", right margin " + AccessoryViewRightMargin + ", right margin 2" + AccessoryViewRightMargin2);
            //AccessoryView.Size = new Vector2D<float>(Math.Max(0, Frame.Size.Width - LeftMargin - TextLabel.StringSize(TextLabel.Text, TextLabel.Font).Width - 2 * AccessoryViewRightMargin), 40);
            //Application.UILog.Log("accessory size is " + AccessoryView.Size + ", text size is " + (Frame.Size.Width - TextLabel.StringSize(TextLabel.Text, TextLabel.Font).Width - TextLabel.Frame.X) + ", frame width " + Frame.Size.Width + " right margin " + rightMargin);
            ////AccessoryView.Location = new Vector2D<float>(0, 0);
            //AccessoryView.UpdateLayout();
            Application.UILog.Log("frame width " + Frame.Width + " label pos " + TextLabel.Frame.X + " text width " + TextLabel.StringSize(TextLabel.Text, TextLabel.Font).Width);
            AccessoryView.Size = new Vector2D<float>(Math.Max(0, Frame.Width - TextLabel.Frame.X - TextLabel.StringSize(TextLabel.Text, TextLabel.Font).Width) - 2 * 20, 40);
            AccessoryView.UpdateLayout();
            base.LayoutSubviews();
        }
    }


    [Obsolete("only used by a deprecated list view item type", true)]
    public interface IDoubleDisplayString
    {
        string DisplayString { get; }
        string SecondaryDisplayString { get; }
    }


    [Obsolete("use the built-in listview generator of the ListViewController", true)]
    public class SelectionListViewItem : ListViewItem
    {
        private IDoubleDisplayString DefaultItem;
        private IDoubleDisplayString[] Selectables;

        /// <summary>
        /// The index of the selected item in the list. -1 refers to the default item.
        /// </summary>
        public int Selection { get { return selection; } set { selection = value; var selected = (selection == -1 ? DefaultItem : Selectables[selection]); Text = selected.DisplayString; Subtitle = selected.SecondaryDisplayString; } }
        private int selection;

        public SelectionListViewItem(IDoubleDisplayString[] selectables, IDoubleDisplayString defaultItem, string selectionDialogTitle, LayerLayout selectionDialogParent)
            : base(true, false, true)
        {
            Selectables = selectables;
            DefaultItem = defaultItem;
            Selection = -1;

            IsSelectable = true;

            Selected += (o, e) => ShowDialog(selectionDialogTitle, selectionDialogParent);
        }

        public async void ShowDialog(string selectionDialogTitle, LayerLayout selectionDialogParent)
        {
            SingleSelectionDialog<IDoubleDisplayString> dialog = new SingleSelectionDialog<IDoubleDisplayString>(selectionDialogTitle, Selectables, (item) => new CheckListViewItem(true) { Text = item.DisplayString, Subtitle = item.SecondaryDisplayString });
            Selection = await dialog.Show(Selection, selectionDialogParent);
        }
    }


    [Obsolete("use the built-in listview generator of the ListViewController", true)]
    public class OptionsListViewItem<T> : ListViewItem
    {
        private T[] options;
        private Func<T, string> converter;
        private int selection;
        private Label selectionLabel = new Label() { FontSize = 16f, TextAlignment = TextAlignment.Right, Autosize = true };

        public T Selection { get { return options[selection]; } }

        public OptionsListViewItem(T[] options, Func<T, string> converter, int defaultSelection)
            : base(false, false, false)
        {
            this.options = options;
            this.converter = converter;
            this.selection = defaultSelection;

            IsSelectable = true;
            selectionLabel.SizeSampleText = "";
            AccessoryView = selectionLabel;
            UpdateSelection(this.selection);
        }

        public override ListViewItem ConstructAccessoryItem()
        {
            selectionLabel.TextColor = Application.ThemeColor;
            var picker = new DataPickerView(new DataPickerColumn[] {
                new DataPickerColumn(options.Select(converter).ToArray(), selection, false, false)
            });
            Application.UILog.Log("units: " + options.Count());
            //picker.Select(0, selection, false);
            picker.SelectionChanged += (o, e) => UpdateSelection(e.Item2);
            //picker.UpdateLayout();
            return new PlainListViewItem(picker);
        }
        public override void DestructAccessoryItem()
        {
            selectionLabel.TextColor = Color.Black;
        }

        private void UpdateSelection(int selection)
        {
            this.selection = selection;
            Application.UILog.Log("did reselect");
            selectionLabel.Text = converter(Selection);
            selectionLabel.UpdateLayout();
        }
    }
}