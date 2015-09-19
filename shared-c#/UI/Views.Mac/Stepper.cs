using System;
using System.Collections.Generic;
using System.Text;
using AppInstall.Framework;
using Foundation;
using UIKit;

namespace AppInstall.UI
{
    public class Stepper : View<UIStepper>
    {
        public event Action<double> ValueChanged;

        public double Value { get { return nativeView.Value; } set { nativeView.Value = value; } }
        public double Minimum { get { return nativeView.MinimumValue; } set { nativeView.MinimumValue = value; } }
        public double Maximum { get { return nativeView.MaximumValue; } set { nativeView.MaximumValue = value; } }
        public double StepSize { get { return nativeView.StepValue; } set { nativeView.StepValue = value; } }

        public Stepper()
        {
            Minimum = 0;
            Maximum = double.MaxValue;
            nativeView.ValueChanged += (o, e) => ValueChanged.SafeInvoke(Value);
        }
    }
}
