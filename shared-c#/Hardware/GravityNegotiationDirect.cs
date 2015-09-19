using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using AppInstall.Framework;

namespace AppInstall.Hardware
{
    public class GravityNegotiationDirect
    {
        private const int MAX_SUPPORTED_VERSION = 1;
        
        private const int FULL_THROTTLE = 0x3FFF;
        private const float LOOP_PERIOD = 0.003f;

        private const int LOG_BUFFER_SIZE = 256;

        private const int YPR_SIZE = 7;
        private const int PID_SIZE = 24;
        private const int KALMAN_SIZE = 7;
        private const int CONTROL_STRUCT_SIZE = 2 + YPR_SIZE;
        private const int CONTROL_STRUCT_OFFSET = 2;
        private const int CONFIG_STRUCT_SIZE = 3 * PID_SIZE + 3 * KALMAN_SIZE;
        private const int CONFIG_STRUCT_OFFSET = CONTROL_STRUCT_OFFSET + CONTROL_STRUCT_SIZE;
        private const int STATE_STRUCT_SIZE = 2 * YPR_SIZE;
        private const int STATE_STRUCT_OFFSET = CONFIG_STRUCT_OFFSET + CONFIG_STRUCT_SIZE;
        private const int LOG_STRUCT_SIZE = 2 + 4 * LOG_BUFFER_SIZE;
        private const int LOG_STRUCT_OFFSET = STATE_STRUCT_OFFSET + STATE_STRUCT_SIZE;





        #region "Structures"

        private const int I2C_SLAVE_ADDRESS = 0x42;
        private const int I2C_SLAVE_ADDRESS_BYTES = 2;

        // These declarations mirror the datastructures found in the registerfile of the device

        struct YPR
        {
            Int16 Yaw;
            Int16 Pitch;
            Int16 Roll;
            byte Flipped;
        }

        struct Kalman
        {
            byte reactiveness; // this parameter defines the behavior of the filter. 0xFF: immediate reaction, no noise rejection, 0x00: very slow reaction, maximum noise rejection
            byte predictionTime; // number of time steps that the filter will predict into the future. Example: Kalman Filter executed every 1ms, predictionTime = 100 -> filter output is the prediction for 100ms into the future
            Int16 lastPrediction;
            byte lastSlopeB0, lastSlopeB1, lastSlopeB2;
        }
        
        struct PID
        {
            float P;		// proportional coefficient
            float I;		// integral coefficient
            float D;		// derivative coefficient
            float ILimitP;	// positive limit for the integrated error (can grow quickly)
            float ILimitN;	// negative limit
            float ErrSum;	// used by the I-controller
        }


        struct RegisterFile
        {
            UInt16 Version;
            struct ControlInput
            {
                Int16 Throttle;
                YPR Attitude;
            }
            struct Configuration
            {
                Kalman KalmanY;
                Kalman KalmanP;
                Kalman KalmanR;
                PID PidY;
                PID PidP;
                PID PidR;
            }
            struct PhysicalState
            {
                YPR Attitude;
                YPR AngularRate;
            }
        }

        #endregion



        private readonly II2CPort port;

        private float P, I, D, ILimit, A, T;

        private readonly SlowAction readDataAction;
        private readonly SlowAction readLogAction;
        private readonly SlowAction controlAction;
        private readonly SlowAction configureAction;

        
        public int Version { get; private set; }
        public YawPitchRoll Attitude { get; private set; }
        public YawPitchRoll AngularRate { get; private set; }
        
        public float Throttle { get; set; }
        public YawPitchRoll ControlAttitude { get; set; }

        public float[] PitchSensorLog { get; private set; }
        public float[] PitchActionLog { get; private set; }


        public GravityNegotiationDirect(II2CPort port)
        {
            readDataAction = new SlowAction(ReadDataEx);
            readLogAction = new SlowAction(ReadLogEx);
            controlAction = new SlowAction(ControlEx);
            configureAction = new SlowAction(ConfigureEx);

            //byte[] bla;
            //bla = new byte[]{ 0x12, 0x23, 0x34, 0x45, 0x56, 0x67, 0x78, 0x89, 0x9A, 0xAB, 0xBC, 0xCD, 0xDE, 0xEF, 0xF1, 0x13, 0x24, 0x35, 0x46, 0x57 };
            //port.Write(I2C_SLAVE_ADDRESS, 0x1337, 2, bla);
            //byte[] bla2 = port.Read(I2C_SLAVE_ADDRESS, 0x1337, 2, 20);
            this.port = port;
            byte[] version = port.Read(I2C_SLAVE_ADDRESS, 0, I2C_SLAVE_ADDRESS_BYTES, 2);
            Version = BitConverter.ToInt16(version, 0);
            LogSystem.Log("actual firmware version: " + Version);
            if (Version > MAX_SUPPORTED_VERSION) throw new NotSupportedException("the device is too new");
            ControlAttitude = new YawPitchRoll();
        }

        /// <summary>
        /// blocks until data is read
        /// </summary>
        public void ReadData()
        {
            readDataAction.Trigger().WaitOne();
        }

        public void ReadLog()
        {
            readLogAction.Trigger().WaitOne();
        }

