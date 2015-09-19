using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using AppInstall.Networking;
using AppInstall.Framework;

namespace AppInstall.Hardware
{
    public class FlightControllerService : Service
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


        private float P, I, D, ILimit, A, T;

        private readonly SlowAction readDataAction;
        //private readonly SlowAction readLogAction;
        private readonly SlowAction controlAction;
        private readonly SlowAction configureAction;

        
        public int Version { get; private set; }
        public Quaternion Attitude { get; private set; }
        public YawPitchRoll AngularRate { get; private set; }
        
        public float Throttle { get; set; }
        public YawPitchRoll ControlAttitude { get; set; }

        public float[] PitchSensorLog { get; private set; }
        public float[] PitchActionLog { get; private set; }


        public FlightControllerService(ZeroConfService network, BluetoothPeripheral bluetooth, LogContext logContext)
            : base("flight-service", AppInstall.Organization.GlobalConstants.FLIGHT_SERVICE_UUID, network, bluetooth, logContext)
        {
            readDataAction = new SlowAction(c => ReadDataEx(c).Wait(c));
            //readLogAction = new SlowAction(ReadLogEx);
            controlAction = new SlowAction(c => ControlEx(c).Wait(c));
            configureAction = new SlowAction(c => ConfigureEx(c).Wait(c));

            ControlAttitude = new YawPitchRoll();
        }


        /// <summary>
        /// Blocks until data is read
        /// </summary>
        public void ReadData()
        {
            readDataAction.Trigger(ApplicationControl.ShutdownToken).WaitOne();
        }

        /*
        public void ReadLog()
        {
            readLogAction.Trigger(ApplicationControl.ShutdownToken).WaitOne();
        }*/

        public WaitHandle Control()
        {
            return controlAction.Trigger(ApplicationControl.ShutdownToken);
        }

        /// <summary>
        /// Configures the parameters of the flight controller.
        /// </summary>
        public WaitHandle Configure(float p, float i, float d, float iLimit, float a, float t)
        {
            P = p;
            I = i;
            D = d;
            ILimit = iLimit;
            A = a;
            T = t;
            return configureAction.Trigger(ApplicationControl.ShutdownToken);
        }


        #region "Implementation"


        private float AngleFromData(byte[] data, int offset)
        {
            return (float)ByteConverter.ToInt16LE(data, offset) / (float)0x4000 * (float)Math.PI;
        }

        private void AngleToData(byte[] data, int offset, float angle)
        {
            Array.Copy(ByteConverter.GetBytesLE((UInt16)(angle / (float)Math.PI * (float)0x4000)), 0, data, offset, 2);
        }

        private float QuatFromData(byte[] data, int offset)
        {
            return (float)ByteConverter.ToSingleLE(data, offset);
        }

        private void QuatToData(byte[] data, int offset, float val)
        {
            Array.Copy(ByteConverter.GetBytesLE(val), 0, data, offset, 4);
        }

        private async Task ReadDataEx(CancellationToken cancellationToken)
        {
            byte[] data1 = await base.ReadEndpoint("attitude", AppInstall.Organization.GlobalConstants.MOTION_ATTITUDE_UUID, cancellationToken);
            Attitude = new Quaternion(QuatFromData(data1, 0), QuatFromData(data1, 4), QuatFromData(data1, 8));

            byte[] data2 = await base.ReadEndpoint("angular-rate", AppInstall.Organization.GlobalConstants.MOTION_ATTITUDE_UUID, cancellationToken);
            AngularRate = new YawPitchRoll(AngleFromData(data2, 0),  AngleFromData(data2, 2),  AngleFromData(data2, 4));
        }

        /*
        private void ReadLogEx(CancellationToken cancellationToken)
        {
            byte[] data = new byte[LOG_STRUCT_SIZE];
            Utilities.PartitionWork(0, LOG_STRUCT_SIZE, 17, async (start, count) => {
                ReadEndpoint("log", new Guid(), cancellationToken);
                Array.Copy(port.Read(I2C_SLAVE_ADDRESS, LOG_STRUCT_OFFSET + start, I2C_SLAVE_ADDRESS_BYTES, count), 0, data, start, count);
            });

            PitchSensorLog = new float[LOG_BUFFER_SIZE];
            PitchActionLog = new float[LOG_BUFFER_SIZE];

            int startIndex = ByteConverter.ToUInt16LE(data, 0);
            for (int i = 0; i < LOG_BUFFER_SIZE; i++) {
                int sourceIndex = startIndex + i;
                sourceIndex = (sourceIndex + 1 - ((sourceIndex < LOG_BUFFER_SIZE) ? 0 : LOG_BUFFER_SIZE)) * 2; // circle through if necessary, skip first word, convert to byte index
                PitchSensorLog[i] = ByteConverter.ToInt16LE(data, sourceIndex);
                PitchActionLog[i] = ByteConverter.ToInt16LE(data, sourceIndex + 2 * LOG_BUFFER_SIZE);
            }
        }
         * */

        private async Task ControlEx(CancellationToken cancellationToken)
        {
            byte[] data = new byte[CONTROL_STRUCT_SIZE];
            Array.Copy(ByteConverter.GetBytesLE((UInt16)(Throttle * (float)FULL_THROTTLE)), 0, data, 0, 2);
            AngleToData(data, 2, ControlAttitude.Yaw);
            AngleToData(data, 4, ControlAttitude.Pitch);
            AngleToData(data, 6, ControlAttitude.Roll);
            DateTime start = DateTime.Now;
            await WriteEndpoint("control", AppInstall.Organization.GlobalConstants.FLIGHT_CONFIG_UUID, data, cancellationToken);
            logContext.Log("write took " + DateTime.Now.Subtract(start).TotalMilliseconds + " ms");
        }

        private async Task ConfigureEx(CancellationToken cancellationToken)
        {
            byte[] pidData = new byte[20];
            Array.Copy(ByteConverter.GetBytesLE(P), 0, pidData, 0, 4);
            Array.Copy(ByteConverter.GetBytesLE(I), 0, pidData, 4, 4);
            Array.Copy(ByteConverter.GetBytesLE(D / LOOP_PERIOD), 0, pidData, 8, 4);
            Array.Copy(ByteConverter.GetBytesLE(ILimit), 0, pidData, 12, 4);
            Array.Copy(ByteConverter.GetBytesLE(-ILimit), 0, pidData, 16, 4);

            byte[] kalmanData = new byte[2] { (byte)((int)A & 0xFF), (byte)((int)(T / LOOP_PERIOD) & 0xFF) };

            await WriteEndpoint("config", AppInstall.Organization.GlobalConstants.FLIGHT_CONTROL_UUID, pidData.Concat(kalmanData).ToArray(), cancellationToken); // todo: format data correctly
        }

        #endregion
    }
}