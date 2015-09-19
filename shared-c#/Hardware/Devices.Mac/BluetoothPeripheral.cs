
//#define BT_DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using AppInstall.Framework;
using Foundation;
using CoreBluetooth;


namespace AppInstall.Hardware
{


    public class BluetoothPeripheral
    {
        public const int MTU_SIZE = 20;

        public readonly LogContext logContext;

        public readonly CBPeripheral peripheral;
        public readonly Mutex comMutex = new Mutex(); // used to ensure exclusive communication lock - (never acquire within a data lock!)
        object activeProcesses; // keeps track of the currently running actions on peripheral
        Exception lastException;




        private CountdownEvent ActiveProcesses
        {
            get {
                Thread.MemoryBarrier();
                CountdownEvent result;
                while ((result = (CountdownEvent)Thread.VolatileRead(ref activeProcesses)) == null) {
                    logContext.Log("counter is null");
                    Thread.Sleep(100);
                }
                return result;
            }
            set { Thread.VolatileWrite(ref activeProcesses, value); Thread.MemoryBarrier(); }
        }

        /// <summary>
        /// The local name advertised by the device (can be null)
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Manufacturer specific data (can be null)
        /// </summary>
        public byte[] ManufacturerData { get; private set; }
        /// <summary>
        /// A list of all services provided by the device
        /// </summary>
        public Guid[] ServiceUUIDs { get; private set; }
        /// <summary>
        /// Transmit power (0 if not applicable)
        /// </summary>
        public float TxPower { get; private set; }
        /// <summary>
        /// Indicates whether the application can subscribe to the service (?) (false if unknown)
        /// </summary>
        public bool IsConnectable { get; private set; }
        /// <summary>
        /// The reception strength
        /// </summary>
        public int RSSI { get; private set; }
        /// <summary>
        /// Indicates whether the device is connected (or in the process of connecting)
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Triggered when the metadata of this device are updated (e.g. name, list of services, etc).
        /// </summary>
        public event Action<BluetoothPeripheral> InfoChanged;

        
        private Dictionary<Guid, Dictionary<Guid, object>> services = new Dictionary<Guid, Dictionary<Guid, object>>(); // used for data lock (on services, characteristics and descriptors)
        


        public BluetoothPeripheral(CBPeripheral peripheral, NSDictionary advertismentData, int RSSI, LogContext logContext)
        {
            this.logContext = logContext;

            this.peripheral = peripheral;
            this.RSSI = RSSI;

            //ServiceData = new Dictionary<Guid, byte[]>();

            Name = peripheral.Name;
            UpdateData(advertismentData, RSSI);
            //if (Name == null) throw new ArgumentException();




            Action<string, NSError, Action> bluetoothHandler = (eventName, error, action) => {
                try {
                    if (error != null)
                        throw error.ToException();
#if BT_DEBUG
                    LogSystem.Log("BTP: " + eventName);
#endif
                    if (action != null) action();
                } catch (Exception ex) {
                    logContext.Log("bluetooth error (event: " + eventName + "): " + ex.ToString());
                    lastException = ex;
                } finally {
                    ActiveProcesses.Signal();
                }
            };


            peripheral.DiscoveredIncludedService += (o, e) => bluetoothHandler("discovered included service", e.Error, null);
            peripheral.ModifiedServices += (o, e) => bluetoothHandler("modified services", null, null);
            peripheral.RssiUpdated += (o, e) => bluetoothHandler("updated rssi", e.Error, null);
            peripheral.UpdatedName += (o, e) => bluetoothHandler("updated name", null, null);
            peripheral.UpdatedNotificationState += (o, e) => bluetoothHandler("updated notification state", e.Error, null);
            peripheral.UpdatedValue += (o, e) => bluetoothHandler("updated value " + e.Descriptor.Characteristic, e.Error, null);
            peripheral.WroteDescriptorValue += (o, e) => bluetoothHandler("wrote descriptor value", e.Error, null);



            // *************  events triggerd during peripheral exploration  *************

            peripheral.DiscoveredService += (o, e) => bluetoothHandler("discovered service", e.Error, () => {
                foreach (CBService s in peripheral.Services) {
                    ActiveProcesses.AddCount();
                    peripheral.DiscoverCharacteristics(s);
                }
            });

            peripheral.DiscoveredCharacteristic += (o, e) => bluetoothHandler("discovered characteristic", e.Error, () => {
                foreach (CBCharacteristic c in e.Service.Characteristics) {
                    ActiveProcesses.AddCount();
                    peripheral.DiscoverDescriptors(c);
                }
            });

            peripheral.DiscoveredDescriptor += (o, e) => bluetoothHandler("discovered descriptor", e.Error, null);



            // *************  events triggered during characteristic/descriptor read/write operation  *************

            peripheral.UpdatedCharacterteristicValue += (o, e) => bluetoothHandler("updated characteristic", e.Error, null); // characteristic read completed
            peripheral.WroteCharacteristicValue += (o, e) => bluetoothHandler("wrote characteristic", e.Error, null); // characteristic write completed
        }


