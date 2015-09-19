using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Foundation;
using CoreBluetooth;
using AppInstall.Framework;
using AppInstall.OS;

namespace AppInstall.Hardware
{
    /// <summary>
    /// Provides functions to use bluetooth low energy on the platform.
    /// </summary>
    public static class BluetoothCentral
    {
        /// <summary>
        /// Used internally to store scan information when a new data source is generated.
        /// </summary>
        class BTScan
        {
            public CollectionSource<BluetoothPeripheral> Peripherals;
            public CBUUID[] UUIDs; // can be null
            public Action<string> ScanFailedCallback; // invoked when the scan was stopped, passing an error message (may be invoked multiple times)

            public bool Applies(CBPeripheral peripheral)
            {
                return UUIDs == null ? true : peripheral.Services.Select(service => service.UUID).Intersect(UUIDs).Any();
            }
        };


        /// <summary>
        /// Log context to be used by the bluetooth central
        /// </summary>
        public static LogContext LogContext { get; set; }

        /// <summary>
        /// The platform specific bluetooth central
        /// </summary>
        static CBCentralManager central;

        /// <summary>
        /// A list of all peripherals that are currently in range.
        /// If a device is not used or seen for some time, it is automatically removed from the list.
        /// </summary>
        static VolatileList<CBPeripheral, Tuple<NSDictionary, int>> peripherals;

        /// <summary>
        /// A dictionary of all peripherals that have been discovered, together with their respective platform independent abstraction
        /// </summary>
        static Dictionary<CBPeripheral, BluetoothPeripheral> availablePeripherals;

        /// <summary>
        /// Required for the synchronous connect function, since the native framework uses asynchronous methods.
        /// </summary>
        static Dictionary<CBPeripheral, Action<NSError>> connectionAttemptDoneHandler;

        /// <summary>
        /// Lists all scans that are currently active.
        /// If bluetooth becomes available, a scan is started for all of these filters.
        /// If no scans are active, the central stops scanning.
        /// </summary>
        static HashSet<BTScan> activeScans;



        /// <summary>
        /// The bluetooth central started to connect
        /// </summary>
        public static event Action<BluetoothPeripheral> ConnectionInitiated;
        /// <summary>
        /// The device was paired successfully
        /// </summary>
        public static event Action<BluetoothPeripheral> ConnectionEstablished;
        /// <summary>
        /// The connection to the device was closed. Also triggered when a connection attempt failed.
        /// </summary>
        public static event Action<BluetoothPeripheral> ConnectionClosed;




        /// <summary>
        /// Initializes the bluetooth central.
        /// Until this function is called, bluetooth central functions are not operational.
        /// </summary>
        static void Init()
        {
            if (central != null)
                return; // not thread safe!

            // init static variables
            central = new CBCentralManager(new CoreFoundation.DispatchQueue("bluetooth dispatch queue"));
            peripherals = new VolatileList<CBPeripheral, Tuple<NSDictionary, int>>(TimeSpan.FromSeconds(5));
            availablePeripherals = new Dictionary<CBPeripheral, BluetoothPeripheral>();
            connectionAttemptDoneHandler = new Dictionary<CBPeripheral, Action<NSError>>();
            activeScans = new HashSet<BTScan>();



            central.UpdatedState += (o, e) => central_UpdatedState();
            central.DiscoveredPeripheral += (o, e) => central_DiscoveredPeripheral(e.Peripheral, e.AdvertisementData, (int)e.RSSI);
            central.ConnectedPeripheral += (o, e) => central_ConnectPeripheralDone(e.Peripheral, null);
            central.FailedToConnectPeripheral += (o, e) => central_ConnectPeripheralDone(e.Peripheral, e.Error);
            central.DisconnectedPeripheral += (o, e) => central_DisconnectedPeripheral(e.Peripheral, e.Error);
            central.RetrievedPeripherals += (o, e) => LogContext.Log("retrieved peripherals");
            central.RetrievedConnectedPeripherals += (o, e) => LogContext.Log("retrieved connected peripherals");



            peripherals.FoundObject += (o, e) => {
                // todo create peripheral data source
                LogContext.Log("found peripheral");
                BluetoothPeripheral p = new BluetoothPeripheral(e.Item1, e.Item2.Item1, e.Item2.Item2, LogContext);
                availablePeripherals[e.Item1] = p;

                lock (activeScans) {
                    foreach (var scan in activeScans.Where(s => s.Applies(e.Item1))) {
                        Platform.InvokeMainThread(() => scan.Peripherals.Add(p));
                        p.InfoChanged += (obj) => Platform.InvokeMainThread(() => scan.Peripherals.UpdateItem(obj));
                    }
                }
            };
            peripherals.TouchedObject += (o, e) => availablePeripherals[e.Item1].UpdateData(e.Item2.Item1, e.Item2.Item2); ;
            peripherals.LostObject += (o, e) => {
                BluetoothPeripheral p = availablePeripherals[e];
                if (p.IsConnected)
                    throw new Exception("can't lose peripheral while it is connected");
                availablePeripherals.Remove(e);

                lock (activeScans)
                    foreach (var scan in activeScans)
                        scan.Peripherals.Remove(p);
            };
            peripherals.StartMonitoring(ApplicationControl.ShutdownToken);
        }


