using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppInstall.Framework
{
    public class TimeRange
    {
        public TimeSpan proposedStart;
        public TimeSpan proposedEnd;
        public FieldSource<TimeSpan> ProposedStart { get; private set; }
        public FieldSource<TimeSpan> ProposedEnd { get; private set; }
        public bool IsValid { get { return proposedStart <= proposedEnd; } }


        public TimeRange(FieldSource<Tuple<TimeSpan, TimeSpan>> source)
        {
            proposedStart = source.Get().Item1;
            proposedEnd = source.Get().Item2;

            source.ValueChanged += (v) => { ProposedStart.Set(v.Item1); ProposedEnd.Set(v.Item2); };
            Action propositionChanged = () => { if (IsValid) source.Set(new Tuple<TimeSpan, TimeSpan>(proposedStart, proposedEnd)); };

            ProposedStart = new FieldSource<TimeSpan>(() => proposedStart, (t) => { proposedStart = t; propositionChanged(); ProposedEnd.PerformUpdate(); });
            ProposedEnd = new FieldSource<TimeSpan>(() => proposedEnd, (t) => { proposedEnd = t; propositionChanged(); ProposedStart.PerformUpdate(); });
        }
    }
}
