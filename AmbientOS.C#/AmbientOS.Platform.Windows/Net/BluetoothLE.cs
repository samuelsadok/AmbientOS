using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Devices.Bluetooth.Advertisement;
using static AmbientOS.LogContext;

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

        public DynamicSet<IBluetoothLEPeripheral> Scan(int parameters)
        {
            var set = new DynamicSet<IBluetoothLEPeripheral>().Retain();

            TypedEventHandler<BluetoothLEAdvertisementWatcher, BluetoothLEAdvertisementReceivedEventArgs> onReveiced = (o, e) => {
                DebugLog("received advertisement");
            };

            TypedEventHandler<BluetoothLEAdvertisementWatcher, BluetoothLEAdvertisementWatcherStoppedEventArgs> onStopped = (o, e) => {
                DebugLog("scanner stopped");
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