        public WaitHandle Control()
        {
            return controlAction.Trigger();
        }

        public WaitHandle Configure(float p, float i, float d, float iLimit, float a, float t)
        {
            P = p;
            I = i;
            D = d;
            ILimit = iLimit;
            A = a;
            T = t;
            return configureAction.Trigger();
        }


        #region "Implementation"


        private float AngleFromData(byte[] data, int offset)
        {
            return (float)BitConverter.ToInt16(data, offset) / (float)0x4000 * (float)Math.PI;
        }

        private void AngleToData(byte[] data, int offset, float angle)
        {
            Array.Copy(BitConverter.GetBytes((UInt16)(angle / (float)Math.PI * (float)0x4000)), 0, data, offset, 2);
        }

        private void ReadDataEx()
        {
            byte[] data = port.Read(I2C_SLAVE_ADDRESS, STATE_STRUCT_OFFSET, I2C_SLAVE_ADDRESS_BYTES, STATE_STRUCT_SIZE);
            float y1 = AngleFromData(data, 0), p1 = AngleFromData(data, 2), r1 = AngleFromData(data, 4);
            float y2 = AngleFromData(data, 7), p2 = AngleFromData(data, 9), r2 = AngleFromData(data, 11);
            Attitude = new YawPitchRoll(y1, p1, r1);
            AngularRate = new YawPitchRoll(y2, p2, r2);
        }

        private void ReadLogEx()
        {
            byte[] data = new byte[LOG_STRUCT_SIZE];
            Utilities.PartitionWork(0, LOG_STRUCT_SIZE, 17, (start, count) => {
                Array.Copy(port.Read(I2C_SLAVE_ADDRESS, LOG_STRUCT_OFFSET + start, I2C_SLAVE_ADDRESS_BYTES, count), 0, data, start, count);
            });

            PitchSensorLog = new float[LOG_BUFFER_SIZE];
            PitchActionLog = new float[LOG_BUFFER_SIZE];

            int startIndex = BitConverter.ToUInt16(data, 0);
            for (int i = 0; i < LOG_BUFFER_SIZE; i++) {
                int sourceIndex = startIndex + i;
                sourceIndex = (sourceIndex + 1 - ((sourceIndex < LOG_BUFFER_SIZE) ? 0 : LOG_BUFFER_SIZE)) * 2; // circle through if necessary, skip first word, convert to byte index
                PitchSensorLog[i] = BitConverter.ToInt16(data, sourceIndex);
                PitchActionLog[i] = BitConverter.ToInt16(data, sourceIndex + 2 * LOG_BUFFER_SIZE);
            }
        }

        public void ControlEx()
        {
            byte[] data = new byte[CONTROL_STRUCT_SIZE];
            Array.Copy(BitConverter.GetBytes((UInt16)(Throttle * (float)FULL_THROTTLE)), 0, data, 0, 2);
            AngleToData(data, 2, ControlAttitude.Yaw);
            AngleToData(data, 4, ControlAttitude.Pitch);
            AngleToData(data, 6, ControlAttitude.Roll);
            DateTime start = DateTime.Now;
            port.Write(I2C_SLAVE_ADDRESS, CONTROL_STRUCT_OFFSET, I2C_SLAVE_ADDRESS_BYTES, data);
            LogSystem.Log("write took " + DateTime.Now.Subtract(start).TotalMilliseconds + " ms");
        }

        public void ConfigureEx()
        {
            byte[] pidData = new byte[20];
            Array.Copy(BitConverter.GetBytes(P), 0, pidData, 0, 4);
            Array.Copy(BitConverter.GetBytes(I), 0, pidData, 4, 4);
            Array.Copy(BitConverter.GetBytes(D / LOOP_PERIOD), 0, pidData, 8, 4);
            Array.Copy(BitConverter.GetBytes(ILimit), 0, pidData, 12, 4);
            Array.Copy(BitConverter.GetBytes(-ILimit), 0, pidData, 16, 4);
            port.Write(I2C_SLAVE_ADDRESS, CONFIG_STRUCT_OFFSET + 3 * KALMAN_SIZE + 1 * PID_SIZE, I2C_SLAVE_ADDRESS_BYTES, pidData); // config pitch controller
            port.Write(I2C_SLAVE_ADDRESS, CONFIG_STRUCT_OFFSET + 3 * KALMAN_SIZE + 2 * PID_SIZE, I2C_SLAVE_ADDRESS_BYTES, pidData); // config roll controller

            byte[] kalmanData = new byte[2] { (byte)((int)A & 0xFF), (byte)((int)(T / LOOP_PERIOD) & 0xFF) };
            port.Write(I2C_SLAVE_ADDRESS, CONFIG_STRUCT_OFFSET + 1 * KALMAN_SIZE, I2C_SLAVE_ADDRESS_BYTES, kalmanData); // config pitch filter
            port.Write(I2C_SLAVE_ADDRESS, CONFIG_STRUCT_OFFSET + 2 * KALMAN_SIZE, I2C_SLAVE_ADDRESS_BYTES, kalmanData); // config roll filter
        }

        #endregion
    }
}