using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.OS;
using AppInstall.Framework;

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


    // todo: update list items when the underlying data changes (see iOS implementation)

    public class Field<T>
    {
        public string Header { get; protected set; }
        public Func<T, View> Constructor { get; protected set; }


        /// <summary>
        /// Should return a human readable text representation of the field value.
        /// </summary>
        public Func<T, string> TextConstructor { get; protected set; }

        internal System.Windows.Controls.GridViewColumn[] Columns { get; set; }
        public Field(string header, Func<T, View> constructor, Func<T, T, int> comparer, bool isPrimaryField)
        {
            Columns = new System.Windows.Controls.GridViewColumn[] { Platform.EvaluateOnMainThread(() => new System.Windows.Controls.GridViewColumn() {
                Header = this.Header = header,
                CellTemplate = Abstraction.GetTemplate<T>(this.Constructor = constructor)
            })};
        }
        protected Field()
        {
            //Columns = fields.SelectMany((f) => f.Columns).ToArray();
        }

        public static View JoinViews(Func<View> delimiterFactory, params View[] fields)
        {
            var grid = new GridLayout(1, 2 * fields.Count() - 1) { Autosize = true };
            grid.RelativeRowHeights[0] = 1f;
            for (int i = 0; i < 2 * fields.Count() - 1; i++)
                grid.RelativeColumnWidths[i] = 0f;


            for (int i = 0; i < fields.Count(); i++) {
                grid[0, 2 * i] = fields[i];
            }

            if (delimiterFactory != null)
                for (int i = 0; i < fields.Count() - 1; i++) {
                    grid[0, 2 * i + 1] = fields[i];
                }

            grid.UpdateLayout();

            return grid;
        }

        public static View JoinViews(T obj, params Field<T>[] fields)
        {
            return JoinViews(null, fields.Select((f) => f.Constructor(obj)).ToArray());
        }
    }

    public class MultiFieldView<T, TInternal> : Field<T>
    {
        private Dictionary<T, TInternal> dataMappings = new Dictionary<T, TInternal>();
        private Func<T, TInternal> converter;
        public string Delimiter { get; private set; }

        private TInternal Map(T obj)
        {
            TInternal result;
            if (!dataMappings.TryGetValue(obj, out result))
                result = dataMappings[obj] = converter(obj);
            return result;
        }

        /// <summary>
        /// none of the subfields must be a multifield itself
        /// </summary>
        /// <param name="header">String that describes this collection of fields</param>
        /// <param name="converter">A function that constructs a per-item data store that is internal to this collection of fields</param>
        public MultiFieldView(string header, string delimiter, Func<T, TInternal> converter, params Field<TInternal>[] fields)
        {
            Header = header;
            Delimiter = delimiter;
            this.converter = converter;

            Constructor = (obj) => Field<TInternal>.JoinViews(() => new Label() { Text = delimiter }, fields.Select((f) => f.Constructor(Map(obj))).ToArray());

            Columns = fields.Select((f) => new System.Windows.Controls.GridViewColumn() {
                Header = f.Header,
                CellTemplate = Abstraction.GetTemplate<T>((obj) => f.Constructor(Map(obj)))
            }).ToArray();
        }
    }

    public class TextFieldView<T> : Field<T>
    {
        public TextFieldView(string header, Func<T, string> getter, bool isPrimaryField)
            : base(header, (obj) => new Label() { Text = getter(obj), Padding = new Margin(5, 0) }, (x, y) => getter(x).CompareTo(getter(y)), isPrimaryField)
        {
        }
    }

    public class SmallTextFieldView<T> : Field<T>
    {
        public SmallTextFieldView(string header, Func<T, FieldSource<string>> fieldSourceProvider)
            : base(header, (obj) => { var t = new TextBox(); t.ConnectToField(fieldSourceProvider(obj)); return t; },
            (x, y) => fieldSourceProvider(x).Get().CompareTo(fieldSourceProvider(y).Get()), false)
        {
        }
    }

    public class PasswordFieldView<T> : Field<T>
    {
        public PasswordFieldView(string header, Func<T, FieldSource<string>> fieldSourceProvider)
            : base(header, (obj) => { var p = new PasswordBox(); p.ConnectToField(fieldSourceProvider(obj)); return p; },
            (x, y) => fieldSourceProvider(x).Get().CompareTo(fieldSourceProvider(y).Get()), false)
        {
        }
    }

    public class LargeTextFieldView<T> : Field<T>
    {
        public LargeTextFieldView(string header, Func<T, FieldSource<string>> fieldSourceProvider)
            : base(header, (obj) => { var t = new TextBox(); t.ConnectToField(fieldSourceProvider(obj)); return t; },
            (x, y) => fieldSourceProvider(x).Get().CompareTo(fieldSourceProvider(y).Get()), false)
        {
        }
    }

    public class BoolFieldView<T> : Field<T>
    {
        public BoolFieldView(string header, Func<T, FieldSource<bool>> fieldSourceProvider)
            : base("", (obj) => { var b = new BoolInput(); b.ConnectToField(fieldSourceProvider(obj)); return b; },
            (x, y) => FieldSource<bool>.CompareObjects(x, y, fieldSourceProvider), false)
        {
        }
    }

    public class DateFieldView<T> : Field<T>
    {
        public DateFieldView(string header, Func<T, DateTime> getter, Action<T, DateTime> setter)
            : base(header, (obj) => {
                var date = new DateTimePicker() { Value = getter(obj) };
                date.ValueChanged += (v) => { if (v.HasValue) setter(obj, v.Value); };
                return date;
            }, (x, y) => getter(x).CompareTo(getter(y)), false)
        {
            if (setter == null)
                throw new ArgumentNullException("setter");
        }
    }

    public class IntegerFieldView<T> : Field<T>
    {
        public new Func<T, string> TextConstructor { set { base.TextConstructor = value; } }

        public IntegerFieldView(string header, Func<T, FieldSource<int>> fieldSourceProvider, Func<T, bool> highlight)
            : base(header,
            (obj) => {
                var t = new TextBox();
                t.ConnectToField(fieldSourceProvider(obj), (i) => i.ToString(),
                (str) => {
                    int val;
                    if (int.TryParse(str, out val))
                        return new Tuple<int, bool>(val, true);
                    else
                        return new Tuple<int, bool>(default(int), false);
                }, null);
                return t;
            },
            (x, y) => FieldSource<int>.CompareObjects(x, y, fieldSourceProvider), false)
        {
        }
    }

    public class TimeFieldView<T> : Field<T>
    {
        public TimeFieldView(string header, Func<T, FieldSource<TimeSpan>> fieldSourceProvider, Func<T, bool> forceHighlight)
            : base(header, (obj) => {
                var tb = new TextBox();
                tb.ConnectToField(fieldSourceProvider(obj), (t) => t.ToString(@"hh\:mm"), (str) => {
                    TimeSpan val;
                    if (TimeSpan.TryParseExact(str, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out val))
                        return new Tuple<TimeSpan, bool>(val, true);
                    else
                        return new Tuple<TimeSpan, bool>(default(TimeSpan), false);
                }, () => forceHighlight(obj));
                return tb;
            },
            (x, y) => fieldSourceProvider(x).Get().CompareTo(fieldSourceProvider(y).Get()), false)
        {

        }
    }

    public class TimeRangeFieldView<T> : MultiFieldView<T, TimeRange>
    {
        public TimeRangeFieldView(string header, string startHeader, string delimiter, string endHeader, Func<T, TimeRange> fieldSourceProvider)
            : base(header, delimiter, obj => fieldSourceProvider(obj),
                new TimeFieldView<TimeRange>(startHeader, obj => obj.ProposedStart, obj => !obj.IsValid),
                new TimeFieldView<TimeRange>(endHeader, obj => obj.ProposedEnd, obj => !obj.IsValid))
        {
        }
    }
}
