using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using EnvDTE;

namespace InterfaceParser
{

    public class Program
    {

        public static void Main(string[] args)
        {
            try {
                var root = NamespaceDefinition.GetNewRootNamespace();

                var file = new DefinitionFile(args[0], root);
                var builder = new StringBuilder();

                builder.GenerateCSPrologue();

                file.RootDefinition.GenerateCS("", builder);

                builder.GenerateCSEpilogue();

                Console.WriteLine(builder.ToString());

            } catch (Exception ex) {
                Console.WriteLine("/* CODE GENERATION FAILED!");
                Console.WriteLine(ex.ToString() + " */");
            }
        }
    }
}
