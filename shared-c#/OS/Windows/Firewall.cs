
// this file requires a reference to C:\Windows\system32\FirewallAPI.dll

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetFwTypeLib;
using AppInstall.Framework;
using AppInstall.Networking;

namespace AppInstall.OS
{
    public static class Firewall
    {
        private static object lockRef = new object();
        private static INetFwMgr mgr = null;
        private static INetFwMgr GetMgr()
        {
            if (mgr == null)
                mgr = (INetFwMgr)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("{304CE942-6E39-40D8-943A-B913C40C9CD4}")));
            return mgr;
        }


        static NET_FW_IP_PROTOCOL_ ToInternalProtocol(Protocol protocol)
        {
            switch (protocol) {
                case Protocol.TCP: return NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
                case Protocol.UDP: return NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_UDP;
                default: return NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_ANY;
            }
        }

        /// <summary>
        /// Returns the rule for the specified port. Returns null if such a rule does not exist.
        /// </summary>
        static INetFwOpenPort GetRule(int port, Protocol protocol)
        {
            try {
                return GetMgr().LocalPolicy.CurrentProfile.GloballyOpenPorts.Item(port, ToInternalProtocol(protocol));
            } catch (System.IO.FileNotFoundException) {
                return null;
            }
        }

        /// <summary>
        /// Opens the specified port. The name should indicated the usage. Returns false if the port was already open.
        /// This routine is thread-safe.
        /// </summary>
        public static bool OpenPort(int port, Protocol protocol, string name, LogContext logContext)
        {
            lock (lockRef) {
                var openPort = GetRule(port, protocol);

                if (openPort == null) {
                    openPort = (INetFwOpenPort)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("{0CA545C6-37AD-4A6C-BF92-9F7610067EF5}")));
                    openPort.Protocol = ToInternalProtocol(protocol);
                    openPort.Port = port;
                    openPort.Name = Application.ApplicationName + " " + name;
                    openPort.Enabled = false;
                    GetMgr().LocalPolicy.CurrentProfile.GloballyOpenPorts.Add(openPort);
                }

                bool wasEnabled = openPort.Enabled;
                openPort.Enabled = true;
                return !wasEnabled;
            }
        }
    }
}
