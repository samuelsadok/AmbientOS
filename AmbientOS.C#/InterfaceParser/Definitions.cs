using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace InterfaceParser
{
    public class DefinitionFile
    {

        public NamespaceDefinition RootDefinition { get; }

        public DefinitionFile(string path, NamespaceDefinition globalNamespace)
        {
            var doc = new XmlDocument();
            //doc.PreserveWhitespace = false;
            doc.Load(path);
            RootDefinition = new NamespaceDefinition();
            globalNamespace.Children.Add(RootDefinition);
            RootDefinition.Init(doc.DocumentElement, globalNamespace);

            //var i = doc.DocumentElement as IXmlLineInfo;
            //if (i != null) {
            //    Console.WriteLine("have line info");
            //} else {
            //    Console.WriteLine("have NO line info");
            //}
        }
    }


    public abstract class Definition
    {
        public XmlNode Node { get; protected set; }
        public string Name { get; protected set; }
        public string ShortName { get; protected set; }
        public string Language { get; protected set; } = null;
        public string Summary { get; protected set; }
        
        /// <summary>
        /// The type that is associated with this definition.
        /// </summary>
        public Type Type { get; protected set; }

        /// <summary>
        /// The namespace definition that contains this definition.
        /// </summary>
        public NamespaceDefinition Namespace { get; private set; }

        protected virtual void ConsumeNode(XmlNode node)
        {
            if (node is XmlComment)
                return;

            switch (node.Name) {
                case "name": Name = node.InnerText; break;
                case "lang": Language = node.InnerText; break;
                case "short": ShortName = node.InnerText; break;
                case "summary": Summary = node.InnerText; break;
                default: throw new Exception(string.Format("node \"{0}\" is invalid in {1}", node.Name, GetType()));
            }
        }

        public virtual void Init(XmlNode node, NamespaceDefinition ns)
        {
            Node = node;
            Namespace = ns;

            foreach (XmlAttribute attr in node.Attributes)
                ConsumeNode(attr);

            if (this is NamespaceDefinition)
                Type = ns.Type.AddType(Name, Language);

            foreach (XmlNode child in node.ChildNodes)
                ConsumeNode(child);

            if (this is TypeDefinition)
                Type = ns.Type.AddType(Name, Language, (TypeDefinition)this);
        }
    }


    public class NamespaceDefinition : Definition
    {
        public List<Definition> Children { get; } = new List<Definition>();
        public Dictionary<string, List<TypeDefinition>> Types { get; } = new Dictionary<string, List<TypeDefinition>>();

        public void InitAsRoot()
        {
            Name = null;
            Type = new RootType();
        }

        protected override void ConsumeNode(XmlNode node)
        {
            switch (node.Name) {
                case "namespace":
                    var nsDef = new NamespaceDefinition();
                    nsDef.Init(node, this);
                    Children.Add(nsDef);
                    break;

                case "interface":
                    var ifDef = new InterfaceDefinition();
                    ifDef.Init(node, this);
                    Children.Add(ifDef);
                    break;

                case "type":
                    var tDef = new BareTypeDefinition();
                    tDef.Init(node, this);
                    Children.Add(tDef);
                    break;

                case "alias":
                    var aDef = new AliasDefinition();
                    aDef.Init(node, this);
                    Children.Add(aDef);
                    break;

                case "struct":
                    var sDef = new StructDefinition();
                    sDef.Init(node, this);
                    Children.Add(sDef);
                    break;

                case "enum":
                    var eDef = new EnumDefinition();
                    eDef.Init(node, this);
                    Children.Add(eDef);
                    break;

                default:
                    base.ConsumeNode(node);
                    break;
            }
        }
    }

    public abstract class TypeDefinition : Definition
    {
        public string[] GenericArgNames { get; private set; }

        
        public static string[] ReadGenericName(ref string name)
        {
            if (!name.Substring(name.LastIndexOf('.') + 1).Contains('['))
                return null;

            var argListStart = name.LastIndexOf('[') + 1;
            var argListEnd = name.LastIndexOf(']');

            if (argListEnd <= argListStart)
                throw new Exception(string.Format("invalid argument list in type \"{0}\"", name));

            var argList = name.Substring(argListStart, argListEnd - argListStart);
            var argNames = argList.Split(',').Select(arg => arg.Trim(' ')).ToArray();
            name = name.Substring(0, argListStart - 1) + "`" + argNames.Count();
            return argNames;
        }

        public override void Init(XmlNode node, NamespaceDefinition ns)
        {
            base.Init(node, ns);

            if (Name == null)
                throw new Exception("type definitions must have a name");

            var name = Name;
            GenericArgNames = ReadGenericName(ref name);
            Name = name;
        }

        protected override void ConsumeNode(XmlNode node)
        {
            switch (node.Name) {
                default: base.ConsumeNode(node); break;
            }
        }

        protected string GetCode(string genericCode, string[] genericArgs)
        {
            if (genericArgs == null) {
                if (GenericArgNames != null)
                    throw new Exception("Unbound generic type arguments in " + this);
            } else {
                // todo: wait for the first problems to arise because of this half hearted generic arg substitution, and then improve it
                for (int i = 0; i < genericArgs.Count(); i++)
                    genericCode = genericCode.Replace(GenericArgNames[i], genericArgs[i]);
            }
            return genericCode;
        }

        public abstract string GetTypeName(string[] genericArgs, Func<string, NamespaceDefinition, string> typeResolver);
    }

    public class BareTypeDefinition : TypeDefinition
    {
        public string Code { get; private set; }

        protected override void ConsumeNode(XmlNode node)
        {
            switch (node.Name) {
                case "code": Code = node.InnerText; break;
                default: base.ConsumeNode(node); break;
            }
        }

        public override string GetTypeName(string[] genericArgs, Func<string, NamespaceDefinition, string> typeResolver)
        {
            return GetCode(Code, genericArgs);
        }
    }

    public class AliasDefinition : TypeDefinition
    {
        public string Alias { get; private set; }

        protected override void ConsumeNode(XmlNode node)
        {
            switch (node.Name) {
                case "alias": Alias = node.InnerText; break;
                default: base.ConsumeNode(node); break;
            }
        }

        public override string GetTypeName(string[] genericArgs, Func<string, NamespaceDefinition, string> typeResolver)
        {
            return typeResolver(Alias, Namespace);
        }
    }

    public class StructDefinition : TypeDefinition
    {
        public List<FieldDefinition> Fields { get; private set; } = new List<FieldDefinition>();

        protected override void ConsumeNode(XmlNode node)
        {
            switch (node.Name) {
                case "field":
                    var def = new FieldDefinition();
                    def.Init(node, Namespace);
                    Fields.Add(def);
                    break;

                default:
                    base.ConsumeNode(node);
                    break;
            }
        }

        public override string GetTypeName(string[] genericArgs, Func<string, NamespaceDefinition, string> typeResolver)
        {
            // todo: implement generic structs
            return GetCode(Name, genericArgs);
        }
    }

    public class FieldDefinition : Definition
    {
        public string FieldType { get; private set; }

        protected override void ConsumeNode(XmlNode node)
        {
            switch (node.Name) {
                case "type": FieldType = node.InnerText; break;
                default: base.ConsumeNode(node); break;
            }
        }
    }

    public class EnumDefinition : TypeDefinition
    {
        public List<ValueDefinition> Values { get; private set; } = new List<ValueDefinition>();

        protected override void ConsumeNode(XmlNode node)
        {
            switch (node.Name) {
                case "value":
                    var def = new ValueDefinition();
                    def.Init(node, Namespace);
                    Values.Add(def);
                    break;

                default:
                    base.ConsumeNode(node);
                    break;
            }
        }

        public override string GetTypeName(string[] genericArgs, Func<string, NamespaceDefinition, string> typeResolver)
        {
            if (genericArgs != null)
                throw new Exception("generic enum types are not allowed");
            return GetCode(Name, genericArgs);
        }
    }

    public class ValueDefinition : Definition
    {
        public string Value { get; private set; }

        protected override void ConsumeNode(XmlNode node)
        {
            switch (node.Name) {
                case "value": Value = node.InnerText; break;
                default: base.ConsumeNode(node); break;
            }
        }
    }

    public class InterfaceDefinition : TypeDefinition
    {
        public List<AttributeDefinition> Attributes { get; } = new List<AttributeDefinition>();
        public List<string> BaseInterfaces { get; } = new List<string>();
        public List<PropertyDefinition> Properties { get; } = new List<PropertyDefinition>();
        public List<MethodDefinition> Methods { get; } = new List<MethodDefinition>();

        protected override void ConsumeNode(XmlNode node)
        {
            switch (node.Name) {

                case "inherits":
                    BaseInterfaces.Add(node.InnerText);
                    break;

                case "attribute":
                    var aDef = new AttributeDefinition();
                    aDef.Init(node, Namespace);
                    Attributes.Add(aDef);
                    break;

                case "property":
                    var pDef = new PropertyDefinition();
                    pDef.Init(node, Namespace);
                    Properties.Add(pDef);
                    break;

                case "method":
                    var mDef = new MethodDefinition();
                    mDef.Init(node, Namespace);
                    Methods.Add(mDef);
                    break;

                default:
                    base.ConsumeNode(node);
                    break;
            }
        }

        public override string GetTypeName(string[] genericArgs, Func<string, NamespaceDefinition, string> typeResolver)
        {
            if (genericArgs != null)
                throw new Exception("generic interfaces are not allowed");
            return GetCode(Name, genericArgs);
        }
    }

    public class AttributeDefinition : Definition
    {
        public string Method { get; private set; }
        public string Field { get; private set; } = null;

        protected override void ConsumeNode(XmlNode node)
        {
            switch (node.Name) {
                case "method": Method = node.InnerText; break;
                case "field": Field = node.InnerText; break;
                default: base.ConsumeNode(node); break;
            }
        }
    }

    public class PropertyDefinition : Definition
    {
        public string PropertyType { get; private set; }

        protected override void ConsumeNode(XmlNode node)
        {
            switch (node.Name) {
                case "type": PropertyType = node.InnerText; break;
                default: base.ConsumeNode(node); break;
            }
        }
    }

    public class MethodDefinition : Definition
    {
        public string ReturnType { get; private set; } = "void";
        public List<ParamDefinition> Parameters { get; } = new List<ParamDefinition>();

        protected override void ConsumeNode(XmlNode node)
        {
            switch (node.Name) {
                case "ret":
                    ReturnType = node.InnerText;
                    break;

                case "param":
                    var pDef = new ParamDefinition();
                    pDef.Init(node, Namespace);
                    Parameters.Add(pDef);
                    break;

                default:
                    base.ConsumeNode(node);
                    break;
            }
        }
    }

    public class ParamDefinition : Definition
    {
        public string ParamType { get; private set; }

        protected override void ConsumeNode(XmlNode node)
        {
            switch (node.Name) {
                case "type": ParamType = node.InnerText; break;
                default: base.ConsumeNode(node); break;
            }
        }
    }
}
