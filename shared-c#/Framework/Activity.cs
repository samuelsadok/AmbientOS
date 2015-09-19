using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppInstall.Framework
{
    public enum ActivityStatus {
        Inactive,
        Active,
        Paused,
        Failed,
        Succeeded
    }

    /// <summary>
    /// Tracks the status of an activity by exposing events that trigger on any status change.
    /// </summary>
    public class ActivityTracker
    {
        private ActivityStatus status = ActivityStatus.Inactive;
        public ActivityStatus Status
        {
            get { return status; }
            private set { StatusChanged.SafeInvoke(this, status = value); }
        }

        public event EventHandler<ActivityStatus> StatusChanged;

        public bool IsActive { get { return Status == ActivityStatus.Active || Status == ActivityStatus.Paused; } }

        /// <summary>
        /// Indicates the last time the activity completed successfully.
        /// Returns null if the operation never succeeded before.
        /// </summary>
        public DateTime? LastSuccess { get; private set; }

        public Exception LastException { get; private set; }

        public void SwitchToActive()
        {
            LastException = null;
            Status = ActivityStatus.Active;
        }

        public void SwitchToPaused()
        {
            Status = ActivityStatus.Paused;
        }

        public void SwitchToFailed(Exception ex)
        {
            LastException = ex;
            Status = ActivityStatus.Failed;
        }

        public void SwitchToSucceeded()
        {
            LastException = null;
            if (Status != ActivityStatus.Succeeded)
                LastSuccess = DateTime.Now;
            Status = ActivityStatus.Succeeded;
        }
    }

    /// <summary>
    /// Monitors multiple activities.
    /// Shows as running if any of the child inner activities are running.
    /// Else, the last child activity that failed, succeeded or was paused, defines
    /// the status of the aggregate activity.
    /// </summary>
    public class AggregateActivity : ActivityTracker
    {
        public ActivityTracker LastSuspendedChild { get; private set; }
        public ActivityTracker LastFailedChild { get; private set; }

        public AggregateActivity(params ActivityTracker[] activities) {
            var validActivities = activities.Where((a) => a != null);
            if (!validActivities.Any()) return; // if we don't have any child activities, do nothing

            LastSuspendedChild = validActivities.First();
            LastFailedChild = validActivities.FirstOrDefault(a => a.Status == ActivityStatus.Failed);

            Action updateStatus = () => {
                var running = validActivities.Where((a) => a.Status == ActivityStatus.Active);
                var paused = validActivities.Where((a) => a.Status == ActivityStatus.Paused);
                if (running.Any()) {
                    SwitchToActive();
                } else if (LastSuspendedChild != null) {
                    if (LastSuspendedChild.Status == ActivityStatus.Paused)
                        SwitchToPaused();
                    else if (LastSuspendedChild.Status == ActivityStatus.Succeeded)
                        SwitchToSucceeded();
                    else if (LastSuspendedChild.Status == ActivityStatus.Failed)
                        SwitchToFailed(LastSuspendedChild.LastException);
                }
            };

            foreach (var activity in validActivities)
                activity.StatusChanged += (o, e) => {
                    var child = (ActivityTracker)o;
                    if (e != ActivityStatus.Active)
                        LastSuspendedChild = child;
                    if (e == ActivityStatus.Failed)
                        LastFailedChild = child;
                    updateStatus();
                };

            updateStatus();
        }
    }
}
