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
            //var p = new System.Diagnostics.Process();
            //p.Kill();
            //p.WaitForExit()
            

            try {
                var ns = new NamespaceDefinition();
                ns.InitAsRoot();

                var file = new DefinitionFile(args[0], ns);
                var builder = new StringBuilder();

                builder.AppendLine("using System;");
                builder.AppendLine("using System.Collections.Generic;");
                builder.AppendLine("using AmbientOS.Utils;");
                builder.AppendLine();

                file.RootDefinition.GenerateCS("", builder);

                Console.WriteLine(builder.ToString());

            } catch (Exception ex) {
                Console.WriteLine("/* CODE GENERATION FAILED!");
                Console.WriteLine(ex.ToString() + " */");
            }
        }


        //    public static string Hello()
        //    {
        //        var domain = AppDomain.CreateDomain("InterfaceParser");
        //        var instance = domain.CreateInstanceFromAndUnwrap(@".\..\InterfaceParser\bin\Debug\InterfaceParser.dll", "InterfaceParser.Class1");
        //        domain.ExecuteAssembly()
        //        AppDomain.Unload(domain);
        //
        //
        //        var result = (MarshalByRefObject)instance.GetType().GetMethod("Hello").Invoke(instance, new object[0]);
        //
        //
        //        //var assembly = System.Reflection.Assembly.LoadFile(Host.ResolvePath(@".\..\InterfaceParser\bin\Debug\InterfaceParser.dll"));
        //        //var t = assembly.GetType("InterfaceParser.Class1");
        //        //var m = t.GetMethod("Hello", System.Reflection.BindingFlags.Static);
        //        //m.Invoke(null, new object[0]);
        //        
        //        return "hello world!";
        //    }
    }
}
