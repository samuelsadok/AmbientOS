using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppInstall.Framework;

namespace AppInstall.UI
{
    public interface ITimeRange
    {
        DateTime Time1 { get; set; }
        DateTime Time2 { get; set; }
    }


    class TimeRangePicker : DataPickerView
    {
        private const int EARLIEST_HOUR = 6;
        private const int LATEST_HOUR = 23;
        private const int MINUTE_SPACING = 5;
        private static string[] hourList = (from i in Enumerable.Range(EARLIEST_HOUR, LATEST_HOUR - EARLIEST_HOUR + 1) select i.ToString()).ToArray();
        private static string[] minuteList = (from i in Enumerable.Range(0, 60 / MINUTE_SPACING) select (i * MINUTE_SPACING).ToString("D2")).ToArray();
        private static Func<int, int> indexToHour = (i) => i + EARLIEST_HOUR;
        private static Func<int, int> hourToIndex = (h) => h - EARLIEST_HOUR;
        private static Func<int, int> indexToMinute = (i) => i * MINUTE_SPACING;
        private static Func<int, int> minuteToIndex = (m) => m / MINUTE_SPACING;

        private ITimeRange range;

        public event Action<TimeRangePicker> TimeChanged;

        /// <summary>
        /// Creates a view that lets the user pick a start and a stop time. Time2 is guaranteed to be always later than or equal to Time1.
        /// </summary>
        /// <param name="time1">the initially selected start time</param>
        /// <param name="delimiter">the text to display between the two times</param>
        /// <param name="time2">the initially selected end time</param>
        public TimeRangePicker(ITimeRange range, string delimiter)
            : base(new DataPickerColumn[] {
                new DataPickerDelimiter(" ", true),
                new DataPickerColumn(hourList, hourToIndex(range.Time1.Hour), false, false),
                new DataPickerDelimiter(":", false),
                new DataPickerColumn(minuteList, minuteToIndex(range.Time1.Minute), false, false),
                new DataPickerDelimiter(delimiter, false),
                new DataPickerColumn(hourList, hourToIndex(range.Time2.Hour), false, false),
                new DataPickerDelimiter(":", false),
                new DataPickerColumn(minuteList, minuteToIndex(range.Time2.Minute), false, false),
                new DataPickerDelimiter(" ", true)
            })
        {
            this.range = range;

            SelectionChanged += (o, e) => {
                if (e.Item1 == 1)
                    SetTime1(range.Time1.AddHours(indexToHour(e.Item2) - range.Time1.Hour), false);
                else if (e.Item1 == 3)
                    SetTime1(range.Time1.AddMinutes(indexToMinute(e.Item2) - range.Time1.Minute), false);
                else if (e.Item1 == 5)
                    SetTime2(range.Time2.AddHours(indexToHour(e.Item2) - range.Time2.Hour), false);
                else if (e.Item1 == 7)
                    SetTime2(range.Time2.AddMinutes(indexToMinute(e.Item2) - range.Time2.Minute), false);
            };
        }

        private void SetTime1(DateTime t, bool updateGUI)
        {
            if (t > range.Time2) { t = range.Time2; updateGUI = true; }
            range.Time1 = t;
            if (updateGUI) {
                Select(1, hourToIndex(range.Time1.Hour), true);
                Select(3, minuteToIndex(range.Time1.Minute), true);
            }
            TimeChanged.SafeInvoke(this);
        }
        private void SetTime2(DateTime t, bool updateGUI)
        {
            if (range.Time1 > t) { t = range.Time1; updateGUI = true; }
            range.Time2 = t;
            if (updateGUI) {
                Select(5, hourToIndex(range.Time2.Hour), true);
                Select(7, minuteToIndex(range.Time2.Minute), true);
            }
            TimeChanged.SafeInvoke(this);
        }

        /// <summary>
        /// Rectifies a time range so that it only takes on values that could also be selected by the TimeRangePicker.
        /// Time2 will be moved to the same day as time1.
        /// </summary>
        public static void RectifyTimeRange(ITimeRange timeRange)
        {
            timeRange.Time1 = new DateTime(timeRange.Time1.Year, timeRange.Time1.Month, timeRange.Time1.Day, Scalar.Bound(timeRange.Time1.Hour, EARLIEST_HOUR, LATEST_HOUR), (int)(timeRange.Time1.Minute / MINUTE_SPACING) * MINUTE_SPACING, 0);
            timeRange.Time2 = new DateTime(timeRange.Time1.Year, timeRange.Time1.Month, timeRange.Time1.Day, Scalar.Bound(timeRange.Time2.Hour, EARLIEST_HOUR, LATEST_HOUR), (int)(timeRange.Time2.Minute / MINUTE_SPACING) * MINUTE_SPACING, 0);
            if (timeRange.Time1 > timeRange.Time2) timeRange.Time2 = timeRange.Time1;
        }
    }
}