using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InterfaceParser
{
    /*
    public class TypePrototype
    {
        public string Name { get; }
        public string Language { get; }
        public TypeDefinition Definition { get; }

        public TypePrototype(string name, string lang, TypeDefinition definition)
        {
            Name = name;
            Language = lang;
            Definition = definition;
        }
    }
    */

    public abstract class Type
    {
        /// <summary>
        /// The generic name of this type. This is null for the root type.
        /// Examples: String, Array`1
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns the fully qualified name of this type. Null for the root type.
        /// </summary>
        public string FullName { get { return Name == null ? null : (Parent.FullName == null ? "" : Parent.FullName + ".") + Name; } }

        /// <summary>
        /// The language for which this type is valid.
        /// Null if it's valid for all languages.
        /// </summary>
        public string Language { get; }

        /// <summary>
        /// The type that contains this type.
        /// </summary>
        public Type Parent { get; }

        /// <summary>
        /// The root of the type hierarchy.
        /// </summary>
        public RootType Root { get; }

        /// <summary>
        /// All subtypes contained by this type.
        /// </summary>
        public Dictionary<string, List<Type>> SubTypes { get; } = new Dictionary<string, List<Type>>();

        public bool ValidForLanguage(string lang)
        {
            return Language == null ? true : Language == lang;
        }

        protected Type()
        {
            Name = null;
            Language = null;
            Parent = this;
            Root = (RootType)this;
        }

        public Type(string name, string lang, Type parent)
        {
            Name = name;
            Language = lang;
            Parent = parent;
            Root = parent.Root;
        }

        public override string ToString()
        {
            return FullName ?? "[global type]";
        }


        /// <summary>
        /// Returns the generic version of the first part of the name.
        /// </summary>
        /// <param name="name">Set to the remainder of the name. Null if this was a single part.</param>
        /// <param name="args">Set to an array that contains the generic arguments. Null if the first part was not a generic name</param>
        public static string ExtractFirstGenericName(ref string name, out string[] args)
        {
            StringBuilder currentName = new StringBuilder();
            int level = 0;
            List<StringBuilder> argList = null;

            int i = 0;
            for (i = 0; i < name.Length && !(level == 0 && name[i] == '.'); i++) {
                var c = name[i];
                switch (c) {
                    case '[':
                        if (level == 0) {
                            argList = new List<StringBuilder>();
                            argList.Add(new StringBuilder());
                            currentName.Append('`');
                        }
                        level++;
                        break;

                    case ',':
                        argList.Add(new StringBuilder());
                        break;

                    case ' ':
                        break;


                    case ']':
                        if (level == 1) {
                            currentName.Append(argList.Count().ToString());
                        }
                        level--;
                        break;

                    default:
                        if (level == 0)
                            currentName.Append(c);
                        else if (level == 1)
                            argList.Last().Append(c);
                        break;
                }
            }

            if (level != 0)
                throw new Exception(string.Format("unbalanced [] in typename \"{0}\"", name));

            name = i < name.Length ? name.Substring(i + 1) : null;
            args = argList == null ? null : argList.Select(arg => arg.ToString()).ToArray();
            return currentName.ToString();

        }

        /// <summary>
        /// Adds a subtype to this type (of one of it's subtypes) and initializes the added type.
        /// </summary>
        public Type AddType(string fullName, string lang, TypeDefinition definition = null)
        {
            string[] genericArgs;
            var currentName = ExtractFirstGenericName(ref fullName, out genericArgs);

            List<Type> list;
            if (!SubTypes.TryGetValue(currentName, out list))
                SubTypes[currentName] = list = new List<Type>();
            var subType = list.FirstOrDefault(c => c.ValidForLanguage(lang));

            if (fullName == null) {
                var t = new TypeWithDefinition(currentName, lang, definition, this);
                if (subType != null) {
                    if (!(subType is AutomaticallyGeneratedType))
                        throw new Exception(string.Format("The type \"{0}\" already exists in \"{1}\"", fullName, ToString()));
                    list.Remove(subType);
                    foreach (var child in subType.SubTypes)
                        t.SubTypes[child.Key] = child.Value;
                }
                list.Add(t);
                return t;

            } else {
                if (subType == null) {
                    subType = new AutomaticallyGeneratedType(currentName, lang, this);
                    list.Add(subType);
                }
                return subType.AddType(fullName, lang, definition);
            }
        }

        public IEnumerable<Type> GetHierarchy()
        {
            var t = this;
            do {
                yield return t;
                if (t is RootType)
                    break;
            } while ((t = t.Parent) != null);
        }

        /// <summary>
        /// Tries to look up the specified type in this namespace definition.
        /// Returns null if the name was not found.
        /// The name must be a single generic name element.
        /// </summary>
        public Type TryGetType(string name, string lang)
        {
            List<Type> list;
            if (!SubTypes.TryGetValue(name, out list))
                return null;
            return list.FirstOrDefault(t => t.ValidForLanguage(lang));
        }

        /// <summary>
        /// Returns all possible variants for the full type name of the specified name with respect to this type.
        /// The variants are ordered by precedence, that is longest variant first.
        /// </summary>
        private IEnumerable<string> GetTypeNameVariants(string name)
        {
            if (name.StartsWith("global::")) {
                name = name.Substring("global::".Length);
            } else {
                var currentPrefix = FullName;
                while (currentPrefix != "") {
                    yield return currentPrefix + "." + name;
                    currentPrefix = currentPrefix.Substring(0, Math.Max(currentPrefix.LastIndexOf('.'), 0));
                }
            }

            yield return name;
        }

        /// <summary>
        /// Resolves the specified name into a type.
        /// Generic arguments are bound.
        /// </summary>
        public Type ResolveType(string name, string lang)
        {
            foreach (var variant in GetTypeNameVariants(name)) {
                string remainingName = variant;
                string[] argList;
                Type type = Root;

                do {
                    var genericName = ExtractFirstGenericName(ref remainingName, out argList);
                    type = type.TryGetType(genericName, lang);

                    if (type != null && argList != null) {
                        var typeArgs = argList.Select(arg => ResolveType(arg, lang)).ToArray();
                        type = new GenericBinding(type, typeArgs);
                    }

                } while (remainingName != null && type != null);

                if (type == null)
                    continue;

                return type;
            }

            throw new Exception(string.Format("type \"{0}\" was not found in \"{1}\" or any of its roots", name, FullName));
        }
    }

    public class RootType : Type
    {
        public RootType()
            : base()
        {

        }
    }

    public class TypeWithDefinition : Type
    {
        public TypeDefinition Definition { get; }
        public TypeWithDefinition(string name, string lang, TypeDefinition definition, Type parent)
            : base(name, lang, parent)
        {
            Definition = definition;
        }
    }

    public class AutomaticallyGeneratedType : Type
    {
        public AutomaticallyGeneratedType(string name, string lang, Type parent)
            : base(name, lang, parent)
        {
        }
    }

    public class GenericBinding : Type
    {
        public Type GenericType { get; }
        public Type[] TypeArgs { get; }

        public GenericBinding(Type genericType, Type[] typeArgs)
            : base(genericType.Name, genericType.Language, genericType.Parent)
        {
            GenericType = genericType;
            TypeArgs = typeArgs;
        }
    }
}
