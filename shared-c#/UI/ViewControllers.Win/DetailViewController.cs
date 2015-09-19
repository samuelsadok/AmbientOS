using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.UI
{
    public abstract partial class DetailViewController<TItem, TData> : DataViewController<TData>
        where TData : DataSource
    {
        private View emptyDetail;
        private View currentDetail;
        private LayerLayout detailContainer;

        protected abstract View ConstructMainView();

        protected void ShowDetail(TItem item)
        {
            var newView = item == null ? emptyDetail : DetailViewConstructor(this, item, false).ConstructView();
            detailContainer.Replace(currentDetail, newView, true, false);
            currentDetail = newView;
        }

        protected override View ConstructViewEx()
        {
            var mainContainer = ConstructMainView();

            if (DetailViewConstructor == null)
                return mainContainer;

            currentDetail = emptyDetail = new Label() { Text = PlaceholderText };
            detailContainer = new LayerLayout();
            detailContainer.Insert(currentDetail, false);

            var split = new SplitView() {
                LeftView = mainContainer,
                RightView = detailContainer,
            };

            return split;
        }

    }
}
