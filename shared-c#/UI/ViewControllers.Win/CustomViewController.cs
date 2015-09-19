using System;
using System.Collections.Generic;
using AppInstall.Framework;

namespace AppInstall.UI
{
    partial class CustomViewController<T> : DataViewController<DataSource<T>>
    {
        protected override View ConstructViewEx()
        {
            return ViewConstructor();
        }
    }
}