using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EasierSockets;


namespace Introducer
{
    class Program
    {
        static ServerSock server;
        static void Main(string[] args)
        {
            server = new ServerSock("any", 1306, "\n", Program.ClientStateChange, Program.ClientRequest);
        }

        static Dictionary<int, PeerState> peers = new Dictionary<int, PeerState>();
        static Dictionary<Guid, Host> hosts = new Dictionary<Guid, Host>();

        public static void ClientStateChange(int id, bool connected)
        {
            if (connected)
            {
                //A new connection
                lock (peers)
                    peers.Add(id, new PeerState());
            }
            else
            {
                //someone has left
                lock (peers)
                    peers[id].status = PeerState.Status.Gone;
            }
        }

        public static string ClientRequest(int id, string message)
        {
            PeerState peerState;
            lock (peers)
                peerState = peers[id];

            switch (peerState.status)
            {
                case PeerState.Status.Handshake:
                    if (message.StartsWith("host"))
                    {
                        int port = int.Parse(message.Split(' ')[1]);
                        peerState.status = PeerState.Status.Host;
                        ServerSock.ConnectionInfo hostInfo = server.GetClientConnectionInfo(id);
                        peerState.host = new Host()
                        {
                            hostId = id,
                            ipAddress = hostInfo.ClientIP,
                            listeningPort = port
                        };
                        lock (hosts)
                            hosts.Add(peerState.host.guid, peerState.host);
                        Console.WriteLine("{0} registered as a host", peerState.host);
                        return peerState.host.guid.ToString();
                    }
                    else
                    {
                        //we should have a guid - new client connecting
                        string[] messageParts = message.Split(' ');
                        Guid guid = Guid.Parse(messageParts[0]);
                        int port = int.Parse(messageParts[1]);
                        Host h;
                        lock (hosts)
                            if (!hosts.TryGetValue(guid, out h))
                                return "false";
                        //if we get here we have a valid host
                        ServerSock.ConnectionInfo connInfo = server.GetClientConnectionInfo(id);
                        peerState.status = PeerState.Status.Controller;
                        peerState.controller = new Controller()
                        {
                            clientId = id,
                            host = h,
                            ipAddress = connInfo.ClientIP,
                            originatingPort = connInfo.ClientPort
                        };
                        server.SendUpstream(h.hostId, peerState.controller.ToString());
                        Console.WriteLine("{0} connected as a client of {1}", peerState.controller, peerState.controller.host);
                        return h.ToString();
                    }
                case PeerState.Status.Gone:
                default:
                    throw new Exception("Not sure what happened here");
            }
        }
    }
}
