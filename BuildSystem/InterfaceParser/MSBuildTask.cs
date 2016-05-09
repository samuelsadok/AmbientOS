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

        //[Output]
        //public ITaskItem[] GeneratedFiles { get; set; }

        public override bool Execute()
        {
            var generatedFileNames = new List<string>();

            foreach (var item in Templates) {
                string inputFileName = item.ItemSpec;
                string outputFileName = Output.ItemSpec; // Path.ChangeExtension(inputFileName, ".Designer.cs");
                string result;

                try {
                    // Build code string
                    result = "Code generator invoked on " + inputFileName + " at " + DateTime.Now;

                    using (var destination = new FileStream(outputFileName, FileMode.Create)) {
                        var bytes = Encoding.UTF8.GetBytes(result);
                        destination.Write(bytes, 0, bytes.Length);
                    }
                    generatedFileNames.Add(outputFileName);
                } catch (Exception ex) {
                    Log.LogError("Error while compiling [{0}]", inputFileName);
                    Log.LogErrorFromException(ex, true, true, inputFileName);
                }
            }
            //GeneratedFiles = generatedFileNames.Select(name => new TaskItem(name)).ToArray();


            Log.LogMessage("Finished Generation");
            return !Log.HasLoggedErrors;
        }
    }

}
