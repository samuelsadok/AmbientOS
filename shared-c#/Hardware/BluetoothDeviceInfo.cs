using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using AppInstall.Framework;

namespace AppInstall.Hardware
{


    [AttributeUsage(AttributeTargets.All)]
    public class BluetoothServiceAttribute : Attribute
    {
        public Guid Guid { get; private set; }
        public BluetoothServiceAttribute(string guid)
        {
            Guid = Bluetooth.MakeGuid(guid);
        }
    }
    
    [AttributeUsage(AttributeTargets.All)]
    public class BluetoothCharacteristicAttribute : Attribute
    {
        public Guid Guid { get; private set; }
        public BluetoothCharacteristicAttribute(string guid)
        {
            Guid = Bluetooth.MakeGuid(guid);
        }
    }





    public abstract class BluetoothService
    {
        protected BluetoothPeripheral peripheral;
        protected Guid service;

        public byte[] this[Guid characteristic]
        {
            get {
                if (!peripheral.HasCharacteristic(service, characteristic)) return null;
                return peripheral.ReadCharacteristic(service, characteristic);
            }
            set { peripheral.WriteCharacteristic(service, characteristic, value); }
        }

        public BluetoothService(BluetoothPeripheral peripheral, string guid)
        {
            this.peripheral = peripheral;
            service = Bluetooth.MakeGuid(guid);
            if (!peripheral.HasService(service)) throw new NotSupportedException("the device does not implement the requested service");
        }
    }


  

    public class BluetoothDeviceInfo : BluetoothService
    {

        private string ToStringOrNull(byte[] data)
        {
            return (data == null ? null : Encoding.ASCII.GetString(data));
        }

        public string ModelNumber { get { return ToStringOrNull(this[Bluetooth.MakeGuid("2A24")]); } }
        public string SerialNumber { get { return ToStringOrNull(this[Bluetooth.MakeGuid("2A25")]); } }
        public string HardwareRevision { get { return ToStringOrNull(this[Bluetooth.MakeGuid("2A27")]); } }
        public string FirmwareRevision { get { return ToStringOrNull(this[Bluetooth.MakeGuid("2A26")]); } }
        public string SoftwareRevision { get { return ToStringOrNull(this[Bluetooth.MakeGuid("2A28")]); } }
        public string ManufacturerName { get { return ToStringOrNull(this[Bluetooth.MakeGuid("2A29")]); } }


        public BluetoothDeviceInfo(BluetoothPeripheral peripheral)
            : base(peripheral, "180A")
        {
        }
    }
}