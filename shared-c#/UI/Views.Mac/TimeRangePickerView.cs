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
    public class TimeRangePickerView : DataPickerView
    {

        public TimeRangePickerView(string delimiter, int minuteInterval, TimeRange data)
            : base()
        {
            AddColumn(24, i => i.ToString(), 0, true, false);
            AddColumn(1, i => ":", 0, false, false);
            AddColumn(60 / minuteInterval, i => (i * minuteInterval).ToString(), 0, true, false);
            AddColumn(1, i => "bis", 0, false, false);
            AddColumn(24, i => i.ToString(), 0, true, false);
            AddColumn(1, i => ":", 0, false, false);
            AddColumn(60 / minuteInterval, i => (i * minuteInterval).ToString(), 0, true, false);
            //AddColumn(0, x => null, 0, false, true);


            Action<TimeSpan> updateStart = val => {
                Select(0, val.Hours, true);
                Select(2, (int)Math.Round((float)val.Minutes / minuteInterval), true);
            };

            Action<TimeSpan> updateEnd = val => {
                Select(4, val.Hours, true);
                Select(6, (int)Math.Round((float)val.Minutes / minuteInterval), true);
            };

            data.ProposedStart.ValueChanged += updateStart;

            data.ProposedEnd.ValueChanged += updateEnd;

            SelectionChanged += (o, val) => {
                if (val.Item1 == 0 || val.Item1 == 2) {
                    data.ProposedStart.Set(new TimeSpan(GetSelected(0), GetSelected(2) * minuteInterval, 0));
                    //updateStart(data.ProposedStart.Get());
                } else if (val.Item1 == 4 || val.Item1 == 6) {
                    data.ProposedEnd.Set(new TimeSpan(GetSelected(4), GetSelected(6) * minuteInterval, 0)); 
                    //updateEnd(data.ProposedEnd.Get());
                }
            };
        }



        /*
        protected override void UpdateContentLayout()
        {
            float flexibleWidth = (Size.X - 5 * data.Count() - fixedColumnsWidth) / (float)flexibleColumns;
            for (int i = 0; i < columnWidths.Count(); i++)
                if (data[i].Item3) columnWidths[i] = flexibleWidth; // todo: respect padding
        }

        public void Select(int section, int row, bool animated)
        {
            Application.UILog.Log("time range select");
            nativeView.Select(row, section, animated);
            Selected(section, row);
        }

        private void Selected(int section, int row)
        {
            Application.UILog.Log("time range selected");
            SelectionChanged.SafeInvoke(this, new Tuple<int, int>(section, row));
        }

        private class DataSource : UIPickerViewModel
        {
            private TimeRangePickerView parent;
            public DataSource(TimeRangePickerView parent)
            {
                this.parent = parent;
            }
            public override int GetComponentCount(UIPickerView picker)
            {
                return 7;
            }
            public override int GetRowsInComponent(UIPickerView picker, int component)
            {
                return (component % 2 == 0) ? 1 : int.MaxValue; // todo: ensure there is no memory problem
            }
            public override string GetTitle(UIPickerView picker, int row, int component)
            {
                if (component == 3)
                    return parent.Delimiter;

                if (component == 1 || component == 5)
                    return ":";

                if (component == 0 || component == 4)
                    return (row % 24).ToString();

                if (component == 2 || component == 6)
                    return (row * parent.MinuteSpacing % 60).ToString();

                throw new InvalidOperationException("unknown column " + component);
            }
            public override float GetComponentWidth(UIPickerView picker, int component)
            {
                return parent.columnWidths[component];
            }
            public override void DidChange(NSKeyValueChange changeKind, NSIndexSet indexes, NSString forKey)
            {
                Application.UILog.Log("datasource.didChange");
            }
            public override void Selected(UIPickerView picker, int row, int component)
            {
                Application.UILog.Log("datasource.selected");
                parent.Selected(component, row);
            }
        } */
    }
}