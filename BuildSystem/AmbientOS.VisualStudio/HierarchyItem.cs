using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.VisualStudio
{
    class HierarchyItem
    {
        public bool IsHidden { get; } = false;

        public HierarchyItem FirstChild { get; set; }
        public HierarchyItem NextSibling { get; set; }

        private readonly uint parentId;
        private readonly Tuple<IVsUIHierarchy, uint> template;

        private static Random ItemIdGenerator = new Random(); // kinda dubious to use random number here
        private uint? itemId;
        public uint ItemId { get { return itemId ?? (uint)(itemId = (uint)ItemIdGenerator.Next(1, 2147483647)); } }
        
        /// <param name="parentId">The ID of the hierarchy item that contains this item.</param>
        /// <param name="template">If not null, this specifies a IVsHierarchy and an itemId. Calls that cannot be handled by this hierarchy item are forwarded to the specified template.</param>
        public HierarchyItem(uint parentId, Tuple<IVsUIHierarchy, uint> template = null)
        {
            this.parentId = parentId;
            this.template = template;
        }

        public virtual int GetProperty(__VSHPROPID propId, out object property)
        {
            switch (propId) {
                case __VSHPROPID.VSHPROPID_Parent: property = unchecked((int)parentId); break;
                case __VSHPROPID.VSHPROPID_NextVisibleSibling: property = GetFirstItemId(NextSibling, false); break;
                case __VSHPROPID.VSHPROPID_NextSibling: property = GetFirstItemId(NextSibling, true); break;
                case __VSHPROPID.VSHPROPID_FirstChild: property = GetFirstItemId(FirstChild, false); break;
                case __VSHPROPID.VSHPROPID_FirstVisibleChild: property = GetFirstItemId(FirstChild, true); break;
                case __VSHPROPID.VSHPROPID_Expandable: property = (FirstChild != null ? 1 : 0); break;
                case __VSHPROPID.VSHPROPID_IsHiddenItem: property = IsHidden; break;
                case __VSHPROPID.VSHPROPID_ExtObject: property = this; break;
                case __VSHPROPID.VSHPROPID_IsNonMemberItem: property = true; break;
                case __VSHPROPID.VSHPROPID_SortPriority: property = -1; break;
                default:
                    if (template != null)
                        return template.Item1.GetProperty(template.Item2, (int)propId, out property);

                    property = null;
                    return VSConstants.E_NOTIMPL;
            }

            return VSConstants.S_OK;
        }

        public int SetProperty(__VSHPROPID propId, object property)
        {
            if (template != null)
                return template.Item1.SetProperty(template.Item2, (int)propId, property);

            return VSConstants.E_NOTIMPL;
        }

        public Guid GetGuidProperty(__VSHPROPID propId)
        {
            Guid guid;
            if (template != null)
                if (template.Item1.GetGuidProperty(template.Item2, (int)propId, out guid) == VSConstants.S_OK)
                    return guid;

            return Guid.Empty;
        }

        public void SetGuidProperty(__VSHPROPID propId, ref Guid guid)
        {
            if (template != null)
                template.Item1.SetGuidProperty(template.Item2, (int)propId, ref guid);
        }

        public int QueryStatusCommand(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            if (template != null)
                return template.Item1.QueryStatusCommand(template.Item2, ref pguidCmdGroup, cCmds, prgCmds, pCmdText);

            return VSConstants.E_NOTIMPL;
        }

        public int ExecCommand(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (template != null)
                return template.Item1.ExecCommand(template.Item2, ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

            if (pguidCmdGroup == typeof(VSConstants.VSStd97CmdID).GUID && nCmdID == (int)VSConstants.VSStd97CmdID.Exit)
                return VSConstants.S_OK; // kind of a hack
            return VSConstants.E_NOTIMPL;
        }


        /// <summary>
        /// Returns the first sibling or child of this item with the specified ID.
        /// The current item may also be returned. Returns null if no match was found.
        /// </summary>
        /// <param name="recursive">If true, children are also considered.</param>
        public HierarchyItem GetItem(uint itemId, bool recursive = true)
        {
            for (var item = this; item != null; item = item.NextSibling) {
                if (item.ItemId == itemId)
                    return item;

                if (recursive) {
                    var child = item.FirstChild?.GetItem(itemId);
                    if (child != null)
                        return child;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the ID of the first item in the linked list of items.
        /// Children are not considered.
        /// </summary>
        public static uint GetFirstItemId(HierarchyItem head, bool visibleOnly)
        {
            for (var item = head; item != null; item = item.NextSibling) {
                if (visibleOnly && item.IsHidden)
                    continue;
                return item.ItemId;
            }

            return (uint)VSConstants.VSITEMID.Nil;
        }
    }
}