        public void UpdateData(NSDictionary advertismentData, int RSSI)
        {
            lock (services) {
                //peripheralStillThere.Set();
                foreach (NSString key in advertismentData.Keys) {
                    NSObject value = advertismentData.ValueForKey(key);
                    if (key == CBAdvertisement.DataLocalNameKey) {
                        Name = (NSString)value;
                    } else if (key == CBAdvertisement.DataManufacturerDataKey) {
                        ManufacturerData = ((NSData)value).ToByteArray();
                    } else if (key == CBAdvertisement.DataServiceDataKey) {
                        // foreach (CBUUID k in ((NSDictionary)value).Keys)
                        //     ServiceData[ToGuid(k)] = ToByteArray((NSData)(((NSDictionary)value)[k]));
                        // todo: Console.WriteLine("todo");
                    } else if ((key == CBAdvertisement.DataServiceUUIDsKey) || (key == CBAdvertisement.DataOverflowServiceUUIDsKey) || (key == CBAdvertisement.DataSolicitedServiceUUIDsKey)) {
                        // todo: Console.WriteLine("todo");
                    } else if (key == CBAdvertisement.DataTxPowerLevelKey) {
                        TxPower = (float)(NSNumber)value;
                    } else if (key == CBAdvertisement.IsConnectable) {
                        IsConnectable = (((int)(NSNumber)value) == 0) ? false : true;
                    }
                }
            }

            InfoChanged.SafeInvoke(this);
        }



        /// <summary>
        /// Starts discovering of all services and their characteristics
        /// </summary>
        public void RetrieveDetails()
        {
            comMutex.WaitOne();

            try {
                // discover services and wait until done
                lastException = null;
                ActiveProcesses = new CountdownEvent(1);
                logContext.Log("now discovering");
                peripheral.DiscoverServices();
                logContext.Log("waiting");
                ActiveProcesses.Wait(); // todo: allow cancelling
                logContext.Log("done discovering");
                ActiveProcesses = null;

                // copy data into our store
                lock (services) {
                    services.Clear();
                    foreach (CBService service in (peripheral.Services == null ? new CBService[0] : peripheral.Services)) {
                        Dictionary<Guid, object> serviceContent = new Dictionary<Guid, object>();
                        services[service.UUID.ToGuid()] = serviceContent;
                        foreach (CBCharacteristic characteristic in (service.Characteristics == null ? new CBCharacteristic[0] : service.Characteristics)) {
                            serviceContent[characteristic.UUID.ToGuid()] = characteristic;
                            foreach (CBDescriptor descriptor in (characteristic.Descriptors == null ? new CBDescriptor[0] : characteristic.Descriptors))
                                serviceContent[descriptor.UUID.ToGuid()] = descriptor;
                        }
                    }
                }
            } finally {
                comMutex.ReleaseMutex();
            }

            lock (services) {
                foreach (var service in services) {
                    logContext.Log("  service: " + service.Key.ToString());
                    foreach (var content in service.Value) {
                        if (content.Value.GetType() == typeof(CBCharacteristic))
                            logContext.Log("    characteristic: " + content.Key.ToString());
                        else
                            logContext.Log("    descriptor: " + content.Key.ToString());
                    }
                }
            }
        }


        /// <summary>
        /// Determines if the device supports the specified service
        /// </summary>
        public bool HasService(Guid service)
        {
            lock (services)
                return services.ContainsKey(service);
        }

