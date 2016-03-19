using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmbientOS.Platform;
using AmbientOS.UI;
using AmbientOS.Environment;
using AmbientOS.Utils;

namespace AmbientOS.Platform
{
    public class iOSPlatform : IPlatform
    {
        public Text Text { get; } = new Text() { Summary = "Windows System Service", Details = "The application is started when the computer starts and runs in the background, even when no user is logged in." };

        public static IPlatform CreateInstance(LaunchMode launchMode)
        {
            return null;
        }

        public Context Init(string[] args, DynamicProperty<string> name, DynamicProperty<string> description, Context preContext)
        {
            return null;
        }

        public void Install(string[] args, DynamicProperty<string> name, DynamicProperty<string> description, Context context)
        {
        }

        public void Uninstall(string[] args, DynamicProperty<string> name, DynamicProperty<string> description, Context context)
        {
        }
    }

}
