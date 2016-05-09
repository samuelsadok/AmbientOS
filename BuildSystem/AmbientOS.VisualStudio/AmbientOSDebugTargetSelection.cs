using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace AmbientOS.VisualStudio
{
    class AmbientOSDebugTargetSelection : IVsProjectCfgDebugTargetSelection
    {
        IVsProjectCfgDebugTargetSelection baseDebugTargetSelection;

        public AmbientOSDebugTargetSelection(IVsProjectCfgDebugTargetSelection baseDebugTargetSelection)
        {
            this.baseDebugTargetSelection = baseDebugTargetSelection; // this is usually (always?) null
        }

        public void GetCurrentDebugTarget(out Guid pguidDebugTargetType, out uint pDebugTargetTypeId, out string pbstrCurrentDebugTarget)
        {
            if (baseDebugTargetSelection != null) {
                baseDebugTargetSelection.GetCurrentDebugTarget(out pguidDebugTargetType, out pDebugTargetTypeId, out pbstrCurrentDebugTarget);
            } else {
                pguidDebugTargetType = DebugCommand.CommandSet;
                pDebugTargetTypeId = DebugCommand.CommandId;
                pbstrCurrentDebugTarget = Constants.DebugTargetName;
            }
        }
        
        /// <returns>A string array of human-readable debug target names</returns>
        public Array GetDebugTargetListOfType(Guid guidDebugTargetType, uint debugTargetTypeId)
        {
            if (baseDebugTargetSelection != null) {
                return baseDebugTargetSelection.GetDebugTargetListOfType(guidDebugTargetType, debugTargetTypeId);
            } else if (guidDebugTargetType == DebugCommand.CommandSet && debugTargetTypeId == DebugCommand.CommandId) {
                return new string[] { Constants.DebugTargetName };
            } else {
                return new string[0];
            }
        }

        /// <param name="pbstrSupportedTargetCommandIDs">Returns an array containing a "Guid:id" string for every debug target group</param>
        public bool HasDebugTargets(IVsDebugTargetSelectionService pDebugTargetSelectionService, out Array pbstrSupportedTargetCommandIDs)
        {
            if (baseDebugTargetSelection != null) {
                return baseDebugTargetSelection.HasDebugTargets(pDebugTargetSelectionService, out pbstrSupportedTargetCommandIDs);
            } else {
                pbstrSupportedTargetCommandIDs = new string[] { DebugCommand.CommandSet + ":" + DebugCommand.CommandId };
                return true;
            }
        }

        /*
        
        private IEnumerable<string> GetDebugTargets(IVsDebugTargetSelectionService pDebugTargetSelectionService)
        {
            yield return DebugCommand.CommandSet + ":" + DebugCommand.CommandId;

            Array targetCommands;
            for (int i = 0; i < baseDebugTargetSelection.Count(); i++) {
                if (baseDebugTargetSelection[i] == null)
                    continue;
                if (baseDebugTargetSelection[i].HasDebugTargets(pDebugTargetSelectionService, out targetCommands)) {
                    foreach (var str in targetCommands.OfType<string>()) {
                        // we associate the current collection with the provided key, so that we can later determine what collection was meant when a new target is selected
                        selectionsByKey[str.ToLower()] = i;
                        yield return str;
                    }
                }
            }
        }
        */

        public void SetCurrentDebugTarget(Guid guidDebugTargetType, uint debugTargetTypeId, string bstrCurrentDebugTarget)
        {
            if (baseDebugTargetSelection != null) {
                baseDebugTargetSelection.SetCurrentDebugTarget(guidDebugTargetType, debugTargetTypeId, bstrCurrentDebugTarget);
            } else {
                // do nothing (we only have one target currently)
            }
        }

        public int Close()
        {
            // todo: dispose resources
            return VSConstants.S_OK;
        }
    }
}
