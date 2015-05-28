using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace EasierSockets
{
    //this delegate is called when a client connects or disconnects
    public delegate void ClientStateChange(int id, bool connected);

    //this delegate is called when the server receives a request from a client
    public delegate string ClientRequest(int id, string message);
    public class ServerSock
    {
        private string separator;
        // the delegate to call when a message is received
        // TODO: this might be a performance killer because the threads need to lock this.
        private ClientStateChange clientchange;
        private ClientRequest clientreq;

        // threads serving clients
        //TODO: use thread pooling for efficiency
        private List<ClientHandler> clients = new List<ClientHandler>();

        // listener thread - routes clients to threads in the list
        private Thread Listener;

        // The physical socket we're using
        private Socket ServerSocket;


        /// <summary>
        /// starts listening on the port specified.
        /// WARNING: Delegates must be thread safe
        /// </summary>
        /// <param name="ip">The IP address to listen for. Use "0.0.0.0" or "any" for all IPs</param>
        /// <param name="port">The port to listen on</param>
        /// <param name="separator">What signals the end of a message? A good choice is \n</param>
        /// <param name="clientstatechange">A delegate that is called when a client connects or disconnects</param>
        /// <param name="clientrequest">A delegate that is called when a client sends a request</param>
        // TODO: Allow other types of comms, like UDP, IPv6, etc
        public ServerSock(string ip, int port, string separator, ClientStateChange clientstatechange, ClientRequest clientrequest)
        {
            this.clientchange = clientstatechange;
            this.clientreq = clientrequest;

            IPAddress IP;
            if (ip == "any")
                IP = IPAddress.Any;
            else
                try
                {
                    byte[] octets = new byte[4];
                    string[] exploded = ip.Split('.');
                    for (byte i = 0; i < 4; i++)
                        octets[i] = byte.Parse(exploded[i]);
                    IP = new IPAddress(octets);
                }
                catch (Exception)
                {
                    throw new Exception("IP Address malformed");
                }

            //IP Address should be valid by now, attempt a bind
            ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint remoteEP = new IPEndPoint(IP, port);
            ServerSocket.Bind(remoteEP);

            this.separator = separator;
            Listener = new Thread(new ThreadStart(ListenAsync));
            Listener.Start();
        }


        /// <summary>
        /// Waits for clients to connect, then puts them on a new thread
        /// </summary>
        private void ListenAsync()
        {
            lock (ServerSocket)
            {
                //start listening forever
                ServerSocket.Listen(10);
                while (true)
                {
                    Socket handle = ServerSocket.Accept();
                    //there's now a new client
                    lock (clients)
                    {
                        // puts the new client in a ClientHandler box
                        clients.Add(new ClientHandler(handle, new Thread(WaitForClient)));
                    }
                }
            }
        }

        /// <summary>
        /// talks to a single client. Waits for the separator, then calls the delegate
        /// </summary>
        /// <param name="o">An object array containing the client id, and the socket</param>
        private void WaitForClient(object o)
        {
            string sep;
            ClientStateChange changeDel;
            ClientRequest reqDel;

            //cache some variables so we don't lock them for long
            lock (separator)
                lock (clientchange)
                    lock (clientreq)
                    {

                        sep = separator;
                        changeDel = clientchange;
                        reqDel = clientreq;
                    }

            //crazy casts
            int id = (int)((object[])o)[0];
            Socket handle = (Socket)((object[])o)[1];

            //new arriving data
            byte[] bytes = new byte[1024];
            //buffer
            string data = "";
            //how do we reply?
            string response = "";

            //tell the user that we're connected to a new client
            changeDel(id, true);
            // receive a stream, and let the dispatcher handle it if we get a separator
            while (handle.Connected)
            {
                //wait for data to arrive
                int bytesrec = 0;
                try { bytesrec = handle.Receive(bytes); }
                catch (SocketException) { break; }
                if (bytesrec == 0) break; //somehow this is a thing... the connection is closed if this thing returns zero

                data += Encoding.ASCII.GetString(bytes, 0, bytesrec);

                if (data.Contains(sep))
                {
                    //we have a separator in the bufffer. Isolate it and send to the dispatcher
                    string[] messages = data.Split(new string[1] { sep }, StringSplitOptions.None);
                    for (int i = 0; i < messages.Length - 1; i++)
                        if ((response = reqDel(id, messages[i])) != "")
                            try
                            {
                                handle.Send(Encoding.ASCII.GetBytes(response + sep));
                            }
                            catch (SocketException) { break; }
                    data = messages[messages.Length - 1];
                }
            }
            handle.Shutdown(SocketShutdown.Both);
            handle.Close();
            //lastly tell the user we're done with the client
            changeDel(id, false);
        }
        /// <summary>
        /// Sends a client an unsolicited message
        /// </summary>
        /// <param name="id">the client to send to</param>
        /// <param name="msg">The message to send</param>
        /// <returns>whether the message was sent properly</returns>
        public bool SendUpstream(int id, string msg)
        {
            //binary search through clients to find the right one
            int pos = BinarySearchClients(id);
            if (pos == -1) return false;
            if (!clients[pos].isAlive) return false;
            lock (clients[pos].sock)
            {
                try
                {
                    clients[pos].sock.Send(Encoding.ASCII.GetBytes(msg + separator));
                }
                catch (SocketException) { return false; }
            }
            return true;
        }

        public class ConnectionInfo
        {
            public IPAddress ServerIP;
            public IPAddress ClientIP;
            public int ServerPort;
            public int ClientPort;
        }

        public ConnectionInfo GetClientConnectionInfo(int id)
        {
            int pos = BinarySearchClients(id);
            if (pos == -1) return null;
            if (!clients[pos].isAlive) return null;

            ConnectionInfo connInfo = new ConnectionInfo();
            IPEndPoint localEndpoint, remoteEndpoint;
            lock (clients[pos].sock)
            {
                Socket clientSock = clients[pos].sock;
                if (clientSock.LocalEndPoint.AddressFamily != AddressFamily.InterNetwork || clientSock.RemoteEndPoint.AddressFamily != AddressFamily.InterNetwork)
                    return null;
                localEndpoint = (IPEndPoint)clientSock.LocalEndPoint;
                remoteEndpoint = (IPEndPoint)clientSock.RemoteEndPoint;
            }
            connInfo.ClientIP = remoteEndpoint.Address;
            connInfo.ClientPort = remoteEndpoint.Port;
            connInfo.ServerIP = localEndpoint.Address;
            connInfo.ServerPort = localEndpoint.Port;
            return connInfo;
        }

        private int BinarySearchClients(int id)
        {
            lock (clients)
            {
                int a = 0;
                int b = clients.Count - 1;
                int c;
                while (b >= a)
                {
                    c = a + (b - a) / 2;
                    if (clients[c].id == id) return id;
                    if (clients[c].id < id)
                    {
                        a = c + 1;
                        continue;
                    }
                    if (clients[c].id > id)
                    {
                        b = c;
                        continue;
                    }
                }
                return -1;
            }
        }
    }
    class ClientHandler
    {
        private static int lastid = 0;
        public Thread Receive;
        public int id;
        public Socket sock;
        /// <summary>
        /// Intitialises a ClientHandler with a unique ID, and starts the thread given.
        /// </summary>
        /// <param name="r">a thread to begin</param>
        /// <param name="s">The socket attached to the client</param>
        public ClientHandler(Socket s, Thread Receive)
        {
            this.id = lastid++;
            this.Receive = Receive;
            this.sock = s;
            Receive.Start(new object[] { id, s });
        }
        public bool isAlive
        {
            get { return Receive.IsAlive; }
        }
        ~ClientHandler()
        {
            Receive.Abort();
        }
    }
}