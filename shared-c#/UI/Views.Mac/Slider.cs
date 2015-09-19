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
    public class Slider : View<UISlider>
    {
        private const float MIN_SLIDER_HEIGHT = 28f; // starting iOS 7
        private const float MIN_SLIDER_WIDTH = 2 * MIN_SLIDER_HEIGHT;

        public event EventHandler<float> ValueChanged;

        public float MaxValue { get { return nativeView.MaxValue; } set { nativeView.MaxValue = value; } }
        public float MinValue { get { return nativeView.MinValue; } set { nativeView.MinValue = value; } }
        public float Value { get { return nativeView.Value; } set { nativeView.Value = value; } }

        public Slider()
        {
            nativeView.Continuous = true;
            nativeView.ValueChanged += (o, e) => {
                ValueChanged.SafeInvoke(this, Value);
            };
        }

        protected override Vector2D<float> GetContentSize(Vector2D<float> maxSize)
        {
            return new Vector2D<float>(MIN_SLIDER_WIDTH, MIN_SLIDER_HEIGHT);
        }
    }
}