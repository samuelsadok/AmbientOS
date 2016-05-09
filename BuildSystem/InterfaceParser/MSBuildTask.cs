using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;

namespace InterfaceParser
{
    /// <summary>
    /// This is the MSBuild task that generates code files from XML interface descriptions.
    /// </summary>
    public class InterfaceCodeGenerationTask : Task
    {
        [Required]
        public ITaskItem[] Templates { get; set; }

        [Required]
        [Output]
        public ITaskItem Output { get; set; }

        public override bool Execute()
        {
            var generatedFileNames = new List<string>();

            var root = NamespaceDefinition.GetNewRootNamespace();
            var definitionFiles = new DefinitionFile[Templates.Count()];
            var builder = new StringBuilder();

            builder.GenerateCSPrologue();

            // load all files
            for (int i = 0; i < Templates.Count(); i++) {
                var inputFileName = Templates[i].ItemSpec;

                try {
                    definitionFiles[i] = new DefinitionFile(inputFileName, root);
                } catch (Exception ex) {
                    definitionFiles[i] = null;
                    Log.LogError("Error while parsing [{0}]", inputFileName);
                    Log.LogErrorFromException(ex, true, true, inputFileName);
                }
            }

            // generate code for all files
            for (int i = 0; i < Templates.Count(); i++) {
                if (definitionFiles[i] == null)
                    continue;

                var inputFileName = Templates[i].ItemSpec;

                try {
                    builder.GenerateCSChapter(inputFileName);
                    definitionFiles[i].RootDefinition.GenerateCS("", builder);
                } catch (Exception ex) {
                    Log.LogError("Error while generating code for [{0}]", inputFileName);
                    Log.LogErrorFromException(ex, true, true, inputFileName);
                }
            }

            builder.GenerateCSEpilogue();

            string outputFileName = Output.ItemSpec;

            using (var destination = new FileStream(outputFileName, FileMode.Create)) {
                var bytes = Encoding.UTF8.GetBytes(builder.ToString());
                destination.Write(bytes, 0, bytes.Length);
            }
            generatedFileNames.Add(outputFileName);


            Log.LogMessage("Finished Generation");
            return !Log.HasLoggedErrors;
        }
    }

}
