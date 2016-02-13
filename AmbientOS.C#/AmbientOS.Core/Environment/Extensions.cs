using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.Environment
{
    public static class Extensions
    {
        /// <summary>
        /// Indicates whether the action can handle the specified object.
        /// </summary>
        public static bool CanHandle(this IAction handler, IObjectRef obj)
        {
            return handler.CompliesTo(obj.GetHandlerConstraints());
        }
    }
}