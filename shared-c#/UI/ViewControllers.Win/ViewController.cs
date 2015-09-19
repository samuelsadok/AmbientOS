using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{

    public abstract partial class ViewController
    {
        protected abstract View ConstructViewEx();
        public View ConstructView()
        {
            return Platform.EvaluateOnMainThread(ConstructViewEx);
        }


        /*
        protected View AddFeatures(View view, DataSource data, FeatureController[] commands)
        {
            var fullGrid = new GridLayout(2, 1);


            // add any automatically generated commands to this list
            List<FeatureController> autoCommands = new List<FeatureController>();

            LayerLayout supplementaryInfo = new LayerLayout();

            if (data != null) {
                AggregateActivity activity = new AggregateActivity(data.RefreshTracker, data.SubmitTracker);
                ActivityIndicator activityIndicator = new ActivityIndicator();
                Label exceptionLabel = new Label() { TextColor = Color.Red };
                if (ExceptionDisplayCommand != null)
                    exceptionLabel.Selected += () => { ExceptionDisplayCommand.Data = activity.LastException; ExceptionDisplayCommand.Invoke(); };

                Action updateStatus = () => {
                    if (activity.Status == ActivityStatus.Active) {
                        fullGrid.Enabled = !(activityIndicator.Active = true);
                        supplementaryInfo.Replace(activityIndicator);
                    } else if (activity.Status == ActivityStatus.Failed) {
                        if (activity.LastSuspendedChild == data.RefreshTracker)
                            exceptionLabel.Text = RefreshErrorMessageFactory(activity.LastException);
                        else
                            exceptionLabel.Text = SubmissionErrorMessageFactory(activity.LastException);
                        fullGrid.Enabled = !(activityIndicator.Active = false);
                        supplementaryInfo.Replace(exceptionLabel);
                    } else {
                        fullGrid.Enabled = !(activityIndicator.Active = false);
                        supplementaryInfo.Remove(activityIndicator, false, duration: 0);
                    }
                };

                activity.StatusChanged += (o, e) => Platform.InvokeMainThread(() => updateStatus());

                updateStatus();

                if (data.CanRefresh)
                    autoCommands.Add(new CustomFeature() { Text = RefreshText, Action = () => data.Refresh(ApplicationControl.ShutdownToken).Run() });
                if (data.CanSubmit)
                    autoCommands.Add(new CustomFeature() { Text = SubmitText, Action = () => data.Submit(ApplicationControl.ShutdownToken).Run() });
            }


            var toolBar = new GridLayout(1, autoCommands.Count() + commands.Count() + 1);
            toolBar[0, 0] = supplementaryInfo;
            toolBar.RelativeRowHeights[0] = 1f;
            toolBar.RelativeColumnWidths[0] = 1f;
            for (int i = 0; i < autoCommands.Count(); i++)
                toolBar[0, i + 1] = autoCommands[i].ConstructButton();
            for (int i = 0; i < commands.Count(); i++)
                toolBar[0, i + 1 + autoCommands.Count()] = commands[i].ConstructButton();

            fullGrid.RelativeRowHeights[0] = 1f;
            fullGrid.RelativeColumnWidths[0] = 1f;
            fullGrid[0, 0] = view;
            fullGrid[1, 0] = toolBar;

            return fullGrid;
        }
         */

        protected View AddFeatures(View view, FeatureList features)
        {
            // extract the features that can be displayed
            var f = features.Features.ToArray();
            var fixedFeatures = f.Where(feature => (feature.SupportedModes & FeatureController.DisplayMode.BottomSimple) != FeatureController.DisplayMode.None).ToArray();
            var flexFeatures = f.Where(feature => (feature.SupportedModes & FeatureController.DisplayMode.BottomAggregate) != FeatureController.DisplayMode.None).ToArray();
            features.Features = f.Except(fixedFeatures.Concat(flexFeatures)).ToArray();

            if (!fixedFeatures.Any() && !flexFeatures.Any())
                return view;

            
            var fullGrid = new GridLayout(2, 1);

            // flexible features are aggregated by their type
            var flexGroups = flexFeatures.GroupBy(feature => feature.GetType()).ToArray();

            var bottomBar = new GridLayout(1, Math.Min(flexGroups.Count(), 1) + fixedFeatures.Count());
            bottomBar.RelativeRowHeights[0] = 1f;
            bottomBar.RelativeColumnWidths[0] = 1f; // a filling cell is always inserted

            // add aggregate views to bottom bar
            for (int i = 0; i < flexGroups.Count(); i++) {
                bottomBar[0, i] = flexGroups[i].First().ConstructAggregateView(fullGrid, flexGroups[i]);
                bottomBar.RelativeColumnWidths[i] = 1f;
            }

            // add simple views to the bottom bar
            for (int i = 0; i < fixedFeatures.Count(); i++)
                bottomBar[0, i + flexGroups.Count()] = fixedFeatures[i].ConstructSimpleView(fullGrid);

            fullGrid.RelativeRowHeights[0] = 1f;
            fullGrid.RelativeColumnWidths[0] = 1f;
            fullGrid[0, 0] = view;
            fullGrid[1, 0] = bottomBar;

            return fullGrid;
        }
    }
}
