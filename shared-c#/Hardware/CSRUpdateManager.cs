using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppInstall.Hardware;
using AppInstall.Organization;
using AppInstall.Framework;
using AppInstall.Framework.MemoryModels;

namespace AppInstall.Hardware
{
    class CSRUpdateManager
    {
        private const byte MINIMUM_SUPPORTED_BOOTLOADER_VERSION = 3; // the earliest bootloader version that is supported by this update manager
        private const byte MAXIMUM_SUPPORTED_BOOTLOADER_VERSION = 3; // the latest bootloader version that is supported by this update manager

        public class IncompatibleException : Exception
        {
            public IncompatibleException(string message)
                : base(message)
            {
            }
        }


        private enum TransportState
        {
            Ready = 1,
            InProgress = 2,     // set by the host to start firmware upload
            Paused = 3,
            Completed = 4,      // set by the host to indicate that the upload is complete
            Failed = 5,
            Aborted = 6
        }


        /// <summary>
        /// Returns true if the peripheral supports over-the-air updates and contains an outdated firmware version.
        /// </summary>
        /// <param name="getLatestFirmware">A function that returns the version of the latest firmware available for this device. If no firmware is available for this device it should return null</param>
        public static bool ShouldUpdate(BluetoothPeripheral peripheral, Func<string, string> getLatestFirmware)
        {
            if (peripheral.HasService(GlobalConstants.OTAU_BOOTLOADER_SERVICE_UUID))
                return true;

            try {
                BluetoothDeviceInfo devInfo = new BluetoothDeviceInfo(peripheral);
                peripheral.logContext.Log("Model Number: " + devInfo.ModelNumber);
                peripheral.logContext.Log("Manufacturer: " + devInfo.ManufacturerName);
                peripheral.logContext.Log("Serial Number: " + devInfo.SerialNumber);
                peripheral.logContext.Log("Hardware Revision: " + devInfo.HardwareRevision);
                peripheral.logContext.Log("Firmware Revision: " + devInfo.FirmwareRevision);
                peripheral.logContext.Log("Software Revision: " + devInfo.SoftwareRevision);
            } catch (Exception ex) {
                peripheral.logContext.Log("could not determine version: " + ex.ToString());
                return false;
            }

            if (!peripheral.HasService(GlobalConstants.OTAU_APPLICATION_SERVICE_UUID))
                return false;


            return false; // todo: check version
        }

        public static void Update(BluetoothPeripheral peripheral, IMemoryModel<byte> firmware, ProgressObserver progressObserver)
        {
            progressObserver.Status = "initiating...";
            if (!peripheral.HasService(GlobalConstants.OTAU_BOOTLOADER_SERVICE_UUID))
                throw new NotImplementedException(); // todo: invoke DFU mode
            
            // check version
            byte[] version = peripheral.ReadCharacteristic(GlobalConstants.OTAU_BOOTLOADER_SERVICE_UUID, GlobalConstants.OTAU_VERSION_CHARACTERISTIC_UUID);
            if (version.Count() != 1) throw new IncompatibleException("the device runs an unsupported bootloader version");
            Console.WriteLine("bootloader version: " + version[0]);
            if (version[0] < MINIMUM_SUPPORTED_BOOTLOADER_VERSION)
                throw new IncompatibleException("the device is too old to receive an update from this application (device bootloader: v" + version[0] + ", minimum supported: v" + MINIMUM_SUPPORTED_BOOTLOADER_VERSION + ")");
            if (version[0] > MAXIMUM_SUPPORTED_BOOTLOADER_VERSION)
                throw new IncompatibleException("this application is too outdated to give the device a firmware update (device bootloader: v" + version[0] + ", maximum supported: v" + MAXIMUM_SUPPORTED_BOOTLOADER_VERSION + ")");

            Upload(peripheral, firmware.Read(0, firmware.HighestAddress), progressObserver);
        }


        /// <summary>
        /// Uploads a byte array to the device using the data transfer and transfer control characteristics
        /// </summary>
        private static void Upload(BluetoothPeripheral peripheral, byte[] data, ProgressObserver progressObserver)
        {
            progressObserver.Status = "starting...";
            peripheral.WriteCharacteristic(GlobalConstants.OTAU_BOOTLOADER_SERVICE_UUID, GlobalConstants.OTAU_TRANSFER_CONTROL_CHARACTERISTIC_KEY_UUID, new byte[1] { (int)TransportState.InProgress }, null);
            progressObserver.Status = "transmitting " + data.Count() + " bytes...";
            long startTime = DateTime.Now.Ticks;
            peripheral.WriteCharacteristic(GlobalConstants.OTAU_BOOTLOADER_SERVICE_UUID, GlobalConstants.OTAU_DATA_TRANSFER_CHARACTERISTIC_KEY_UUID, data, progressObserver);
            long endTime = DateTime.Now.Ticks;
            progressObserver.Status = "terminating...";
            peripheral.WriteCharacteristic(GlobalConstants.OTAU_BOOTLOADER_SERVICE_UUID, GlobalConstants.OTAU_TRANSFER_CONTROL_CHARACTERISTIC_KEY_UUID, new byte[1] { (int)TransportState.Completed }, null);
            progressObserver.Status = "done! took " + TimeSpan.FromTicks(endTime - startTime).TotalSeconds + " seconds, thats " + TimeSpan.FromTicks(endTime - startTime).TotalMilliseconds / (double)(data.Count() / BluetoothPeripheral.MTU_SIZE) + " ms per packet @ MTU = " + BluetoothPeripheral.MTU_SIZE + " bytes";
        }
    }
}