using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace Introducer
{
    class Host
    {
        public int hostId;
        public Guid guid = Guid.NewGuid();
        public IPAddress externalIpAddress;
        public IPAddress internalIpAddress;
        public int externalPort;
        public int internalPort;

        public override string ToString()
        {
            return string.Format("{0}:{1} {2}:{3}", internalIpAddress, internalPort, externalIpAddress, externalPort);
        }
    }
}
