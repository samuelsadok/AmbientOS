using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{
    /// <summary>
    /// A view controller represents a manager of UI content of some type.
    /// This class can be overridden to enable different types of contents (e.g. a list, a set of editable fields, ...).
    /// The controller will generate a suitable view for the content on demand.
    /// The types of views (e.g. plain views, list view section, ...) that an overriding implementation must be able to generate is platform dependent.
    /// </summary>
    public abstract partial class ViewController
    {
        /// <summary>
        /// A title that describes the meaning of this view.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Same as Title but can change dynamically.
        /// If non-null, the Title field is ignored.
        /// </summary>
        public FieldSource<string> TitleSource { get; set; }

        /// <summary>
        /// A subtitle that elaborates the meaning or content of this view.
        /// </summary>
        public string Subtitle { get; set; }

        /// <summary>
        /// Same as Subtitle but can change dynamically.
        /// If non-null, the Subtitle field is ignored.
        /// </summary>
        public FieldSource<string> SubtitleSource { get; set; }

        /// <summary>
        /// A footer that displays additional info about this view.
        /// </summary>
        public string Footer { get; set; }

        /// <summary>
        /// Same as Footer but can change dynamically.
        /// If non-null, the Footer field is ignored.
        /// </summary>
        public FieldSource<string> FooterSource { get; set; }

        /// <summary>
        /// A path to the icon that represents this view
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// A list of custom features.
        /// These features add to the ones that are generated automatically for some view controllers.
        /// </summary>
        public List<FeatureController> Features { get; set; }

        public ViewController()
        {
            Features = new List<FeatureController>();
        }

        /// <summary>
        /// Enumerates all the features of the view controller.
        /// An overriding implenentation may include additional features (e.g. a list view controller may add an Add-button).
        /// Features can be buttons (add, edit, delete, submit, ...) or other
        /// things, such as an activity indicator.
        /// The mode and location of each feature depends heavily on platform, feature combination, etc.
        /// Each platform-specific view controller implementation should determine
        /// by itself, how the feature should be displayed.
        /// There shall not be any omitting of features.
        /// </summary>
        public virtual IEnumerable<FeatureController> GetFeatures()
        {
            if (Features != null)
                foreach (var feature in Features)
                    yield return feature;
        }
    }

    /// <summary>
    /// Represents a view controller with an underlying data source.
    /// This base class generalizes common properties that are associated with the availability of a data source.
    /// </summary>
    /// <typeparam name="T">A type derived from DataSource</typeparam>
    public abstract partial class DataViewController<T> : ViewController
        where T : DataSource
    {
        /// <summary>
        /// The underlying data
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// Text for the command that refreshes the associated data
        /// </summary>
        public string RefreshText { get; set; }

        /// <summary>
        /// Text to display while the associated data is refreshing
        /// </summary>
        public string RefreshingText { get; set; }

        /// <summary>
        /// Text for the command that submits the associated data
        /// </summary>
        public string SubmitText { get; set; }

        /// <summary>
        /// Text to display while the associated data is being submitted
        /// </summary>
        public string SubmittingText { get; set; }

        /// <summary>
        /// Generates the text that should be displayed in case of a successful refresh
        /// The argument indicates the time of completition.
        /// </summary>
        public Func<DateTime?, string> RefreshSuccessMessageFactory { get; set; }

        /// <summary>
        /// Generates the text that should be displayed in case of a refresh error
        /// </summary>
        public Func<Exception, string> RefreshErrorMessageFactory { get; set; }

        /// <summary>
        /// Generates the text that should be displayed in case of a successful submission.
        /// The argument indicates the time of completition.
        /// </summary>
        public Func<DateTime?, string> SubmissionSuccessMessageFactory { get; set; }

        /// <summary>
        /// Generates the text that should be displayed in case of a submit error
        /// </summary>
        public Func<Exception, string> SubmissionErrorMessageFactory { get; set; }

        /// <summary>
        /// The command that displays full exception details.
        /// </summary>
        public DialogFeature<Exception> ExceptionDisplayCommand { get; set; }


        /// <summary>
        /// Returns basic features that are common to all view controllers that have an underlying data source.
        /// </summary>
        public override IEnumerable<FeatureController> GetFeatures()
        {
            foreach (var feature in base.GetFeatures())
                yield return feature;

            if (Data != null) {
                if (Data.CanRefresh) {
                    yield return new ActivityFeature() {
                        Activity = () => Data.Refresh(ApplicationControl.ShutdownToken).Run(),
                        ActivityTracker = Data.RefreshTracker,
                        Text = RefreshText,
                        ActiveText = RefreshingText,
                        SuccessMessageFactory = RefreshSuccessMessageFactory,
                        ErrorMessageFactory = RefreshErrorMessageFactory,
                        ExceptionDisplayCommand = ExceptionDisplayCommand
                    };
                }

                if (Data.CanSubmit) {
                    yield return new ActivityFeature() {
                        Activity = () => Data.Submit(ApplicationControl.ShutdownToken).Run(),
                        ActivityTracker = Data.SubmitTracker,
                        Text = SubmitText,
                        ActiveText = SubmittingText,
                        SuccessMessageFactory = SubmissionSuccessMessageFactory,
                        ErrorMessageFactory = SubmissionErrorMessageFactory,
                        ExceptionDisplayCommand = ExceptionDisplayCommand
                    };
                }
            }
        }
    }
}
