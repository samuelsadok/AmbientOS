using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppInstall.Framework;
using AppInstall.Hardware;

namespace AppInstall.UI
{
    [Obsolete("use common view controller model", true)]
    public class BluetoothPeripheralListItem : ListViewItem
    {
        Button btn = new Button() {
            Location = new Vector2D<float>(10, 0),
            Size = new Vector2D<float>(70, 30),
            Text = "identify"
        };

        private BluetoothPeripheral device;
        private EventHandler<object> identifyAction;

        public BluetoothPeripheralListItem(Guid typeGuid)
            : base(typeGuid, false)
        {
            LogSystem.Log("item construction");
            btn.Clicked += (o) => identifyAction.SafeInvoke(this, device);
            AccessoryView = btn;
        }

        public override void Setup(object data, params EventHandler<object>[] eventHandlers)
        {
            LogSystem.Log("item setup");
            device = (BluetoothPeripheral)data;

            TextLabel.Text = device.Name;

            identifyAction = eventHandlers[0];
        }
    }
}