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
        public IPAddress ipAddress;
        public int listeningPort;

        public override string ToString()
        {
            return string.Format("{0}:{1}", ipAddress.ToString(), listeningPort);
        }
    }
}
