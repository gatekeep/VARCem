/**
 * UDP-socket Network Server
 * INTERNAL/PROPRIETARY/CONFIDENTIAL. Use is subject to license terms.
 * DO NOT ALTER OR REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
 * 
 * Author:	Bryan Biedenkapp, <gatekeep@gmail.com>
 * Copyright 2018 Bryan Biedenkapp
 */
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

using UDPServer.zlib;

using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using SharpPcap.WinPcap;

namespace UDPServer.Service
{
    /// <summary>
    /// This implements the base communications socket listener and session manager.
    /// </summary>
    public class CommManager
    {
        /**
         * Fields
         */
        public const int port = 10123;
        public const int readTimeoutMilliseconds = 250;

        private bool runThread = false;
        private Thread commMgrThread;

        private Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        private EndPoint epFrom = new IPEndPoint(IPAddress.Any, 0);
        private AsyncCallback recv = null;

        private static CommManager instance;
        private Dictionary<PhysicalAddress, IPEndPoint> endPoints = new Dictionary<PhysicalAddress, IPEndPoint>();

        private ICaptureDevice capDevice;

        /**
         * Events
         */
        public delegate void EthCapture(EthernetPacket packet);

        /**
         * Methods
         */
        /// <summary>
        /// Initializes a new instance of the <see cref="CommManager"/> class.
        /// </summary>
        public CommManager()
        {
            instance = this;

            commMgrThread = new Thread(new ThreadStart(CommManagerThread));
            commMgrThread.Name = "CommManagerThread";

            Trace.WriteLine("created comm manager thread [" + commMgrThread.Name + "]");
        }

        /// <summary>
        /// Starts the target manager thread.
        /// </summary>
        public void Start()
        {
            Trace.WriteLine("starting communications manager");

            if (!Program.privateNetwork)
            {
                // get the packet capture device
                CaptureDeviceList devices = CaptureDeviceList.Instance;

                capDevice = devices[Program.interfaceToUse];
                Trace.WriteLine("using [" + capDevice.Description + "] for network capture");

                capDevice.OnPacketArrival += capDevice_OnPacketArrival;

                if (capDevice is WinPcapDevice)
                {
                    WinPcapDevice winPcap = capDevice as WinPcapDevice;
                    winPcap.Open(OpenFlags.Promiscuous | OpenFlags.NoCaptureLocal | OpenFlags.MaxResponsiveness, readTimeoutMilliseconds);
                }
                else if (capDevice is LibPcapLiveDevice)
                {
                    LibPcapLiveDevice livePcapDevice = capDevice as LibPcapLiveDevice;
                    livePcapDevice.Open(DeviceMode.Promiscuous, readTimeoutMilliseconds);
                }
                else
                    throw new InvalidOperationException("unknown device type of " + capDevice.GetType().ToString());

                capDevice.StartCapture();
            }

            // start loop
            runThread = true;
            commMgrThread.Start();
        }

