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
    /// Displays all specified fields for the associated data.
    /// </summary>
    /// <typeparam name="T">the underlying data type for which this view should display fields</typeparam>
    public partial class EditorViewController<T> : DataViewController<DataSource<T>>
    {
        public Field<T>[] Fields { get; set; }

        public event Action DidEdit;
    }
}
