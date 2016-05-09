using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.VisualStudio
{
    class CustomHierarchyItem : HierarchyItem
    {
        public string Caption { get; set; }
        public int Icon { get; set; }

        public CustomHierarchyItem(uint parentId)
            : base(parentId, null)
        {
        }

        public override int GetProperty(__VSHPROPID propId, out object property)
        {
            switch (propId) {
                case __VSHPROPID.VSHPROPID_IconIndex: property = Icon; break;
                case __VSHPROPID.VSHPROPID_OpenFolderIconIndex: property = Icon; break;
                //case __VSHPROPID.VSHPROPID_IconHandle: property = this.GetIconHandle(); break;
                //case __VSHPROPID.VSHPROPID_OpenFolderIconHandle: property = this.GetIconHandle(); break;
                case __VSHPROPID.VSHPROPID_Name: property = Caption; break;
                case __VSHPROPID.VSHPROPID_Caption: property = Caption; break;
                default: return base.GetProperty(propId, out property);
            }

            return VSConstants.S_OK;
        }
    }
}
