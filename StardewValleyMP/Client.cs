﻿using StardewValleyMP.Packets;
using StardewValleyMP.Connections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using SFarmer = StardewValley.Farmer;

namespace StardewValleyMP
{
    public class Client
    {
        public enum NetStage
        {
            WaitingForID,
            WaitingForWorldData,
            Waiting,
            Playing,
        }

        private IConnection conn;
        private Thread receiver;
        private BlockingCollection<Packet> toReceive = new BlockingCollection<Packet>(new ConcurrentQueue<Packet>());
        public byte id = 255;
        public NetStage stage = NetStage.WaitingForID;

        public Dictionary< byte, SFarmer > others = new Dictionary< byte, SFarmer >();

        public bool tempStopUpdating = false;

        public Client( IConnection theConn )
        {
            conn = theConn;
            receiver = new Thread(receiveAndQueue);
            receiver.Start();

            new VersionPacket().writeTo(conn.getStream());

            Multiplayer.sendFunc = send;
        }

        ~Client()
        {
            conn.disconnect();
            receiver.Join();
        }

        private Queue<Packet> packetDelay = new Queue<Packet>();
        public void processDelayedPackets()
        {
            while (packetDelay.Count > 0)
            {
                try
                {
                    packetDelay.Dequeue().process(this);
                }
                catch ( Exception e )
                {
                    Log.error("Exception processing delayed packet: " + e);
                }
            }
        }
        
        public void update()
        {
            if ( !conn.isConnected() )
            {
                ChatMenu.chat.Add(new ChatEntry(null, "You lost connection to the server."));
                Multiplayer.mode = Mode.Singleplayer;
                Multiplayer.client = null;
                return;
            }

            if (tempStopUpdating) return;
            if (stage != NetStage.Waiting)
            {
                processDelayedPackets();
            }

            if ( stage == NetStage.Playing )
            {
                Multiplayer.doMyPlayerUpdates(id);
            }

            try
            {
                while (toReceive.Count > 0)
                {
                    Packet packet;
                    bool success = toReceive.TryTake(out packet);
                    if (!success) continue;

                    if (stage == NetStage.Waiting && packet.id != ID.NextDay && packet.id != ID.Chat /*&& packet.id != ID.WorldData*/)
                        packetDelay.Enqueue(packet);
                    else packet.process(this);
                }
            }
            catch ( Exception e )
            {
                Log.error("Exception receiving: " + e);
            }
        }

        public void forceUpdate()
        {
            try
            {
                Packet packet = Packet.readFrom(conn.getStream());
                if (packet != null) packet.process(this);
            }
            catch (Exception e)
            {
                Log.error("Exception logging packet: " + e);
            }
        }

        public void send(Packet packet)
        {
#if false
            try
            {
                using (MemoryStream s = new MemoryStream())
                {
                    packet.writeTo(s);
                    byte[] bytes = s.GetBuffer();
                    stream.Write( bytes, 0, bytes.Length );
                }
            }
            catch ( Exception e )
            {
                Log.Async("Exception sending " + packet + " to server: " + e);
            }
#endif
#if NETWORKING_BENCHMARK
            int bytes = packet.writeTo(stream);
            Interlocked.Add(ref Multiplayer.clientToServerBytesTransferred, bytes);
            Log.Async("Sent packet " + packet + " ( " + bytes + " bytes)");
#else
            packet.writeTo(conn.getStream());
#endif
        }

        private void receiveAndQueue()
        {
            try
            {
                while (conn.isConnected())
                {
                    Packet packet = Packet.readFrom(conn.getStream());
                    toReceive.Add(packet);

#if NETWORKING_BENCHMARK
                    using (MemoryStream tmpMs = new MemoryStream())
                    {
                        int bytes = packet.writeTo(tmpMs);
                        Interlocked.Add(ref Multiplayer.serverToClientBytesTransferred, bytes);
                        Log.Async("Received packet " + packet + " ( " + bytes + " bytes)");
                    }
#endif
                }
            }
            catch (Exception e)
            {
                Log.error("Exception while receiving: " + e);
                Multiplayer.mode = Mode.Singleplayer;
                Multiplayer.client = null;
            }
        }
    }
}
