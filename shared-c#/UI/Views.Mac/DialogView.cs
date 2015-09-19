using System.Threading;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.UI
{

    public abstract class DialogView<TArg, TResult>
    {
        View parent;
        SemaphoreSlim doneSignal = new SemaphoreSlim(0, 1);

        protected abstract void Setup(TArg args);
        protected abstract NavigationPage MainPage { get; }
        protected abstract TResult Result { get; }

        protected ToolbarButton GetDoneButton()
        {
            var result = new ToolbarButton(ToolbarButton.iOSNavigationBarItemType.Done);
            result.Triggered += (o) => Dismiss();
            return result;
        }

        protected NavigationView Parent { get; private set; }

        protected void Dismiss()
        {
            doneSignal.Release();
        }

        public async Task<TResult> Show(TArg args, NavigationView parent, bool animated)
        {
            Parent = parent;
            Setup(args);

            var upperPage = parent.TopPage;
            var page = MainPage;
            bool dismissedByUI = false;
            page.WillRemoveAction = () => { dismissedByUI = true; Dismiss(); };
            parent.NavigateForward(page, animated, false);

            await doneSignal.WaitAsync();

            if (!dismissedByUI)
                parent.NavigateBack(upperPage, animated);

            return Result;
        }

        public async Task<TResult> Show(TArg args, LayerLayout parent)
        {
            NavigationView navView = new NavigationView();
            Parent = navView;
            Setup(args);

            navView.NavigateForward(MainPage);
            parent.Insert(navView, false, new Vector2D<float>(0, 1));

            await doneSignal.WaitAsync();

            parent.Remove(navView, false, new Vector2D<float>(0, 1));

            return Result;
        }


    }
}