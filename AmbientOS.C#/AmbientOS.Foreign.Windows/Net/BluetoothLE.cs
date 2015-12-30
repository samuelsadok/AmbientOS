using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Devices.Bluetooth.Advertisement;

namespace AmbientOS.Net
{
    class BluetoothLE : IBluetoothLEScannerImpl
    {
        public IBluetoothLEScanner BluetoothLEScannerRef { get; }
        

        public BluetoothLE()
        {
            BluetoothLEScannerRef = new BluetoothLEScannerRef(this);

            //var controller = BluetoothLEScannerRef.GetLifecycleController();
            //
            //controller.OnPause()






        }

        public DynamicSet<IBluetoothLEPeripheral> Scan(int parameters, Context context)
        {
            var set = new DynamicSet<IBluetoothLEPeripheral>().Retain();

            TypedEventHandler<BluetoothLEAdvertisementWatcher, BluetoothLEAdvertisementReceivedEventArgs> onReveiced = (o, e) => {
                context.Log.Debug("received advertisement");
            };

            TypedEventHandler<BluetoothLEAdvertisementWatcher, BluetoothLEAdvertisementWatcherStoppedEventArgs> onStopped = (o, e) => {
                context.Log.Debug("scanner stopped");
                // todo: handle stopped watcher
            };


            var watcher = new BluetoothLEAdvertisementWatcher();

            var controller = set.GetLifecycleController();

            controller.OnResume(() => {
                watcher.Received += onReveiced;
                watcher.Stopped += onStopped;
                watcher.Start();
            });

            controller.OnPause(() => {
                watcher.Stop();
                watcher.Received -= onReveiced;
                watcher.Stopped -= onStopped;
            });

            return set;
        }
    }
}