        /// <summary>
        /// Event that occurs when we capture a packet.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void capDevice_OnPacketArrival(object sender, CaptureEventArgs e)
        {
            try
            {
                Packet packet = Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
                if (packet is EthernetPacket)
                {
                    EthernetPacket eth = (EthernetPacket)packet;

                    foreach (KeyValuePair<PhysicalAddress, IPEndPoint> kvp in endPoints)
                    {
                        // is this our mac?
                        if (kvp.Key.ToString() == eth.DestinationHwAddress.ToString())
                            continue;

                        if (Program.foreground)
                        {
                            Console.WriteLine("session [" + kvp.Key.ToString() + "] destHW [" + eth.DestinationHwAddress.ToString() + "]");
                            Console.WriteLine("packet dump " + eth.ToString());
                        }

                        SendPacket(kvp.Key, kvp.Value, eth.Bytes);
                    }
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                /* stub */
            }
            catch (InvalidOperationException)
            {
                /* stub */
            }
        }

        /// <summary>
        /// Stops the comm manager thread.
        /// </summary>
        public void Stop()
        {
            try
            {
                Trace.WriteLine("stopping communications manager");

                // kill loop
                runThread = false;
                Thread.Sleep(50);

                if (capDevice != null)
                {
                    if (capDevice.Started)
                        capDevice.StopCapture();
                    capDevice.Close();
                    capDevice = null;
                }

                // abort and join
                commMgrThread.Abort();
                commMgrThread.Join();
            }
            catch (Exception e)
            {
                Util.StackTrace(e, false);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private byte PacketCRC(byte[] buffer, int length)
        {
            byte tmpCRC = 0;
            for (int i = 0; i < length; i++)
                tmpCRC ^= buffer[i];
            return tmpCRC;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="endPoint"></param>
        /// <param name="buf"></param>
        private void SendPacket(PhysicalAddress addr, IPEndPoint endPoint, byte[] buf)
        {
            HandshakeHeader header = new HandshakeHeader();
            header.CheckSum = PacketCRC(buf, buf.Length);
            header.DataLength = (ushort)buf.Length;
            header.MacAddr = addr.GetAddressBytes();

            // compress data
            byte[] compressedData = ZStream.CompressBuffer(buf);
            header.CompressedLength = (ushort)compressedData.Length;
            header.Length = (ushort)(header.Size + header.CompressedLength);

            // build final payload
            byte[] buffer = new byte[Util.RoundUp(header.Size + header.CompressedLength, 4)];
            header.WriteTo(buffer, 0);

            Array.Copy(compressedData, 0, buffer, header.Size, compressedData.Length);

            if (Program.foreground && Program.debug)
            {
                if (buf != null)
                {
                    Console.WriteLine("[SNIP .. Packet Tx to Client]");
                    Util.TraceHex("hdr", buffer, header.Size);
                    if (buf != null)
                    {
                        Util.TraceHex("packet", buf, buf.Length);
                        Console.WriteLine("packet length " + buf.Length);
                    }
                    Console.WriteLine("hdr->checksum = " + header.CheckSum.ToString("X"));
                    Console.WriteLine("hdr->length = " + header.Length.ToString());
                    Console.WriteLine("hdr->macAddr = " +
                        header.MacAddr[0].ToString("X") + ":" +
                        header.MacAddr[1].ToString("X") + ":" +
                        header.MacAddr[2].ToString("X") + ":" +
                        header.MacAddr[3].ToString("X") + ":" +
                        header.MacAddr[4].ToString("X") + ":" +
                        header.MacAddr[5].ToString("X"));
                    Console.WriteLine("hdr->dataLength = " + header.DataLength);
                    Console.WriteLine("hdr->compressLength = " + header.CompressedLength);
                    Console.WriteLine("[SNIP .. Packet Tx to Client]");
                }
            }

            socket.SendTo(buffer, endPoint);
        }

        /// <summary>
        /// Implements the comm manager thread.
        /// </summary>
        private void CommManagerThread()
        {
            byte[] rxBuf = new byte[65535];
            byte[] buf = null;

            EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
            socket.Bind(new IPEndPoint(IPAddress.Any, port));

            try
            {
                // implement main work loop
                while (runThread)
                {
                    try
                    {
                        int len = socket.ReceiveFrom(rxBuf, ref endPoint);
                        if (len > 0)
                        {
                            buf = new byte[len];
                            Buffer.BlockCopy(rxBuf, 0, buf, 0, buf.Length);
                        }
                        else
                        {
                            buf = null;
                        }

                        if (buf != null)
                        {
                            Util.TraceHex("buf", buf, buf.Length);

                            // read PDU data from stream
                            ProtocolDataUnit pdu = ProtocolDataUnit.ReadFrom(buf);
                            if (pdu != null && Program.foreground && Program.debug)
                            {
                                Console.WriteLine("[SNIP .. Packet Rx from Client]");
                                Util.TraceHex("hdr", pdu.HeaderData, pdu.HeaderData.Length);
                                if (pdu.ContentData != null)
                                {
                                    Util.TraceHex("packet", pdu.ContentData, pdu.ContentData.Length);
                                    Console.WriteLine("packet length " + pdu.ContentData.Length);
                                }
                                Console.WriteLine("hdr->checksum = " + pdu.Header.CheckSum.ToString("X"));
                                Console.WriteLine("hdr->length = " + pdu.Header.Length.ToString());
                                Console.WriteLine("hdr->macAddr = " +
                                    pdu.Header.MacAddr[0].ToString("X") + ":" +
                                    pdu.Header.MacAddr[1].ToString("X") + ":" +
                                    pdu.Header.MacAddr[2].ToString("X") + ":" +
                                    pdu.Header.MacAddr[3].ToString("X") + ":" +
                                    pdu.Header.MacAddr[4].ToString("X") + ":" +
                                    pdu.Header.MacAddr[5].ToString("X"));
                                Console.WriteLine("hdr->dataLength = " + pdu.Header.DataLength);
                                Console.WriteLine("hdr->compressLength = " + pdu.Header.CompressedLength);
                                Console.WriteLine("[SNIP .. Packet Rx from Client]");
                            }

                            // did we receive a pdu?
                            if (pdu != null)
                            {
                                // are we trying to shut down the session?
                                if ((pdu.Header.CheckSum == 0xFA) && (pdu.Header.DataLength == 0))
                                {
                                    if (Program.foreground)
                                    {

                                        Console.WriteLine("disconnecting session macAddr = " +
                                            pdu.Header.MacAddr[0].ToString("X") + ":" +
                                            pdu.Header.MacAddr[1].ToString("X") + ":" +
                                            pdu.Header.MacAddr[2].ToString("X") + ":" +
                                            pdu.Header.MacAddr[3].ToString("X") + ":" +
                                            pdu.Header.MacAddr[4].ToString("X") + ":" +
                                            pdu.Header.MacAddr[5].ToString("X"));
                                    }

                                    PhysicalAddress addr = new PhysicalAddress(pdu.Header.MacAddr);
                                    if (this.endPoints.ContainsKey(addr))
                                        this.endPoints.Remove(addr);
                                    return;
                                }

                                // are we trying to register a client?
                                if ((pdu.Header.CheckSum == 0xFF) && (pdu.Header.DataLength == 0))
                                {
                                    HandshakeHeader header = new HandshakeHeader();
                                    header.CheckSum = 0xFF;
                                    header.DataLength = 0;
                                    header.CompressedLength = 0;
                                    header.Length = (ushort)header.Size;
                                    header.MacAddr = pdu.Header.MacAddr;

                                    if (Program.foreground)
                                    {
                                        Console.WriteLine("new session macAddr = " +
                                            header.MacAddr[0].ToString("X") + ":" +
                                            header.MacAddr[1].ToString("X") + ":" +
                                            header.MacAddr[2].ToString("X") + ":" +
                                            header.MacAddr[3].ToString("X") + ":" +
                                            header.MacAddr[4].ToString("X") + ":" +
                                            header.MacAddr[5].ToString("X"));
                                    }

                                    // build final payload
                                    byte[] buffer = new byte[Util.RoundUp(header.Size, 4)];
                                    header.WriteTo(buffer, 0);

                                    PhysicalAddress addr = new PhysicalAddress(header.MacAddr);

                                    if (!this.endPoints.ContainsKey(addr))
                                        this.endPoints.Add(addr, (IPEndPoint)endPoint);
                                    else
                                        Console.WriteLine("macAddr already exists?");
                                    socket.SendTo(buffer, endPoint);
                                }
                                else
                                {
                                    if (!Program.privateNetwork)
                                    {
                                        // otherwise handle actual packet data
                                        Packet packet = Packet.ParsePacket(LinkLayers.Ethernet, pdu.ContentData);
                                        capDevice.SendPacket(packet);
                                    }
                                    else
                                    {
                                        foreach (KeyValuePair<PhysicalAddress, IPEndPoint> kvp in endPoints)
                                        {
                                            // is this our mac?
                                            if (kvp.Key.GetAddressBytes() == pdu.Header.MacAddr)
                                                continue;

                                            SendPacket(kvp.Key, kvp.Value, pdu.ContentData);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // do nothing
                    }
                } // while (runThread)
            }
            catch (ThreadAbortException)
            {
                Console.WriteLine("comm manager thread commanded to abort!");
                runThread = false;
            }
            catch (Exception e)
            {
                Util.StackTrace(e, false);
                runThread = false; // terminate thread
            }
        }
    } // public class CommManager
} // namespace UDPServer.Service
