using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS
{
    public class ObjectConstraints
    {
        /// <summary>
        /// A dictionary with an array of possible values for each property.
        /// An array that is null means is equivalent to a wildcard, e.g. all values apply.
        /// </summary>
        public readonly Dictionary<string, object[]> properties;

        public ObjectConstraints(Dictionary<string, object[]> properties)
        {
            this.properties = properties;
        }

        public ObjectConstraints(ICustomAttributeProvider attributeProvider)
            : this(new Dictionary<string, object[]>())
        {
            foreach (var attr in attributeProvider.GetCustomAttributes(typeof(AOSObjectConstraintAttribute), false).Cast<AOSObjectConstraintAttribute>())
                properties[attr.PropertyName] = attr.Values;
        }

        public override string ToString()
        {
            return string.Join(", ", properties.Select(a => a.Key + ": " + a.Value));
        }
    }

    public class ObjectSet : DynamicSet<IObjectRef>
    {
        readonly ObjectConstraints constraints;

        public ObjectSet(ObjectConstraints constraints)
        {
            this.constraints = constraints;
        }

        protected override bool ShouldAdd(IObjectRef item)
        {
            return item.CompliesTo(constraints);
        }
    }
}
