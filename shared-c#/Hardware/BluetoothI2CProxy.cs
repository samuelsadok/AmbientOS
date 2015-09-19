using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppInstall.Framework;
using AppInstall.Organization;

namespace AppInstall.Hardware
{
    public class BluetoothI2CProxy : II2CPort
    {
        public const int BUFFER_SIZE = 20; // last three bytes currently reserved for debugging

        private BluetoothPeripheral peripheral;

        public BluetoothI2CProxy(BluetoothPeripheral peripheral)
        {
            this.peripheral = peripheral;
        }

        public void Write(byte chip, int address, int addressLength, byte[] data)
        {
            SetupTransfer(chip, address, addressLength, data.Length);
            peripheral.WriteCharacteristic(GlobalConstants.GUID_I2C_PROXY_SERVICE, GlobalConstants.GUID_I2C_PROXY_DATA_TRANSFER, data);
        }

        public byte[] Read(byte chip, int address, int addressLength, int length)
        {
            if (length > BUFFER_SIZE - 3) throw new Exception();
            SetupTransfer(chip, address, addressLength, length);
            byte[] result = peripheral.ReadCharacteristic(GlobalConstants.GUID_I2C_PROXY_SERVICE, GlobalConstants.GUID_I2C_PROXY_DATA_TRANSFER);
            // if (result.Length != length) throw new Exception("device returned invalid data (got " + result.Length + " bytes, expected " + length + " bytes");
            return result.Take(length).ToArray();
        }

        private void SetupTransfer(byte chip, int address, int addressLength, int length)
        {
            if (length > BUFFER_SIZE) throw new ArgumentException("cannot transmit " + length + "bytes, maximum supported transmission length is " + BUFFER_SIZE + " bytes", "length");
            if (addressLength != 2) throw new ArgumentException(addressLength + "-byte addressing not supported, only 2-byte addressing is supported");

            byte[] setup = new byte[] {
                chip, 0,
                (byte)((address >> 0) & 0xFF),
                (byte)((address >> 8) & 0xFF),
                (byte)((length >> 0) & 0xFF),
                (byte)((length >> 8) & 0xFF)
            };

            peripheral.WriteCharacteristic(GlobalConstants.GUID_I2C_PROXY_SERVICE, GlobalConstants.GUID_I2C_PROXY_SETUP, setup);
            //GetSetup();
        }

        private void GetSetup()
        {
            byte[] result = peripheral.ReadCharacteristic(GlobalConstants.GUID_I2C_PROXY_SERVICE, GlobalConstants.GUID_I2C_PROXY_SETUP);
            peripheral.logContext.Log("setup: " + result.ToString());
        }
    }
}