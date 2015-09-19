using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.OS;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{


    //public class Field<T>
    //{
    //    internal DataGridCustomColumn<T> Column { get; private set; }
    //    public Field(string header, Func<T, System.Windows.FrameworkElement> constructor, Func<T, T, int> comparer, bool isPrimaryField)
    //    {
    //        Column = Platform.EvaluateOnMainThread(() => new DataGridCustomColumn<T>(constructor, comparer) {
    //            Header = header,
    //            CanUserSort = (comparer != null),
    //            IsReadOnly = true,
    //            Width = (isPrimaryField ? new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)
    //            : new System.Windows.Controls.DataGridLength(0, System.Windows.Controls.DataGridLengthUnitType.SizeToCells))
    //        });
    //    }
    //}


    /// <summary>
    /// Describes a UI field for some data type.
    /// An instance of Field describes conceptually how a particular aspect of a data instance
    /// should be displayed to the user. The actual visual appearance depends on the context
    /// in which the field is used.
    /// </summary>
    public class Field<T>
    {
        /// <summary>
        /// Describes the meaning of the field
        /// </summary>
        public string Header { get; protected set; }

        /// <summary>
        /// Specifies if the editor of this field needs a large display space.
        /// </summary>
        public bool HasLargeEditor { get; protected set; }

        /// <summary>
        /// Specifies if the editor of this field has a built-in display of the value being edited.
        /// </summary>
        public bool EditorShowsValue { get; protected set; }

        /// <summary>
        /// Should return a human readable text representation of the field value.
        /// </summary>
        public Func<T, string> TextConstructor { get; protected set; }

        /// <summary>
        /// Should return null or construct a view that allows editing of the value.
        /// First argument: data item for which the editor is to be constructed.
        /// Second argument: action that should be invoked after the value has changed.
        /// </summary>
        public Func<T, Action, View> EditorConstructor { get; protected set; }


        protected Field(string header, Func<T, string> textConstructor, Func<T, Action, View> editorConstructor, bool largeEditor, bool editorShowsValue)
        {
            Header = header;
            TextConstructor = textConstructor;
            EditorConstructor = editorConstructor;
            HasLargeEditor = largeEditor;
            EditorShowsValue = editorShowsValue;
        }

        protected Field(string header, Func<T, string> textConstructor)
            : this(header, textConstructor, null, false, false)
        {
        }


        /// <summary>
        /// Constructs a list view item that represents this field.
        /// </summary>
        public ListViewItem ConstructListViewItem(T obj)
        {
            if (TextConstructor == null && EditorConstructor == null)
                throw new Exception("nothing to show");

            /*if (EditorConstructor == null) {
                var item = new ListViewItem(false, false, false) {
                    Text = Header,
                    AccessoryView = label
                };

                item.UpdateAction = i => {
                    label.Text = TextConstructor(obj);
                    i.LayoutSubviews();
                    label.UpdateLayout();
                };
                item.PerformUpdate();
                return item;

            } else if (HasLargeEditor) {
                return new 

            } else {*/

            if (EditorConstructor != null && HasLargeEditor && EditorShowsValue)
                return new PlainListViewItem(EditorConstructor(obj, () => { }));

                Label label = null;
                var item = new ListViewItem(false, false, false);

                if (EditorConstructor != null && !HasLargeEditor)
                    item.AccessoryView = EditorConstructor(obj, () => item.PerformUpdate());
                else if (TextConstructor != null)
                    item.AccessoryView = label = new Label() { Autosize = true };

                if (EditorConstructor != null && HasLargeEditor) {
                    item.AccessoryItemConstructor = i => new PlainListViewItem(EditorConstructor(obj, i.PerformUpdate));
                    item.IsSelectable = true; // todo: consider moving to ListViewItem.AccessoryItemConstructor setter
                    item.PersistentSelection = false;

                    item.AccessoryItemExpanded += () => label.TextColor = item.TintColor.ToColor(); // todo: add "highlight" function
                    item.AccessoryItemCollapsed += () => label.TextColor = Color.Black;
                }

                item.UpdateAction = i => {
                    var text = Header;
                    if (label != null) {
                        label.Text = TextConstructor(obj);
                        label.UpdateLayout();
                    } else if (TextConstructor != null) {
                        text += ": " + TextConstructor(obj);
                    }
                    i.Text = text;
                    i.LayoutSubviews();
                };
                item.PerformUpdate();

                return item;
            /*}*/
        }


        /// <summary>
        /// Constructs a single list view item for a data instance that incorporates all of the specified fields.
        /// </summary>
        public static ListViewItem ConstructListViewItem(T obj, Action<T> selectedAction, NavigationView nav, params Field<T>[] fields)
        {
            if (fields == null)
                throw new ArgumentNullException("field list must not be null", "fields");
            if (!fields.Any())
                throw new ArgumentException("field list must not be empty", "fields");

            bool overloaded = false;
            Func<string> primaryText = null, secondaryText = null, tertiaryText = null; // primary: title, secondary: right side, tertiary: subtitle
            View smallEditor = null, largeEditor = null;
            Field<T> smallEditorField = null;
            bool useCheckmark = false;

            ListViewItem item = null;

            for (int i = 0; i < fields.Count() && !overloaded; i++) {
                if (fields[i].TextConstructor != null) {
                    Func<int, Func<string>> text = (i2) => () => fields[i2].TextConstructor(obj);
                    if (primaryText == null)
                        primaryText = text(i);
                    else if (secondaryText == null)
                        secondaryText = text(i);
                    else if (tertiaryText == null)
                        tertiaryText = text(i);
                    else
                        overloaded = true;
                }

                if (fields[i].EditorConstructor != null) {
                    if (fields[i].HasLargeEditor)
                        if (largeEditor == null)
                            largeEditor = fields[i].EditorConstructor(obj, () => item.PerformUpdate());
                        else
                            overloaded = true;
                    else
                        if (smallEditor == null) // don't generate editor for a bool field: the integrated check-mark will be used
                            smallEditor = (useCheckmark = ((smallEditorField = fields[i]) is BoolFieldView<T>)) ? null : fields[i].EditorConstructor(obj, () => item.PerformUpdate());
                        else
                            overloaded = true;
                }
            }

            if (smallEditor != null || useCheckmark) {
                if (tertiaryText == null) {
                    tertiaryText = secondaryText;
                    secondaryText = null;
                } else {
                    overloaded = true;
                }
            }

            // if there are too many elements on the listview element, we just add another navigation level
            if (overloaded) {
                if (selectedAction != null)
                    throw new NotImplementedException("too many fields");

                var title = new FieldSource<string>(primaryText);
                var subtitle = new FieldSource<string>(secondaryText);
                var editor = new EditorViewController<T>() {
                    TitleSource = title,
                    SubtitleSource = subtitle,
                    Data = new DataSource<T>(obj),
                    Fields = fields
                };
                editor.DidEdit += () => { title.PerformUpdate(); subtitle.PerformUpdate(); };
                return editor.ConstructListViewItem(nav);
            }

            if (primaryText == null && secondaryText == null && tertiaryText == null)
                if (smallEditor != null || useCheckmark)
                    primaryText = () => fields[0].Header;
                else
                    return item = new PlainListViewItem(largeEditor);

            if (primaryText == null)
                throw new NotImplementedException("this field combination is not yet implemented");

            //if (smallEditorField is BoolFieldView<T>)
            //    item = new CheckListViewItem(tertiaryText != null);
            //else
                item = new ListViewItem(selectedAction != null, false, tertiaryText != null);

                if (smallEditor != null) {
                    item.AccessoryView = smallEditor;
                } else if (useCheckmark) {
                    item.EnableCheckmark();
                    var boolField = (smallEditorField as BoolFieldView<T>).fieldSourceProvider(obj);

                    item.IsSelectable = true;
                    item.PersistentSelection = false;

                    item.CheckedChanged += (o, val) => boolField.Set(val);
                    boolField.ValueChanged += val => { item.IsChecked = val; item.PerformUpdate(); };
                    boolField.PerformUpdate();
                }

            if (largeEditor != null)
                item.AccessoryItemConstructor = x => new PlainListViewItem(largeEditor);

            if (selectedAction != null)
                item.Selected += (o, e) =>
                    selectedAction(obj);

            if (largeEditor != null || selectedAction != null){
                item.IsSelectable = true;
                item.PersistentSelection = false;
            }

            

            item.UpdateAction = (i) => {
                i.Text = primaryText();
                if (smallEditor == null && secondaryText != null)
                    i.AccessoryView = new Label() { Text = secondaryText() };
                if (tertiaryText != null)
                    i.Subtitle = tertiaryText();
                i.LayoutSubviews();
            };
            item.PerformUpdate();

            return item;
        }
    }

    public class Field<TData, TField> : Field<TData>
    {
        TData fieldData = default(TData);
        FieldSource<TField> fieldSource = null;
        Func<TData, FieldSource<TField>> fieldSourceProvider;

        private FieldSource<TField> GetFieldSource(TData obj) {
            if (!obj.Equals(fieldData))
                fieldSource = fieldSourceProvider(fieldData = obj);
            return fieldSource;
        }

        protected Field(string header, Func<TData, FieldSource<TField>> fieldSourceProvider, Func<FieldSource<TField>, string> textConstructor, Func<FieldSource<TField>, View> editorConstructor, bool largeEditor, bool editorShowsValue)
            : base(header, null, null, largeEditor, editorShowsValue)
        {
            this.fieldSourceProvider = fieldSourceProvider;

            if (textConstructor != null)
                TextConstructor = data => textConstructor(GetFieldSource(data));

            if (editorConstructor != null) {
                EditorConstructor = (data, update) => {
                    var result = editorConstructor(GetFieldSource(data));
                    GetFieldSource(data).ValueChanged += val => update();
                    return result;
                };
            }
        }

        protected Field(string header, Func<TData, FieldSource<TField>> fieldSourceProvider, Func<FieldSource<TField>, string> textConstructor)
            : this(header, fieldSourceProvider, textConstructor, null, false, false)
        {
        }
    }


    //public class MultiFieldView<T, TInternal> : Field<T>
    //{
    //    private Dictionary<T, TInternal> dataMappings = new Dictionary<T, TInternal>();
    //    private Func<T, TInternal> converter;
    //    public string Delimiter { get; private set; }
    //
    //    private TInternal Map(T obj)
    //    {
    //        TInternal result;
    //        if (!dataMappings.TryGetValue(obj, out result))
    //            result = dataMappings[obj] = converter(obj);
    //        return result;
    //    }
    //
    //    /// <summary>
    //    /// none of the subfields must be a multifield itself
    //    /// </summary>
    //    /// <param name="header">String that describes this collection of fields</param>
    //    /// <param name="converter">A function that constructs a per-item data store that is internal to this collection of fields</param>
    //    public MultiFieldView(string header, string delimiter, Func<T, TInternal> converter, params Field<TInternal>[] fields)
    //    {
    //        Header = header;
    //        Delimiter = delimiter;
    //        this.converter = converter;
    //
    //        Constructor = (obj) => Field<TInternal>.JoinViews(() => new Label() { Text = delimiter }, fields.Select((f) => f.Constructor(Map(obj))).ToArray());
    //
    //        Columns = fields.Select((f) => new System.Windows.Controls.GridViewColumn() {
    //            Header = f.Header,
    //            CellTemplate = Abstraction.GetTemplate<T>((obj) => f.Constructor(Map(obj)))
    //        }).ToArray();
    //    }
    //}

    public class TextFieldView<T> : Field<T>
    {
        public TextFieldView(string header, Func<T, string> getter, bool isPrimaryField)
            : base(header, getter)
        {
        }
    }

    public class SmallTextFieldView<T> : Field<T, string>
    {
        public SmallTextFieldView(string header, Func<T, FieldSource<string>> fieldSourceProvider)
            : base(header, fieldSourceProvider, field => field.Get(), field => {
                var ui = new TextBox();
                ui.ConnectToField(field);
                return ui;
            }, false, true)
        {
        }
    }

    public class PasswordFieldView<T> : Field<T, string>
    {
        public PasswordFieldView(string header, Func<T, FieldSource<string>> fieldSourceProvider)
            : base(header, fieldSourceProvider, null, fieldSource => {
                var ui = new TextBox() {
                    Secure = true
                };
                ui.ConnectToField(fieldSource);
                return ui;
            }, false, true)
        {
        }
    }

    public class LargeTextFieldView<T> : Field<T, string>
    {
        public LargeTextFieldView(string header, Func<T, FieldSource<string>> fieldSourceProvider)
            : base(header, fieldSourceProvider, null, fieldSource => {
                var ui = new TextField(!fieldSource.IsReadOnly) { PlaceholderText = header };
                ui.ConnectToField(fieldSource);
                return ui; 
            }, true, true)
        {
        }
    }

    public class BoolFieldView<T> : Field<T, bool>
    {
        public readonly Func<T, FieldSource<bool>> fieldSourceProvider;

        public BoolFieldView(string header, Func<T, FieldSource<bool>> fieldSourceProvider)
            : base(header, fieldSourceProvider, null, fieldSource => {
                var ui = new BoolInput();
                ui.ConnectToField(fieldSource);
                return ui;
            }, false, true)
        {
            this.fieldSourceProvider = fieldSourceProvider;
        }
    }

    public class IntegerFieldView<T> : Field<T>
    {
        public new Func<T, string> TextConstructor { set { base.TextConstructor = value; } }

        // todo: respect highlight argument
        public IntegerFieldView(string header, Func<T, FieldSource<int>> fieldSourceProvider, Func<T, bool> highlight)
            : base(header, (obj) => fieldSourceProvider(obj).Get().ToString(), (obj, update) => {
                var ui = new Stepper();
                var fieldSource = fieldSourceProvider(obj);
                ui.ConnectToField(fieldSource);
                fieldSource.ValueChanged += val => update();
                return ui;
            },
            false, false)
        {
        }
    }

    public class DateFieldView<T> : Field<T, DateTime>
    {
        public DateFieldView(string header, Func<T, FieldSource<DateTime>> fieldSourceProvider)
            : base(header, fieldSourceProvider,
            fieldSource => fieldSource.Get().ToShortDateString(),
            fieldSource => {
                var ui = new DateTimePicker() { Mode = DateTimePicker.SelectionMode.Date };

                ui.ValueChanged += val => {
                    if (val.HasValue)
                        fieldSource.Set(val.Value.ToLocalTime().Date);
                };

                fieldSource.ValueChanged += val => {
                    ui.Value = val;
                };

                fieldSource.PerformUpdate();

                return ui;
            }, true, false)
        {
        }
    }

    public class TimeFieldView<T> : Field<T, TimeSpan>
    {
        public TimeFieldView(string header, Func<T, FieldSource<TimeSpan>> fieldSourceProvider, Func<T, bool> highlight)
            : base(header, fieldSourceProvider,
            fieldSource => fieldSource.Get().ToString("hh\\:mm"),
            fieldSource => {
                var ui = new DateTimePicker() { Mode = DateTimePicker.SelectionMode.Time };

                ui.ValueChanged += val => {
                    if (val.HasValue)
                        fieldSource.Set(val.Value.ToLocalTime().TimeOfDay);
                };

                fieldSource.ValueChanged += val => {
                    ui.Value = DateTime.Today.Add(val);
                };

                fieldSource.PerformUpdate();

                return ui;
            },
            true, false)
        {

        }
    }

    public class TimeRangeFieldView<T> : Field<T>
    {
        public TimeRangeFieldView(string header, string startHeader, string delimiter, string endHeader, Func<T, TimeRange> fieldSourceProvider)
            : base(header, obj => {
                var val = fieldSourceProvider(obj);
                return val.ProposedStart.Get().ToString("hh\\:mm") + " " + delimiter + " " + val.ProposedEnd.Get().ToString("hh\\:mm");
            }, (obj, update) => {
                var val = fieldSourceProvider(obj);
                val.ProposedEnd.ValueChanged += x => update();
                val.ProposedStart.ValueChanged += x => update();
                return new TimeRangePickerView(delimiter, 5, val);
            }, true, false)
        {
        }
    }
}
