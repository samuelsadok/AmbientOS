using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppInstall.Framework
{
    public interface IRecyclableItem<T>
    {
        void Setup(T parameters);
    }

    public interface IRecyclableItemContainer
    {
        bool Recycle<T>(Guid providerGuid, out T item);
    }

    /// <summary>
    /// This is not tested and no longer used and will most likely be discarted.
    /// </summary>
    public class RecyclableItemProvider<TParam, TItem>
    {
        private Guid guid = new Guid();
        private IRecyclableItemContainer container;
        Func<Guid, TItem> constructItem;
        Action<TItem, TParam> setupItem;

        public RecyclableItemProvider(IRecyclableItemContainer container, Func<Guid, TItem> constructItem, Action<TItem, TParam> setupItem)
            : this(new Guid(), container, constructItem, setupItem)
        {

        }

        private RecyclableItemProvider(Guid guid, IRecyclableItemContainer container, Func<Guid, TItem> constructItem, Action<TItem, TParam> setupItem)
        {
            this.guid = guid;
            this.container = container;
            this.constructItem = constructItem;
            this.setupItem = setupItem;
        }

        public TItem ProvideItem(TParam parameters)
        {
            TItem item;
            if (!container.Recycle(guid, out item))
                item = constructItem(guid);
            setupItem(item, parameters);
            return item;
        }

        public static explicit operator RecyclableItemProvider<object, MonoTouch.UIKit.UITableViewCell> (RecyclableItemProvider<TParam, TItem> obj) {
            return new RecyclableItemProvider<object, MonoTouch.UIKit.UITableViewCell>(obj.guid, obj.container, (guid) => {
                try {
                    return (MonoTouch.UIKit.UITableViewCell)(object)obj.constructItem(guid);
                } catch {
                    throw;
                }
            }
            , (item, param) => {
                try {
                    object i1 = item;
                    TItem i2 = (TItem)i1;
                    TParam p = (TParam)param;
                    obj.setupItem(i2, p);
                } catch {
                    throw;
                }
            });
        }
    }
}