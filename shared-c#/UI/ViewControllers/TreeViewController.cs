using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{

    /// <summary>
    /// Displays a tree of items, while a tree item may have a different
    /// appearance than a leaf item.
    /// </summary>
    public partial class TreeViewController<TTree, TItem> : DetailViewController<TItem, TreeSource<TTree, TItem>>
    {
        public Field<TTree>[] FolderFields { get; set; }
        public Field<TItem>[] ItemFields { get; set; }

        public override void DidUpdate(TItem item)
        {
            throw new NotImplementedException("can't update tree items");
        }

        public override void Discard(TItem item)
        {
            throw new NotImplementedException("can't remove tree items");
        }
    }
}
