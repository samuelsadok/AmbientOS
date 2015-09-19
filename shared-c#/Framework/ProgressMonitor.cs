using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace AppInstall.Framework
{
    public abstract class ProgressMonitor {
        private DateTime lastEventFire = new DateTime(0);

        /// <summary>
        /// Triggered when progess was registered
        /// </summary>
        public event EventHandler<double> ProgressChanged;


        /// <summary>
        /// The minimum progress that has to be made for another notification to be triggered.
        /// Upon reaching 100% the notification is triggered exactly once regardless of the minimum step size
        /// </summary>
        public double MinimumStep { get; set; }

        /// <summary>
        /// Returns the current progress (0 - 1)
        /// </summary>
        public abstract double Progress { get; }

        private double lastProgress = 0;

        public ProgressMonitor()
        {
        }


        /// <summary>
        /// Called by inheriting classes to raise the ProgessChanged event.
        /// todo: limit to one call per minute
        /// </summary>
        protected void RegisterProgessChange()
        {
            double p;
            lock (this) {
                if (this.Progress < lastProgress + MinimumStep)
                    if ((this.Progress != 1) || (lastProgress == 1))
                        return;

                lastProgress = this.Progress;
                p = this.Progress;
            }
            ProgressChanged.SafeInvoke(this, p);
        }
    }

    /// <summary>
    /// Represents an operation of a fixed known size.
    /// </summary>
    public class LinearProgressMonitor : ProgressMonitor
    {
        private int progress = 0;
        private int maximumProgress;

        public override double Progress { get { return (double)progress / (double)maximumProgress; } }

        /// <summary>
        /// Creates a LinearProgressMonitor with the specified maximum progress value
        /// </summary>
        public LinearProgressMonitor(int maximumProgress)
        {
            this.maximumProgress = maximumProgress;
        }

        /// <summary>
        /// Registers progress
        /// </summary>
        public void Advance(int points)
        {
            lock (this) {
                progress += points;
                RegisterProgessChange();
            }
        }

        /// <summary>
        /// Sets the progress to the maximum value
        /// </summary>
        public void Complete()
        {
            lock (this) {
                progress = maximumProgress;
                RegisterProgessChange();
            }
        }
    }

    /// <summary>
    /// Represents an operation that consists of several suboperations
    /// </summary>
    public class MultiStageProgressMonitor : ProgressMonitor
    {
        private Tuple<ProgressMonitor, double>[] stages;
        private readonly double weightSum = 0;

        public override double Progress
        {
            get
            {
                double p = 0;
                foreach (var pm in stages)
                    p += pm.Item1.Progress * pm.Item2;
                return p / weightSum;
            }
        }

        /// <summary>
        /// Creates a progess monitor that represents several smaller operations. 
        /// </summary>
        /// <param name="stages">A list of all progess monitors and their relative weights</param>
        public MultiStageProgressMonitor(params Tuple<ProgressMonitor, double>[] stages)
        {
            this.stages = stages;
            foreach (var pm in stages) {
                pm.Item1.ProgressChanged += (o, e) => RegisterProgessChange();
                weightSum += pm.Item2;
            }
            if (weightSum == 0)
                throw new ArgumentNullException("stages => weight", "the sum of all weights must not be zero");
        }
    }

    public class ProgressObserver : ProgressMonitor
    {
        private ProgressMonitor monitor;
        public ProgressMonitor Monitor
        {
            get
            {
                lock (this)
                    return monitor;
            }
            set
            {
                lock (this) {
                    value.ProgressChanged += (o, e) => RegisterProgessChange();
                    monitor = value;
                }
                RegisterProgessChange();
            }
        }


        private string status;
        public string Status
        {
            get
            {
                lock (this)
                    return status;
            }
            set
            {
                lock (this) {
                    status = value;
                    StatusChanged.SafeInvoke(this, value);
                }
            }
        }

        public event EventHandler<string> StatusChanged;

        public override double Progress { get { return (Monitor == null ? 0 : Monitor.Progress); } }
    }
}