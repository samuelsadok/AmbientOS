using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmbientOS.UI;

namespace AmbientOS.Platform
{
    /// <summary>
    /// To implement a platform specific environment, a platform specific assembly should contain a public type that implements this interface.
    /// The type should contain a parameterless constructor and should throw an exception when executed on the wrong platform.
    /// </summary>
    public interface IPlatform
    {
        Text Text { get; }
        
        Context Init(string[] args, string name, string description, Context preContext);
        void Install(string[] args, string name, string description, Context context);
        void Uninstall(string[] args, string name, string description, Context context);
    }

    public enum LaunchMode
    {
        Launch,
        Install,
        Uninstall
    }
}
