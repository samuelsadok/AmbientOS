using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppInstall.Networking
{
    public enum Protocol
    {
        TCP,
        UDP,
        Any
    }

    public interface IFirewall
    {
        void OpenPort(int port, Protocol protocol, string name);
    }
}
