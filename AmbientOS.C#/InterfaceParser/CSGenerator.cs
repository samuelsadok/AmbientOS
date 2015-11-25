using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InterfaceParser
{
    public static class CSGenerator
    {
        private const string DEFAULT_CS_INDENT = "    ";
        private const string DEFAULT_COMMENT_INDENT = "/// ";


        public enum Variant
        {
            Interface,
            Implementation,
            Reference,

            LocalCall,
            RemoteCall,
            BaseCall
        }


        public static string GenerateCS(this Type type, Type ns)
        {
            var nsPrefix = "";
            if (!ns.GetHierarchy().Contains(type.Parent))
                nsPrefix = type.Parent.GenerateCS(ns) + ".";

            if (type is TypeWithDefinition) {
                var def = ((TypeWithDefinition)type).Definition;
                var prefix = (def is InterfaceDefinition) ? "I" : "";
                return nsPrefix + prefix + (def == null ? type.Name : def.GetTypeName(null, ResolveTypeNameCS));

            } else if (type is GenericBinding) {
                var genericType = ((GenericBinding)type).GenericType as TypeWithDefinition;

                if (genericType == null)
                    throw new Exception("there is no definition associated with the generic type of " + type);

                var genericArgs = ((GenericBinding)type).TypeArgs.Select(t => t.GenerateCS(ns)).ToArray();
                var prefix = (genericType.Definition is InterfaceDefinition) ? "I" : "";
                return nsPrefix + prefix + genericType.Definition.GetTypeName(genericArgs, ResolveTypeNameCS);

            } else {
                throw new Exception(string.Format("cannot generate C# code for type \"{0}\" of type type {1}", type.ToString(), type.GetType()));
            }
        }

        /// <summary>
        /// Returns true if the type is reference counted or contains a field that is reference counted.
        /// </summary>
        public static bool IsRefCounted(this Type type)
        {
            if (type == null)
                throw new ArgumentNullException($"{type}");

            if (type is TypeWithDefinition) {
                var def = ((TypeWithDefinition)type).Definition;

                if (def is InterfaceDefinition)
                    return true;
                else if (def is BareTypeDefinition)
                    return false;
                else if (def is AliasDefinition)
                    return ((AliasDefinition)def).Alias.ResolveTypeCS(def.Namespace).IsRefCounted();
                else if (def is EnumDefinition)
                    return false;
                else if (def is StructDefinition)
                    return ((StructDefinition)def).Fields.Any(field => field.FieldType.ResolveTypeCS(field.Namespace).IsRefCounted());
                else
                    throw new Exception(string.Format("cannot determine if definition of type {0} is reference counted", def.GetType()));

            } else if (type is GenericBinding) {
                return ((GenericBinding)type).GenericType.IsRefCounted();

            } else {
                throw new Exception(string.Format("cannot determine if type \"{0}\" is reference counted", type.ToString()));
            }
        }

        public static Type ResolveTypeCS(this string typeName, NamespaceDefinition ns)
        {
            return ns.Type.ResolveType(typeName, "C#");
        }

        public static string ResolveTypeNameCS(this string typeName, NamespaceDefinition ns)
        {
            if (typeName == "void")
                return "void";
            return typeName.ResolveTypeCS(ns).GenerateCS(ns.Type);
        }

        public static void GenerateSummary(this Definition def, string indent, StringBuilder builder)
        {
            if (def.Summary == null)
                return;

            builder.AppendLine(indent + "<summary>");

            var s = new System.IO.StringReader(def.Summary);
            int skippedEmptyLines = -1;
            for (var line = ""; line != null; line = s.ReadLine()) {
                if (string.IsNullOrWhiteSpace(line)) {
                    if (skippedEmptyLines != -1)
                        skippedEmptyLines++;
                } else {
                    for (int i = 0; i < Math.Max(skippedEmptyLines, 0); i++)
                        builder.AppendLine(indent);
                    skippedEmptyLines = 0;
                    builder.AppendLine(indent + line.Trim());
                }
            }
            builder.AppendLine(indent + "</summary>");
        }

        public static void GenerateCS(this NamespaceDefinition def, string indent, StringBuilder builder)
        {
            def.GenerateSummary(indent + DEFAULT_COMMENT_INDENT, builder);
            builder.AppendLine(indent + "namespace " + def.Name);
            builder.AppendLine(indent + "{");

            bool first = true;
            foreach (var child in def.Children) {
                if (!first)
                    builder.AppendLine();
                first = false;

                if (child is NamespaceDefinition)
                    (child as NamespaceDefinition).GenerateCS(indent + DEFAULT_CS_INDENT, builder);
                else if (child is InterfaceDefinition)
                    (child as InterfaceDefinition).GenerateCS(indent + DEFAULT_CS_INDENT, builder);
                else if (child is BareTypeDefinition)
                    (child as BareTypeDefinition).GenerateCS(indent + DEFAULT_CS_INDENT, builder, ref first);
                else if (child is AliasDefinition)
                    (child as AliasDefinition).GenerateCS(indent + DEFAULT_CS_INDENT, builder, ref first);
                else if (child is StructDefinition)
                    (child as StructDefinition).GenerateCS(indent + DEFAULT_CS_INDENT, builder);
                else if (child is EnumDefinition)
                    (child as EnumDefinition).GenerateCS(indent + DEFAULT_CS_INDENT, builder);
                else
                    throw new Exception(string.Format("cannot generate C# code for {0}", child.GetType()));
            }

            builder.AppendLine(indent + "}");
        }

        public static void GenerateCS(this BareTypeDefinition def, string indent, StringBuilder builder, ref bool emptyLine)
        {
            emptyLine = true;
        }

        public static void GenerateCS(this AliasDefinition def, string indent, StringBuilder builder, ref bool emptyLine)
        {
            emptyLine = true;
        }

        public static void GenerateCS(this StructDefinition def, string indent, StringBuilder builder)
        {
            def.GenerateSummary(indent + DEFAULT_COMMENT_INDENT, builder);

            var refCounted = def.Type.IsRefCounted();
            builder.AppendLine(indent + "public class " + def.Name + (refCounted ? " : IRefCounted" : ""));
            builder.AppendLine(indent + "{");

            var retainList = new StringBuilder();
            var releaseList = new StringBuilder();

            bool first = true;
            foreach (var child in def.Fields) {
                if (!first)
                    builder.AppendLine();
                first = false;

                child.GenerateCS(indent + DEFAULT_CS_INDENT, builder, retainList, releaseList);
            }

            if (refCounted) {
                builder.AppendLine();
                builder.AppendLine(indent + DEFAULT_CS_INDENT + "void IRefCounted.Alloc()");
                builder.AppendLine(indent + DEFAULT_CS_INDENT + "{");
                builder.Append(retainList.ToString());
                builder.AppendLine(indent + DEFAULT_CS_INDENT + "}");
                builder.AppendLine();
                builder.AppendLine(indent + DEFAULT_CS_INDENT + "void IRefCounted.Free()");
                builder.AppendLine(indent + DEFAULT_CS_INDENT + "{");
                builder.Append(releaseList.ToString());
                builder.AppendLine(indent + DEFAULT_CS_INDENT + "}");
                builder.AppendLine();
                builder.AppendLine(indent + DEFAULT_CS_INDENT + "void IDisposable.Dispose()");
                builder.AppendLine(indent + DEFAULT_CS_INDENT + "{");
                builder.AppendLine(indent + DEFAULT_CS_INDENT + DEFAULT_CS_INDENT + "this.Release();");
                builder.AppendLine(indent + DEFAULT_CS_INDENT + "}");
            }

            builder.AppendLine(indent + "}");
        }

        public static void GenerateCS(this FieldDefinition def, string indent, StringBuilder builder, StringBuilder retainList, StringBuilder releaseList)
        {
            def.GenerateSummary(indent + DEFAULT_COMMENT_INDENT, builder);
            var refCounted = def.FieldType.ResolveTypeCS(def.Namespace).IsRefCounted();
            if (refCounted) {
                builder.AppendLine(indent + string.Format("public {1} {0} {{ get {{ return _{0}; }} set {{ _{0}?.Release(); _{0} = value?.Retain(); }} }}", def.Name, def.FieldType.ResolveTypeNameCS(def.Namespace)));
                retainList.AppendLine(indent + DEFAULT_CS_INDENT + "_" + def.Name + ".Retain();");
                releaseList.AppendLine(indent + DEFAULT_CS_INDENT + "_" + def.Name + ".Release();");
            }
            builder.AppendLine(indent + "public " + def.FieldType.ResolveTypeNameCS(def.Namespace) + " " + (refCounted ? "_" : "") + def.Name + ";");
        }

        public static void GenerateCS(this EnumDefinition def, string indent, StringBuilder builder)
        {
            def.GenerateSummary(indent + DEFAULT_COMMENT_INDENT, builder);
            builder.AppendLine(indent + "public enum " + def.Name);
            builder.Append(indent + "{");

            bool first = true;
            foreach (var child in def.Values) {
                if (!first)
                    builder.AppendLine(",");
                first = false;
                builder.AppendLine();
                child.GenerateCS(indent + DEFAULT_CS_INDENT, builder);
            }

            builder.AppendLine();
            builder.AppendLine(indent + "}");
        }

        public static void GenerateCS(this ValueDefinition def, string indent, StringBuilder builder)
        {
            def.GenerateSummary(indent + DEFAULT_COMMENT_INDENT, builder);
            builder.Append(indent + def.Name + (def.Value == null ? "" : " = " + def.Value));
        }

        public static void GenerateCS(this InterfaceDefinition def, string indent, StringBuilder builder)
        {
            def.GenerateCS(indent, builder, Variant.Interface);
            builder.AppendLine();
            def.GenerateCS(indent, builder, Variant.Implementation);
            builder.AppendLine();
            def.GenerateCS(indent, builder, Variant.Reference);
            builder.AppendLine();
        }

        public static void GenerateCS(this InterfaceDefinition def, string indent, StringBuilder builder, Variant variant)
        {
            def.GenerateSummary(indent + DEFAULT_COMMENT_INDENT, builder);

            if (variant != Variant.Reference)
                builder.AppendLine(indent + string.Format("[AOSInterface(\"{0}\", typeof(I{1}Impl), typeof({1}Ref))]", def.Type.FullName, def.Name));

            if (variant == Variant.Implementation)
                foreach (var attr in def.Attributes)
                    attr.GenerateCS(indent, builder);

            var baseInterfaces = def.BaseInterfaces.Select(i => def.Namespace.Type.ResolveType(i, "C#")).ToArray();

            builder.AppendLine(indent + string.Format(variant == Variant.Reference ? "public class {0}Ref : ObjectRef<I{0}Impl>, I{0}" : "public interface I{0}{1} : IObject{2}", def.Name, variant == Variant.Implementation ? "Impl" : "", variant == Variant.Implementation ? "Impl" : "Ref") + string.Join("", baseInterfaces.Select(i => ", " + i.GenerateCS(def.Namespace.Type) + (variant == Variant.Implementation ? "Impl" : ""))));
            builder.AppendLine(indent + "{");

            bool first = true;

            if (variant == Variant.Reference) {
                foreach (var i in baseInterfaces)
                    builder.AppendLine(indent + DEFAULT_CS_INDENT + string.Format("I{0} {0}Ref {{ get; }}", i.Name));

                builder.AppendLine(indent + DEFAULT_CS_INDENT + string.Format("public {0}Ref(I{0}Impl implementation)", def.Name));
                builder.AppendLine(indent + DEFAULT_CS_INDENT + DEFAULT_CS_INDENT + ": base(implementation)");
                builder.AppendLine(indent + DEFAULT_CS_INDENT + "{");
                builder.Append(indent + DEFAULT_CS_INDENT + DEFAULT_CS_INDENT + "baseReferences = new IObjectRef[] {");

                var firstItem = true;
                foreach (var i in baseInterfaces) {
                    builder.AppendLine();
                    if (!firstItem)
                        builder.Append(",");
                    firstItem = false;
                    builder.Append(indent + DEFAULT_CS_INDENT + DEFAULT_CS_INDENT + DEFAULT_CS_INDENT + string.Format("{0}Ref = new {0}Ref(implementation)", i.Name));
                }

                if (!firstItem)
                    builder.AppendLine();
                builder.AppendLine((firstItem ? " " : indent + DEFAULT_CS_INDENT + DEFAULT_CS_INDENT) + "};");
                builder.AppendLine(indent + DEFAULT_CS_INDENT + "}");
                first = false;
            } else if (variant == Variant.Implementation) {
                builder.AppendLine(indent + DEFAULT_CS_INDENT + string.Format("I{0} {0}Ref {{ get; }}", def.Name));
                first = false;
            }

            var allMethods = def.Methods.Select(m => new { def = def, method = m });

            if (variant == Variant.Reference)
                foreach (var baseInterface in baseInterfaces.Select(i => ((InterfaceDefinition)((TypeWithDefinition)i).Definition)))
                    allMethods = allMethods.Concat(baseInterface.Methods.Select(i => new { def = baseInterface, method = i }));

            foreach (var child in allMethods) {
                if (!first)
                    builder.AppendLine();
                first = false;

                child.method.GenerateCS(indent + DEFAULT_CS_INDENT, builder, child.def.Name, child.def != def, variant);
            }

            builder.AppendLine(indent + "}");
        }

        public static void GenerateCS(this AttributeDefinition def, string indent, StringBuilder builder)
        {
            builder.AppendLine(indent + string.Format("[AOSAttribute(\"{0}\", \"{1}\"{2})]", def.Name , def.Method, def.Field == null ? "" : string.Format(", Field = \"{0}\"", def.Field)));
        }

        public static void GenerateCS(this MethodDefinition def, string indent, StringBuilder builder, string interfaceName, bool forward, Variant variant)
        {
            if (variant != Variant.LocalCall && variant != Variant.RemoteCall && variant != Variant.BaseCall) {
                def.GenerateSummary(indent + DEFAULT_COMMENT_INDENT, builder);

                foreach (var child in def.Parameters)
                    child.GenerateSummaryCS(indent + DEFAULT_COMMENT_INDENT, builder);
            }

            string format;
            if (variant == Variant.Reference)
                format = "{0} I" + interfaceName + ".{1}";
            else if (variant == Variant.LocalCall)
                format = "{2}implementation.{1}";
            else if (variant == Variant.BaseCall)
                format = "{2}{3}Ref.{1}";
            else
                format = "{0} {1}";

            builder.Append(indent + string.Format(format, def.ReturnType.ResolveTypeNameCS(def.Namespace), def.Name, def.ReturnType == "void" ? "" : "return ", interfaceName) + "(");

            bool first = true;
            foreach (var child in def.Parameters) {
                if (!first)
                    builder.Append(", ");
                first = false;

                child.GenerateCS(builder, variant);
            }

            if (variant == Variant.Reference) {
                builder.AppendLine(")");
                builder.AppendLine(indent + "{");
                def.GenerateCS(indent + DEFAULT_CS_INDENT, builder, interfaceName, forward, forward ? Variant.BaseCall : Variant.LocalCall);
                builder.AppendLine(indent + "}");
            } else {
                builder.AppendLine(");");
            }
        }

        public static void GenerateCS(this ParamDefinition def, StringBuilder builder, Variant variant)
        {
            if (variant != Variant.LocalCall && variant != Variant.RemoteCall && variant != Variant.BaseCall)
                builder.Append(def.ParamType.ResolveTypeNameCS(def.Namespace) + " ");
            builder.Append(def.Name);
        }

        public static void GenerateSummaryCS(this ParamDefinition def, string indent, StringBuilder builder)
        {
            if (def.Summary != null)
                builder.AppendLine(indent + string.Format("<param name=\"{0}\">{1}</param>", def.Name, def.Summary));
        }
    }
}
