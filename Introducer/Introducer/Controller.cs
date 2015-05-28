using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace Introducer
{
    class Controller
    {
        public int clientId;
        public Host host;
        public IPAddress ipAddress;
        public int originatingPort;

        public override string ToString()
        {
            return string.Format("{0}:{1}", ipAddress.ToString(), originatingPort);
        }
    }
}
