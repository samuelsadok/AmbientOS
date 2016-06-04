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


        public static void GenerateCSPrologue(this StringBuilder builder)
        {
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Runtime.CompilerServices;");
        }

        public static void GenerateCSChapter(this StringBuilder builder, string title)
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("/*");
            builder.AppendLine(" * " + title);
            builder.AppendLine(" */");
            builder.AppendLine();
        }

        public static void GenerateCSEpilogue(this StringBuilder builder)
        {
            // nothing to do
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

        public static void GenerateSummary(this Definition def, string indent, params StringBuilder[] builders)
        {
            if (def.Summary == null)
                return;

            foreach (var builder in builders) {
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

                var childBuilder = new StringBuilder();

                try {
                    if (child is NamespaceDefinition)
                        (child as NamespaceDefinition).GenerateCS(indent + DEFAULT_CS_INDENT, childBuilder);
                    else if (child is InterfaceDefinition)
                        (child as InterfaceDefinition).GenerateCS(indent + DEFAULT_CS_INDENT, childBuilder);
                    else if (child is BareTypeDefinition)
                        (child as BareTypeDefinition).GenerateCS(indent + DEFAULT_CS_INDENT, childBuilder, ref first);
                    else if (child is AliasDefinition)
                        (child as AliasDefinition).GenerateCS(indent + DEFAULT_CS_INDENT, childBuilder, ref first);
                    else if (child is StructDefinition)
                        (child as StructDefinition).GenerateCS(indent + DEFAULT_CS_INDENT, childBuilder);
                    else if (child is EnumDefinition)
                        (child as EnumDefinition).GenerateCS(indent + DEFAULT_CS_INDENT, childBuilder);
                    else
                        throw new Exception(string.Format("cannot generate C# code for {0}", child.GetType()));

                    builder.Append(childBuilder.ToString());
                } catch (Exception ex) {
                    builder.AppendLine("<Code-Generation-Error>");
                    builder.AppendLine("/* node: " + (child?.Name ?? "(null)"));
                    builder.Append(ex.ToString());
                    builder.AppendLine(" */");
                }
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
            def.GenerateSummary(indent + DEFAULT_COMMENT_INDENT, builder);

            var intBuilder = new StringBuilder();
            var impBuilder = new StringBuilder();
            var refBuilder = new StringBuilder();


            var baseInterfaces = def.BaseInterfaces.Select(i => def.Namespace.Type.ResolveType(i, "C#")).ToArray();

            var allProperties = def.Properties.Select(p => new { def = def, property = p });
            var allMethods = def.Methods.Select(m => new { def = def, method = m });

            foreach (var baseInterface in baseInterfaces.Select(i => ((InterfaceDefinition)((TypeWithDefinition)i).Definition))) {
                allProperties = allProperties.Concat(baseInterface.Properties.Select(p => new { def = baseInterface, property = p }));
                allMethods = allMethods.Concat(baseInterface.Methods.Select(m => new { def = baseInterface, method = m }));
            }

            // *** emit static members ***
            
            refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + string.Format("public readonly static ObjectStore<I{0}Impl, {0}Ref> store = new ObjectStore<I{0}Impl, {0}Ref>(impl => new {0}Ref(impl));", def.Name));
            refBuilder.AppendLine();


            // *** emit constructor ***

            refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + string.Format("public {0}Ref(I{0}Impl implementation)", def.Name));
            refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + DEFAULT_CS_INDENT + ": base(implementation)");
            refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + "{");
            refBuilder.Append(indent + DEFAULT_CS_INDENT + DEFAULT_CS_INDENT + "baseInterfaces = new IObjectRef[] {");

            var firstItem = true;
            foreach (var i in baseInterfaces) {
                refBuilder.AppendLine();
                if (!firstItem)
                    refBuilder.Append(",");
                firstItem = false;
                refBuilder.Append(indent + DEFAULT_CS_INDENT + DEFAULT_CS_INDENT + DEFAULT_CS_INDENT + string.Format("{0}Ref.store.GetReference(implementation)", i.Name));
            }

            if (!firstItem)
                refBuilder.AppendLine();
            refBuilder.AppendLine((firstItem ? " " : indent + DEFAULT_CS_INDENT + DEFAULT_CS_INDENT) + "};");


            foreach (var i in allProperties)
                refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + DEFAULT_CS_INDENT + string.Format("I{0}_{1} = implementation.{1};", i.def.Name, i.property.Name, i.property.PropertyType.ResolveTypeNameCS(def.Namespace)));

            refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + "}");
            refBuilder.AppendLine();


            // *** emit override functions ***
            /*
            refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + string.Format("protected override void DeliverPropertiesTo(I{0}Impl implementation)", def.Name));
            refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + "{");
            foreach (var i in allProperties)
                refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + DEFAULT_CS_INDENT + string.Format("I{0}_{1}.DeliverTo(implementation.{1});", i.def.Name, i.property.Name));
            refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + "}");

            refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + string.Format("protected override void FetchPropertiesFrom(I{0}Impl implementation)", def.Name));
            refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + "{");
            foreach (var i in allProperties)
                refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + DEFAULT_CS_INDENT + string.Format("I{0}_{1}.FetchFrom(implementation.{1});", i.def.Name, i.property.Name));
            refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + "}");
            */


            // *** emit properties & methods ***

            bool first = true;

            foreach (var child in allProperties) {
                refBuilder.AppendLine();
                if (!first) {
                    impBuilder.AppendLine();
                    intBuilder.AppendLine();
                }
                first = false;

                child.property.GenerateCS(indent + DEFAULT_CS_INDENT, intBuilder, impBuilder, refBuilder, child.def.Name, child.def != def);
            }

            foreach (var child in allMethods) {
                refBuilder.AppendLine();
                if (!first) {
                    impBuilder.AppendLine();
                    intBuilder.AppendLine();
                }
                first = false;

                child.method.GenerateCS(indent + DEFAULT_CS_INDENT, intBuilder, impBuilder, refBuilder, child.def.Name, child.def != def);
            }


            // *** concat all versions ***

            builder.AppendLine(indent + string.Format("[AOSInterface(\"{0}\", typeof(I{1}Impl), typeof({1}Ref))]", def.Type.FullName, def.Name));
            builder.AppendLine(indent + string.Format("public interface I{0} : IObjectRef", def.Name) + string.Join("", baseInterfaces.Select(i => ", " + i.GenerateCS(def.Namespace.Type))));
            builder.AppendLine(indent + "{");
            builder.Append(intBuilder);
            builder.AppendLine(indent + "}");
            builder.AppendLine();

            foreach (var attr in def.Attributes)
                attr.GenerateCS(indent, builder);
            builder.AppendLine(indent + string.Format("[AOSInterface(\"{0}\", typeof(I{1}Impl), typeof({1}Ref))]", def.Type.FullName, def.Name)); // it's not yet clear if this attribute is actually required in both the interface and the implementation
            builder.AppendLine(indent + string.Format("public interface I{0}Impl : IObjectImpl", def.Name) + string.Join("", baseInterfaces.Select(i => ", " + i.GenerateCS(def.Namespace.Type) + "Impl")));
            builder.AppendLine(indent + "{");
            builder.Append(impBuilder);
            builder.AppendLine(indent + "}");
            builder.AppendLine();

            builder.AppendLine(indent + string.Format("public class {0}Ref : ObjectRef<I{0}Impl>, I{0}", def.Name) + string.Join("", baseInterfaces.Select(i => ", " + i.GenerateCS(def.Namespace.Type))));
            builder.AppendLine(indent + "{");
            builder.Append(refBuilder);
            builder.AppendLine(indent + "}");
            builder.AppendLine();
        }

        public static void GenerateCS(this AttributeDefinition def, string indent, StringBuilder builder)
        {
            builder.AppendLine(indent + string.Format("[AOSAttribute(\"{0}\", \"{1}\"{2})]", def.Name, def.Method, def.Field == null ? "" : string.Format(", Field = \"{0}\"", def.Field)));
        }

        public static void GenerateCS(this PropertyDefinition def, string indent, StringBuilder intBuilder, StringBuilder impBuilder, StringBuilder refBuilder, string interfaceName, bool inherited)
        {
            def.GenerateSummary(indent + DEFAULT_COMMENT_INDENT, intBuilder, impBuilder, refBuilder);

            if (!inherited) {
                intBuilder.AppendLine(indent + string.Format("DynamicValue<{1}> {0} {{ get; }}", def.Name, def.PropertyType.ResolveTypeNameCS(def.Namespace), interfaceName));
                impBuilder.AppendLine(indent + string.Format("DynamicValue<{1}> {0} {{ get; }}", def.Name, def.PropertyType.ResolveTypeNameCS(def.Namespace), interfaceName));
            }

            refBuilder.AppendLine(indent + string.Format("DynamicValue<{1}> I{2}.{0} {{ get {{ return I{2}_{0}.Fetch(); }} }}", def.Name, def.PropertyType.ResolveTypeNameCS(def.Namespace), interfaceName));
            refBuilder.AppendLine(indent + string.Format("readonly DynamicValue<{1}> I{2}_{0};", def.Name, def.PropertyType.ResolveTypeNameCS(def.Namespace), interfaceName));
        }

        public static void GenerateCS(this MethodDefinition def, string indent, StringBuilder intBuilder, StringBuilder impBuilder, StringBuilder refBuilder, string interfaceName, bool inherited)
        {
            def.GenerateSummary(indent + DEFAULT_COMMENT_INDENT, intBuilder, impBuilder, refBuilder);

            foreach (var child in def.Parameters)
                child.GenerateSummaryCS(indent + DEFAULT_COMMENT_INDENT, intBuilder, impBuilder, refBuilder);

            var returnType = def.ReturnType.ResolveTypeNameCS(def.Namespace);
            var paramList1 = string.Join(", ", def.Parameters.Select(param => param.GenerateCS(true)));
            var paramList2 = string.Join(", ", def.Parameters.Select(param => param.GenerateCS(false)));

            if (!inherited) {
                intBuilder.AppendLine(indent + string.Format("{0} {1}({2});", returnType, def.Name, paramList1));
                impBuilder.AppendLine(indent + string.Format("{0} {1}({2});", returnType, def.Name, paramList1));
            }

            refBuilder.AppendLine(indent + string.Format("public {0} {1}({2})", returnType, def.Name, paramList1));
            refBuilder.AppendLine(indent + "{");
            refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + string.Format("Barrier();"));
            refBuilder.AppendLine(indent + DEFAULT_CS_INDENT + string.Format("{0}implementation.{1}({2});", def.ReturnType == "void" ? "" : "return ", def.Name, paramList2));
            refBuilder.AppendLine(indent + "}");
        }

        public static string GenerateCS(this ParamDefinition def, bool includeType)
        {
            return (includeType ? def.ParamType.ResolveTypeNameCS(def.Namespace) + " " : "") + def.Name;
        }

        public static void GenerateSummaryCS(this ParamDefinition def, string indent, params StringBuilder[] builders)
        {
            if (def.Summary == null)
                return;

            foreach (var builder in builders)
                builder.AppendLine(indent + string.Format("<param name=\"{0}\">{1}</param>", def.Name, def.Summary));
        }
    }
}
