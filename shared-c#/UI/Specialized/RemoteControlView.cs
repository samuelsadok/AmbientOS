using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppInstall.Framework;

namespace AppInstall.UI
{

    class RemoteControlView : GridLayout
    {
        private const float MAX_THROTTLE_WAY_RATIO = 0.6f; // part of the view height corresponding to max throttle
        private const float MAX_THROTTLE_WAY_DISTANCE = 250; // points of the view height corresponding to max throttle

        public event EventHandler<float> ThrottleChanged;
        public event Action<RemoteControlView> ConfigurationChanged;
        public event Action<RemoteControlView> SupplementaryAction;

        private float throttle;
        public float Throttle
        {
            get
            {
                return throttle;
            }
            private set
            {
                throttle = Math.Max(0, Math.Min(1, value));
                ThrottleChanged.SafeInvoke(this, throttle);
                throttleLabel.Text = Math.Round(throttle * 1000) / 10 + "%";
                UpdateLayout();
            }
        }
        public float P { get { return sliderP.Value; } set { sliderP.Value = value; } }
        public float I { get { return sliderI.Value; } set { sliderI.Value = value; } }
        public float D { get { return sliderD.Value; } set { sliderD.Value = value; } }
        public float ILimit { get { return sliderL.Value; } set { sliderL.Value = value; } }
        public float A { get { return sliderA.Value; } set { sliderA.Value = value; } }
        public float T { get { return sliderT.Value; } set { sliderT.Value = value; } }

        
        Label tagLabelP = new Label() { Text = "P:" };
        Label tagLabelI = new Label() { Text = "I:" };
        Label tagLabelD = new Label() { Text = "D:" };
        Label tagLabelL = new Label() { Text = "max I:" };
        Label tagLabelA = new Label() { Text = "a:" };
        Label tagLabelT = new Label() { Text = "t:" };
        Label valLabelP = new Label() { Text = "", SizeSampleText = "99.9%", TextAlignment = UI.TextAlignment.Right };
        Label valLabelI = new Label() { Text = "", SizeSampleText = "99.9%", TextAlignment = UI.TextAlignment.Right };
        Label valLabelD = new Label() { Text = "", SizeSampleText = "99.9%", TextAlignment = UI.TextAlignment.Right };
        Label valLabelL = new Label() { Text = "", SizeSampleText = "9.999", TextAlignment = UI.TextAlignment.Right };
        Label valLabelA = new Label() { Text = "", SizeSampleText = "999", TextAlignment = UI.TextAlignment.Right };
        Label valLabelT = new Label() { Text = "", SizeSampleText = "999ms", TextAlignment = UI.TextAlignment.Right };
        Slider sliderP = new Slider() { MinValue = 0f, MaxValue = 1.0f, Margin = new Margin(10f, 3f) };
        Slider sliderI = new Slider() { MinValue = 0f, MaxValue = 2.0f, Margin = new Margin(10f, 3f) };
        Slider sliderD = new Slider() { MinValue = 0f, MaxValue = 0.2f, Margin = new Margin(10f, 3f) };
        Slider sliderL = new Slider() { MinValue = 0f, MaxValue = 5000f, Margin =new Margin(10f, 3f) };
        Slider sliderA = new Slider() { MinValue = 0f, MaxValue = 255f, Margin = new Margin(10f, 3f) };
        Slider sliderT = new Slider() { MinValue = 0f, MaxValue = 0.765f, Margin=new Margin(10f, 3f) };
        Button button = new Button() { Text = "test" };


        Label throttleLabel = new Label() {
            Text = "100%",
            TextAlignment = UI.TextAlignment.Center
        };

        public RemoteControlView()
            : base(3, 1)
        {
            Action<Label, float, int> updateSlider = (label, value, mode) => {
                switch (mode) {
                    case 0: label.Text = string.Format("{0:N0}", value); break;
                    case 1: label.Text = string.Format("{0:N" + (value >= 1 ? "0" : "1") + "}%", value * 100); break;
                    case 2: label.Text = string.Format("{0:N0}ms", value * 1000); break;
                }
                ConfigurationChanged.SafeInvoke(this);
            };

            sliderP.ValueChanged += (o, e) => updateSlider(valLabelP, e, 1);
            sliderI.ValueChanged += (o, e) => updateSlider(valLabelI, e, 1);
            sliderD.ValueChanged += (o, e) => updateSlider(valLabelD, e, 1);
            sliderL.ValueChanged += (o, e) => updateSlider(valLabelL, e, 0);
            sliderA.ValueChanged += (o, e) => updateSlider(valLabelA, e, 0);
            sliderT.ValueChanged += (o, e) => updateSlider(valLabelT, e, 2);

            button.Triggered += (o) => SupplementaryAction.SafeInvoke(this);

            GridLayout grid = new GridLayout(6, 3);
            grid[0, 0] = tagLabelP; grid[0, 1] = sliderP; grid[0, 2] = valLabelP;
            grid[1, 0] = tagLabelI; grid[1, 1] = sliderI; grid[1, 2] = valLabelI;
            grid[2, 0] = tagLabelD; grid[2, 1] = sliderD; grid[2, 2] = valLabelD;
            grid[3, 0] = tagLabelL; grid[3, 1] = sliderL; grid[3, 2] = valLabelL;
            grid[4, 0] = tagLabelA; grid[4, 1] = sliderA; grid[4, 2] = valLabelA;
            grid[5, 0] = tagLabelT; grid[5, 1] = sliderT; grid[5, 2] = valLabelT;
            grid.RelativeColumnWidths[1] = 1; // slider column is flexible, rest is autosized

            RelativeRowHeights[2] = 1;
            this[0, 0] = grid;
            this[1, 0] = button;
            this[2, 0] = throttleLabel;


            this.nativeView.AddGestureRecognizer(new UIKit.UIPanGestureRecognizer(recognizer => {
                //LogSystem.Log("state: "  + recognizer.State.ToString());
                switch (recognizer.State) {
                    case UIKit.UIGestureRecognizerState.Began:
                    case UIKit.UIGestureRecognizerState.Changed:
                        Throttle = -((float)recognizer.TranslationInView(nativeView).Y) / Math.Min((float)nativeView.Bounds.Height * MAX_THROTTLE_WAY_RATIO, MAX_THROTTLE_WAY_DISTANCE);
                        break;
                    default:
                        Application.UILog.Log("pan: " + recognizer.State.ToString());
                        Throttle = 0;
                        break;
                }
            }));
        }
    }
}