        /// <summary>
        /// Determines if the device supports the specified characteristic
        /// </summary>
        public bool HasCharacteristic(Guid service, Guid characteristic)
        {
            lock (services) {
                Dictionary<Guid, object> s;
                object o;
                if (!services.TryGetValue(service, out s))
                    return false;
                if (!s.TryGetValue(characteristic, out o))
                    return false;
                return o.GetType() == typeof(CBCharacteristic);
            }
        }


        /// <summary>
        /// Reads the value of a characteristic or descriptor on the remote device
        /// </summary>
        public async Task<byte[]> ReadCharacteristic(Guid service, Guid characteristic, CancellationToken cancellationToken)
        {
            object o;
            lock (services)
                o = services[service][characteristic];

            await comMutex.WaitAsync(cancellationToken);

            try {

                lastException = null;

                ActiveProcesses = new CountdownEvent(1);
                if (o.GetType() == typeof(CBCharacteristic))
                    peripheral.ReadValue((CBCharacteristic)o);
                else
                    peripheral.ReadValue((CBDescriptor)o); // todo: add descriptor support
                await ActiveProcesses.WaitHandle.WaitAsync(cancellationToken); // todo: allow cancelling
                ActiveProcesses = null;
                if (lastException != null) throw new Exception("bluetooth error", lastException);

            } finally {
                comMutex.ReleaseMutex();
            }

            if (o.GetType() == typeof(CBCharacteristic))
                return (((CBCharacteristic)o).Value).ToByteArray();
            else
                throw new NotImplementedException(); // todo: return descriptor value
            //return ToByteArray(((CBDescriptor)o).Value);
        }
    

        /// <summary>
        /// Sets the value of a characteristic or descriptor on the remote device.
        /// Using a progress observer is advised if the value is very large.
        /// </summary>
        public async Task WriteCharacteristic(Guid service, Guid characteristic, byte[] value, CancellationToken cancellationToken, ProgressObserver progressObserver = null)
        {
            NSData data = NSData.FromArray(value);

            object o;
            lock (services)
                o = services[service][characteristic];
            if (o.GetType() != typeof(CBCharacteristic)) throw new KeyNotFoundException(); // todo: add descriptor support

            LinearProgressMonitor progressMonitor = new LinearProgressMonitor(value.Count());
            if (progressObserver != null) progressObserver.Monitor = progressMonitor;
            
            using (UnmanagedMemory mem = new UnmanagedMemory(value.Count())) {
                Marshal.Copy(value, 0, mem.Handle, value.Count());
                
                await Utilities.PartitionWork(0, value.Count(), MTU_SIZE, async (i, count) => {
                    await WriteCharacteristic((CBCharacteristic)o, NSData.FromBytesNoCopy(IntPtr.Add(mem.Handle, i), (uint)count, false), cancellationToken);
                    progressMonitor.Advance(count);
                });
                progressMonitor.Complete();
            }
        }

        private async Task WriteCharacteristic(CBCharacteristic characteristic, NSData value, CancellationToken cancellationToken)
        {
            await comMutex.WaitAsync(cancellationToken);
            try {
                lastException = null;
                ActiveProcesses = new CountdownEvent(1);
                peripheral.WriteValue(value, characteristic, CBCharacteristicWriteType.WithResponse);
                await ActiveProcesses.WaitHandle.WaitAsync(cancellationToken); // todo: allow cancelling // todo: detect completition
                ActiveProcesses = null;
                if (lastException != null) throw new Exception("bluetooth error", lastException);
            } finally {
                comMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Enables reception of notifications on the specified characteristic
        /// </summary>
        public void EnableNotifications(Guid service, Guid characteristic)
        {
            object o;
            lock (services)
                o = services[service][characteristic];
            if (o.GetType() != typeof(CBCharacteristic))
                throw new KeyNotFoundException("the selected GUID is not a characteristic");

            comMutex.WaitOne();

            try {
                lastException = null;
                ActiveProcesses = new CountdownEvent(1);
                peripheral.SetNotifyValue(true, (CBCharacteristic)o);
                throw new NotImplementedException("need to implement update handler using UpdatedValue or UpdatedNotificationState");
                ActiveProcesses.Wait(); // todo: allow cancelling // todo: detect completition
                ActiveProcesses = null;
                if (lastException != null) throw new Exception("bluetooth error", lastException);
            } finally {
                comMutex.ReleaseMutex();
            }
        }
    }

 
}