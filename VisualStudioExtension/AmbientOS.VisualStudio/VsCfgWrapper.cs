using System;
using System.Collections.Generic;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace AmbientOS.VisualStudio
{
    internal class VsCfgWrapper : IVsCfg
    {
        private readonly IVsCfg template;
        private readonly string platformRename;
        private readonly Dictionary<string, string> configurationRenames;

        /// <summary>
        /// Wraps an IVsCfg object with the purpose of renaming the platform and configuration.
        /// </summary>
        public VsCfgWrapper(IVsCfg template, string platformRename, Dictionary<string, string> configurationRenames)
        {
            this.template = template;
            this.platformRename = platformRename;
            this.configurationRenames = configurationRenames;
        }

        public int get_DisplayName(out string pbstrDisplayName)
        {
            var result = template.get_DisplayName(out pbstrDisplayName);

            if (result == VSConstants.S_OK || !string.IsNullOrEmpty(pbstrDisplayName)) {
                var delimiter = pbstrDisplayName.IndexOf('|');
                if (delimiter != -1) {
                    // rename configuration
                    var configuration = pbstrDisplayName.Substring(0, delimiter);
                    string newConfiguration;
                    if (configurationRenames.TryGetValue(configuration, out newConfiguration))
                        configuration = newConfiguration;

                    // rename platform
                    var platform = pbstrDisplayName.Substring(delimiter + 1, pbstrDisplayName.Length - delimiter - 1);
                    platform = platformRename;

                    pbstrDisplayName = configuration + '|' + platform;
                }
            }

            return result;
        }

        public int get_IsDebugOnly(out int pfIsDebugOnly)
        {
            return template.get_IsDebugOnly(out pfIsDebugOnly);
        }

        public int get_IsReleaseOnly(out int pfIsReleaseOnly)
        {
            return template.get_IsReleaseOnly(out pfIsReleaseOnly);
        }
    }
}
