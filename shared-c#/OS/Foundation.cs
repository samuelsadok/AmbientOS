using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace AppInstall.OS
{

    public enum PlatformType {

        /// <summary>
        /// The platform could not be determined (why on earth would this ever happen?)
        /// </summary>
        Unknown,

        /// <summary>
        /// Our concept OS
        /// </summary>
        AmbientOS,

        /// <summary>
        /// The application is running on Windows.
        /// This includes desktop, universal and console apps and services.
        /// </summary>
        Windows,
        
        /// <summary>
        /// The application is running on Linux (other than Android)
        /// </summary>
        Linux,

        /// <summary>
        /// The application is running on Mac OSX.
        /// </summary>
        OSX,

        /// <summary>
        /// The application is running on iOS.
        /// </summary>
        iOS,

        /// <summary>
        /// The application is running on Android.
        /// </summary>
        Android

    }

}