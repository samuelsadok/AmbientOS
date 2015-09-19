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
    /// Base class for different types of commands that can be used on th UI.
    /// Platform specific code should use this base class to declare a common way of
    /// generating an appropriate UI element from the command.
    /// </summary>
    public abstract partial class FeatureController
    {
        /// <summary>
        /// The label of the command. For some commands on some platforms this may be ignored.
        /// </summary>
        public string Text { get; set; }
    }

    /// <summary>
    /// Represents a feature that provides some way of invoking an action.
    /// </summary>
    public abstract partial class InvokableFeature : FeatureController
    {
        /// <summary>
        /// Invokes the underlying action of this feature.
        /// </summary>
        public abstract void Invoke();
    }


    /// <summary>
    /// A command with custom a custom text and action.
    /// </summary>
    public partial class CustomFeature : InvokableFeature
    {
        /// <summary>
        /// The action to be invoked on triggering the command
        /// </summary>
        public Action Action { get; set; }

        public override void Invoke()
        {
            Action.SafeInvoke();
        }
    }



    /// <summary>
    /// A command that pops up a dialog that is connected to some data.
    /// Platform specific code must provide a constructor that sets up the command action.
    /// </summary>
    public partial class DialogFeature<T> : InvokableFeature
    {
        private Func<T> dataFactory;
        private T data;

        /// <summary>
        /// The data associated with the dialog.
        /// When first queried and the dataFactory is non-null, it is invoked to generate the result.
        /// </summary>
        public T Data
        {
            get
            {
                if (data == null && dataFactory != null)
                    data = dataFactory();
                return data;
            }
            set
            {
                if (dataFactory != null)
                    throw new InvalidOperationException("Data is read-only");
                data = value;
            }
        }

        /// <summary>
        /// Shall return the parent window of the dialog that should be shown.
        /// This is kept as a function so that the feature can be instantiated before the parent has been instantiated.
        /// </summary>
        public Func<Window> ParentConstructor { get; set; }

        /// <summary>
        /// This method shall generate a view controller that will displayed in the dialog.
        /// The argument is the Data associated with this command.
        /// </summary>
        public Func<T, ViewController> ViewConstructor { get; set; }

        /// <summary>
        /// The UI text for the command that is used to dismiss the dialog.
        /// todo: define "dismiss" (accept? reject?)
        /// </summary>
        public string DismissText { get; set; }

        /// <summary>
        /// The action to be invoked after the dialog has been dismissed.
        /// </summary>
        public Action<T> DismissAction { get; set; }

        public DialogFeature()
            : this(null)
        {
        }

        /// <param name="dataFactory">if not null, the Data property is read-only and data is generated from the factory at the time of displaying the dialog</param>
        public DialogFeature(Func<T> dataFactory)
        {
            this.dataFactory = dataFactory;
        }

        /// <summary>
        /// Invokes the show dialog action.
        /// </summary>
        public override void Invoke()
        {
            var view = ViewConstructor(Data);
            var diag = new Dialog(ParentConstructor(), view);
            view.Features.Add(new StandardFeature() {
                Action = () => {
                    diag.Close();
                    if (DismissAction != null)
                        DismissAction(Data);
                },
                Text = DismissText,
                Type = StandardFeature.StandardCommandType.Done
            });
            diag.Show();
        }
    }

    public partial class ActivityFeature : InvokableFeature
    {
        public string ActiveText { get; set; }
        public Func<DateTime?, string> SuccessMessageFactory { get; set; }
        public Func<Exception, string> ErrorMessageFactory { get; set; }

        /// <summary>
        /// The feature that displays full exception details.
        /// </summary>
        public DialogFeature<Exception> ExceptionDisplayCommand { get; set; }

        public Action Activity { get; set; }
        public ActivityTracker ActivityTracker { get; set; }


        public override void Invoke()
        {
            Activity.SafeInvoke();
        }
    }


    public partial class StandardFeature : InvokableFeature
    {
        public Action Action { get; set; }
        public StandardCommandType Type { get; set; }

        public enum StandardCommandType
        {
            Add,
            Edit,
            Delete,
            Done
        }

        public override void Invoke()
        {
            Action.SafeInvoke();
        }
    }


    public class FeatureList
    {
        public IEnumerable<FeatureController> Features;

        public FeatureList(IEnumerable<FeatureController> features)
        {
            Features = features;
        }

        public void AssertEmpty()
        {
            if (Features.Any())
                throw new NotImplementedException(Features.Count() + " features could not be displayed: " + string.Join(", ", Features.Select((feature) => feature.ToString())));
        }
    }
}
