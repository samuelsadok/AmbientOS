using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppInstall.Framework;

namespace AppInstall.Hardware
{

    public enum BluetoothCentralState
    {
        Unknown,        // bluetooth is resetting or in an unknown state
        Unsupported,    // the device doesn't support bluetooth
        Unauthorized,   // the app is not allowed to use bluetooth
        Off,            // bluetooth is switched off
        Ready           // the central is ready
    }

    public static class Bluetooth
    {
        public static Guid MakeGuid(string guid)
        {
            // extend 16-bit UUID according to BT 4.0 specs vol 3 part F section 3.2.1
            if (guid.Count() == 4) guid = "0000" + guid + "-0000-1000-8000-00805F9B34FB";
            return new Guid(guid);
        }
    }

}