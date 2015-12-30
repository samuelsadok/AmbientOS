using System;

namespace AmbientOS
{
    /// <summary>
    /// Identifies different platforms.
    /// This may be useful, for instance, to display platform specific help texts.
    /// </summary>
    public enum PlatformType {

        /// <summary>
        /// The platform is unspecified.
        /// This may be the case in platform independent environments.
        /// </summary>
        Unspecified,

        /// <summary>
        /// The application is running on a native AmbientOS kernel.
        /// </summary>
        AmbientOS,

        /// <summary>
        /// The application is running on Windows.
        /// This includes desktop, universal and console apps and services.
        /// </summary>
        Windows,
        
        /// <summary>
        /// The application is running on a Linux distribution other than Android.
        /// </summary>
        Linux,

        /// <summary>
        /// The application is running on Mac OS X.
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