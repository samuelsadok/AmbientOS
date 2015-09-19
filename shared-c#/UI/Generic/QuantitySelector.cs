using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppInstall.Framework;

namespace AppInstall.UI
{
    //class QuantitySelector : DataPickerView
    //{
    //    private static Func<int, int> indexToQuantity = (i) => i + 1;
    //    private static Func<int, int> quantityToIndex = (h) => h - 1;
    //
    //    public event Action<QuantitySelector> QuantityChanged;
    //
    //    /// <summary>
    //    /// Creates a view that lets the user pick a quantity and a unity.
    //    /// </summary>
    //    public QuantitySelector(IPhysicalQuantity quantity)
    //        : base(new DataPickerColumn[0])
    //    {
    //        AddColumn(new DataPickerDelimiter(" ", true));
    //        AddColumn(99, (i) => indexToQuantity(i).ToString(), quantityToIndex((int)quantity.GetQuantity(quantity.PreferredUnit)), false, false);
    //        AddColumn(new DataPickerColumn(quantity.GetUnitNames(), quantity.PreferredUnit, false, false));
    //        AddColumn(new DataPickerDelimiter(" ", true));
    //
    //        SelectionChanged += (o, e) => {
    //            if (e.Item1 == 1) {
    //                quantity.SetQuantity(indexToQuantity(e.Item2), quantity.PreferredUnit);
    //            } else if (e.Item1 == 2) {
    //                var q = quantity.GetQuantity(quantity.PreferredUnit);
    //                quantity.PreferredUnit = e.Item2;
    //                quantity.SetQuantity(q, quantity.PreferredUnit);
    //            }
    //            InvokeQuantityChanged();
    //        };
    //    }
    //
    //    private void InvokeQuantityChanged()
    //    {
    //        QuantityChanged.SafeInvoke(this);
    //    }
    //}
}