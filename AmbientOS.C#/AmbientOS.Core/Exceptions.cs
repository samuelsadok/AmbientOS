using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS
{
    /// <summary>
    /// Thrown by a service when it finds that it cannot operate object it was given.
    /// If this is an action such as open with no specific application, the next compatible application is used.
    /// If the user specifies a specific application and it cannot use the object, the user is notified.
    /// </summary>
    public class AOSRejectException : Exception
    {
        public AOSRejectException(string message, IObjectRef obj)
            : base(obj + " is not compatible: " + message)
        {
        }
    }

    /// <summary>
    /// Thrown when no suitable application was found that could execute the specified action on the specifide object.
    /// </summary>
    public class AOSAppNotFoundException : Exception
    {
        public AOSAppNotFoundException(string action, IObjectRef obj)
            : base("No application was found that could " + action + " " + obj)
        {
        }

        public AOSAppNotFoundException(string action, IObjectRef obj, IEnumerable<AOSRejectException> rejections)
            : base("None of the available applications could " + action + " " + obj, new AggregateException(rejections))
        {
        }
    }

    public class AOSLockException : Exception
    {
        public AOSLockException(bool exclusive, IObjectRef obj)
            : base("Failed to aquire exclusive access rights to " + obj)
        {
        }
    }
}