        /// <summary>
        /// Creates a collection source that is updated whenever a relevant peripheral is discovered or lost.
        /// A peripheral is never lost while connected.
        /// If a refresh is triggerd on the data source, the bluetooth central starts scanning.
        /// If the bluetooth central becomes unavailable, the refresh fails and is restarted when the central becomes available again.
        /// </summary>
        /// <param name="services">The service GUID's that should be scanned for. A peripheral is added to the list if it support's any of these services. Set to null to accept all peripherals.</param>
        /// <param name="refreshOnMainThread">If true, any updates to the collection are done on the main thread. This is useful if the list is observed by a GUI.</param>
        public static CollectionSource<BluetoothPeripheral> CreateDeviceSource(Guid[] services, bool refreshOnMainThread)
        {
            Init();

            string scanFailMsg = null;
            var scanFailed = new ManualResetEvent(false);

            BTScan scan = new BTScan() {
                UUIDs = services == null ? null : services.Select(uuid => CBUUID.FromString(uuid.ToString())).ToArray(),
                ScanFailedCallback = msg => { scanFailMsg = msg; scanFailed.Set(); }
            };

            return scan.Peripherals = new CollectionSource<BluetoothPeripheral>(c => {
                lock (activeScans) {
                    activeScans.Add(scan);
                    scanFailed.Reset();
                    UpdateScanState();
                }

                scanFailed.WaitOne();
                //activeScans.Remove(scan);
                throw new Exception(scanFailMsg);
            }, null, refreshOnMainThread, null);
        }


        /// <summary>
        /// Restarts the scan with updated settings taken from the activeScans list.
        /// If no scans are active, the scan is stopped.
        /// </summary>
        static void UpdateScanState()
        {
            lock (activeScans) {
                central.StopScan();
                if (activeScans.Any()) {
                    if (central.State == CBCentralManagerState.PoweredOn) {
                        CBUUID[] uuids = null;
                        if (!activeScans.Any(scan => scan.UUIDs == null))
                            uuids = activeScans.SelectMany(scan => scan.UUIDs).ToArray();
                        central.ScanForPeripherals(uuids, new NSDictionary(CBCentralManager.ScanOptionAllowDuplicatesKey, true));

                        foreach (var scan in activeScans)
                            scan.Peripherals.SoftRefresh(ApplicationControl.ShutdownToken).Run();

                    } else {

                        string msg;
                        switch (central.State) {
                            case CBCentralManagerState.Unsupported: msg = "This device does not have Bluetooth v4.0. You need an iPhone 4S, iPad 3, iPod Touch 5 or newer.\n\n" + "Future support is planned for these devices: MacBook Air 2011, MacBook Pro 2012, Mac Mini"; break;
                            case CBCentralManagerState.Unauthorized: msg = "Please allow this App to use Bluetooth"; break;
                            case CBCentralManagerState.PoweredOff: msg = "Please go to Settings and turn on Bluetooth"; break;
                            default: msg = "Unknown bluetooth state. Please contact support."; break;
                        }

                        foreach (var scan in activeScans)
                            scan.ScanFailedCallback.SafeInvoke(msg);
                    }
                }
            }
        }



        static void central_UpdatedState()
        {
            LogContext.Log("state changed to " + central.State);
            lock (activeScans) {
                peripherals.Clear();
                UpdateScanState();
            }
        }


        private static void central_DiscoveredPeripheral(CBPeripheral peripheral, NSDictionary advertismentData, int RSSI)
        {
            LogContext.Log("discovered peripheral");
            peripherals.Touch(peripheral, new Tuple<NSDictionary, int>(advertismentData, RSSI));
        }

        private static void central_ConnectPeripheralDone(CBPeripheral peripheral, NSError error)
        {
            LogContext.Log("connected peripheral (or failed)");
            if (connectionAttemptDoneHandler.ContainsKey(peripheral))
                connectionAttemptDoneHandler[peripheral](error);
        }

        private static void central_DisconnectedPeripheral(CBPeripheral peripheral, NSError error)
        {
            LogContext.Log("disconnected peripheral");
            availablePeripherals[peripheral].IsConnected = false;
            ConnectionClosed.SafeInvoke(availablePeripherals[peripheral]);
            peripherals.MakeVolatile(peripheral);
        }





        /// <summary>
        /// Connects to a peripheral
        /// </summary>
        /// <exception cref="InvalidOperationException">The connection attempt failed</exception>
        /// <exception cref="TimeoutException">The connection attempt timed out</exception>
        public static void ConnectPeripheral(BluetoothPeripheral peripheral)
        {
            CBPeripheral p = peripheral.peripheral;

            peripherals.MakeResilent(p);

            lock (p) {
                peripheral.IsConnected = true;
                ConnectionInitiated.SafeInvoke(peripheral);

                try {
                    AutoResetEvent done = new AutoResetEvent(false);
                    NSError error = null;

                    connectionAttemptDoneHandler[p] = (err) => {
                        error = err;
                        done.Set();
                    };
                    
                    central.ConnectPeripheral(p);
                    if (!done.WaitOne(10000)) { central.CancelPeripheralConnection(p); throw new TimeoutException(); }

                    if (error != null) throw new InvalidOperationException();

                    ConnectionEstablished.SafeInvoke(peripheral);

                } catch (Exception) {
                    peripheral.IsConnected = false;
                    ConnectionClosed.SafeInvoke(peripheral);
                    peripherals.MakeVolatile(p);
                    throw;
                }
            }
        }

        public static void DisconnectPeripheral(BluetoothPeripheral peripheral)
        {

            // todo: disconnect

            lock (peripheral.peripheral) {
                LogContext.Log("connection closing");
            }
        }
    }
}