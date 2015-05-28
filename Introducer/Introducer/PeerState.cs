using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Introducer
{
    class PeerState
    {
        public enum Status { Handshake, Host, Controller, Gone };
        public Status status = Status.Handshake;
        public Host host;
        public Controller controller;
    }
}
