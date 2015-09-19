using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppInstall.Framework;

namespace AppInstall.Hardware
{
    /// <summary>
    /// Represents a I2C (inter IC) interface
    /// </summary>
    public interface II2CPort
    {
        void Write(byte chip, int address, int addressLength, byte[] data);
        byte[] Read(byte chip, int address, int addressLength, int length);
    }
}