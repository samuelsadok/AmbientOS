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
            var handlerAppearances = new ObjectAppearance[] { handler.GetAppearance() };
            return new ObjectAppearance[] { obj.GetAppearance() }.Select(set => {
                var dict = set.attributes.ToDictionary(a => "obj." + a.Key, a => a.Value);
                dict["input"] = obj.GetTypeName();
                return new ObjectAppearance(dict);
            }).Any(constraints => handlerAppearances.Any(appearance => appearance.CompliesTo(constraints)));
        }
    }
}