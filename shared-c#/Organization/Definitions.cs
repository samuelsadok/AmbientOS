using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppInstall.Organization
{

    /// <summary>
    /// Contains global constant definitions.
    /// Do not change any values if you don't know what you're doing! Most values must remain in sync with other projects.
    /// </summary>
    static class GlobalConstants
    {
        // internet domains
        public const string WEB_SERVER = "innovation-labs.appinstall.ch"; // main webserver
        public const string HOMEPAGE = "http://" + WEB_SERVER + "/"; // main webserver
        public const string CALLHOME_SERVER = "innovation-labs.appinstall.ch"; // callhome server for end user applications
        public const string SOFTWARE_PROVIDER_SERVER = CALLHOME_SERVER;
        public const int SOFTWARE_PROVIDER_SERVER_PORT = 1331;
        public static readonly string[] DATABASE_SERVERS = { "innovation-labs.appinstall.ch,1338", "192.168.178.32", "localhost" };


        // branding
        public static AppInstall.Graphics.Color UIColor = AppInstall.Graphics.Color.Green;


        // Bluetooth service IDs
        public static Guid FLIGHT_SERVICE_UUID = new Guid("E20A39F4-73F5-4BC4-A12F-17D1AD07A961"); // exposes flight parameters
        public static Guid FLIGHT_CONTROL_UUID = new Guid("08590F7E-DB05-467E-8757-72F6FAEB13D4"); // the current control input (throttle, yaw-pitch-roll)
        public static Guid FLIGHT_CONFIG_UUID = new Guid("1D662171-E569-4CAA-A08A-CBFFACD9EB24"); // the current flight controller parameters
        public static Guid MOTION_SERVICE_UUID = new Guid("47C751E2-5DF4-480B-8E5E-FA0E861B3E55"); // reports the device's motion data
        public static Guid MOTION_ATTITUDE_UUID = new Guid("CC767FE7-192D-4675-B313-5117AAADF1C5"); // the current device attitude
        public static Guid MOTION_ACCELERATION_UUID = new Guid("18DE0712-A406-4F4C-A8BA-1B52BCF294D1"); // the current accelerometer measurement
        public static Guid CONNECTION_MGR_SERVICE_UUID = new Guid("93236A4C-2424-4E86-9A68-E291585B1A59"); // exposes fields related to the wireless connection
        public static Guid CONNECTION_MGR_IDENTIFY_CHARACTERISTIC_UUID = new Guid("1ABFCC02-F883-469F-8868-FC8B8F2DD982"); // set to 1 to make the device blink its LEDs

        // defined by CSR
        public static Guid OTAU_BOOTLOADER_SERVICE_UUID = new Guid("00001010-d102-11e1-9b23-00025b00a5a5");
        public static Guid OTAU_APPLICATION_SERVICE_UUID = new Guid("00001016-d102-11e1-9b23-00025b00a5a5");
        public static Guid OTAU_VERSION_CHARACTERISTIC_UUID = new Guid("00001011-d102-11e1-9b23-00025b00a5a5"); // get version (8-bit, bootloader only)
        public static Guid OTAU_CURRENT_APP_CHARACTERISTIC_UUID = new Guid("00001013-d102-11e1-9b23-00025b00a5a5"); // get/set application number 8-bit
        public static Guid OTAU_DATA_TRANSFER_CHARACTERISTIC_KEY_UUID = new Guid("00001014-d102-11e1-9b23-00025b00a5a5"); // contains the requested data (read) or receives data (write) ((ATT_MTU-3) bytes (20 bytes))
        public static Guid OTAU_TRANSFER_CONTROL_CHARACTERISTIC_KEY_UUID = new Guid("00001015-d102-11e1-9b23-00025b00a5a5"); // 16-bit, bootloader only
        public static Guid OTAU_READ_CS_KEY_CHARACTERISTIC_UUID = new Guid("00001017-d102-11e1-9b23-00025b00a5a5"); // 8-bit, application only
        public static int OTAU_KEY_NOT_READ = 0x80;


        public static Guid GUID_I2C_PROXY_SERVICE = new Guid("827815A9-8E5F-4D71-84E3-71B497718D21");
        public static Guid GUID_I2C_PROXY_SETUP = new Guid("574D8970-CC06-4FC9-8C05-15811F5C1919");
        public static Guid GUID_I2C_PROXY_DATA_TRANSFER = new Guid("80B920BF-3996-444E-8270-8A88685EA991");



    }

}