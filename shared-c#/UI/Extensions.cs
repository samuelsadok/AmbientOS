using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    public static class Extensions
    {

        /// <summary>
        /// Connects a text-based UI element to a FieldSource, so that updates to either of the two propagate to the other one.
        /// </summary>
        /// <param name="fieldSource">the data source that represents the data that should be displayed</param>
        /// <param name="toString">converts the underlying data type into a human-readable text</param>
        /// <param name="parser">converts the text into the underlying data type (may return false in the tuple if the conversion failed)</param>
        /// <param name="forceHighlight">if this returns true, the textbox is highlighted even if the text-to-data conversion succeeds</param>
        public static void ConnectToField<T>(this ITextBox ui, FieldSource<T> fieldSource, Func<T, string> toString, Func<string, Tuple<T, bool>> parser, Func<bool> forceHighlight)
        {
            var defaultColor = ui.TextColor;

            FieldSource<string> uiField = new FieldSource<string>(() => ui.Text, (val) => ui.Text = val);
            if (!(ui.IsReadOnly = fieldSource.IsReadOnly))
                ui.TextChanged += (t) => uiField.PerformUpdate();

            if (forceHighlight == null) forceHighlight = () => false;

            Action<bool> updateHighlight = (validText) => {
                if (validText && !forceHighlight())
                    ui.TextColor = defaultColor;
                else
                    ui.TextColor = Color.Red;
            };

            FieldSource<T>.Connect(fieldSource, uiField,
                (val) => new Tuple<string, bool>(toString(val), true), parser,
                (valid) => updateHighlight(true), updateHighlight);
        }

        /// <summary>
        /// Connects a text-based UI element to a FieldSource, so that updates to either of the two propagate to the other one.
        /// </summary>
        /// <param name="fieldSource">the data source that represents the text that should be displayed</param>
        /// <param name="forceHighlight">if this returns true, the textbox is highlighted to indicate an invalid input</param>
        public static void ConnectToField(this ITextBox ui, FieldSource<string> fieldSource, Func<bool> highlight)
        {
            if (highlight == null)
                throw new ArgumentNullException("highlight");
            ConnectToField(ui, fieldSource, (str) => str, (str) => new Tuple<string, bool>(str, highlight()), null);
        }

        /// <summary>
        /// Connects a text-based UI element to a FieldSource, so that updates to either of the two propagate to the other one.
        /// </summary>
        /// <param name="fieldSource">the data source that represents the text that should be displayed</param>
        public static void ConnectToField(this ITextBox ui, FieldSource<string> fieldSource)
        {
            ConnectToField(ui, fieldSource, (str) => str, (str) => new Tuple<string, bool>(str, true), null);
        }


        public static void ConnectToField(this BoolInput ui, FieldSource<bool> fieldSource)
        {
            FieldSource<bool?> uiField = new FieldSource<bool?>(() => ui.Value, (val) => ui.Value = val);
            ui.ValueChanged += (val) => uiField.PerformUpdate();

            FieldSource<bool>.Connect(fieldSource, uiField,
                (val) => new Tuple<bool?, bool>(val, true), (val) => new Tuple<bool, bool>((val.HasValue ? val.Value : false), val.HasValue),
                null, null);
        }


        public static void ConnectToField(this Stepper ui, FieldSource<int> fieldSource)
        {
            FieldSource<double> uiField = new FieldSource<double>(() => ui.Value, (val) => ui.Value = val);
            ui.ValueChanged += (val) => uiField.PerformUpdate();

            FieldSource<int>.Connect(fieldSource, uiField,
                (val) => new Tuple<double, bool>(val, true), (val) => new Tuple<int, bool>((int)val, (double)((int)val) == val),
                null, null);
        }
    }
}
