using System;
using System.Collections.Generic;
using AppInstall.Framework;

namespace AppInstall.UI
{
    partial class CustomViewController<T> : DataViewController<DataSource<T>>
    {
        public Func<View> ViewConstructor { get; set; }
    }
}