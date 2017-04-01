﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using StardewValley;
using StardewModdingAPI;
using Microsoft.Xna.Framework;
using SFarmer = StardewValley.Farmer;

namespace StardewValleyMP.Packets
{
    // Client <-> Server
    // Inform everyone of someone having a dance partner (and that that person is now taken)
    public class FestivalDancePartnerPacket : Packet
    {
        public byte clientId;
        public string partner;

        public FestivalDancePartnerPacket()
            : base(ID.FestivalDancePartner)
        {
        }

        public FestivalDancePartnerPacket(byte theId, string partnerName)
            : this()
        {
            clientId = theId;
            partner = partnerName;
            danceMessage();
        }

        protected override void read(BinaryReader reader)
        {
            clientId = reader.ReadByte();
            partner = reader.ReadString();
        }

        protected override void write(BinaryWriter writer)
        {
            writer.Write( clientId );
            writer.Write(partner);
        }

        public override void process(Client client)
        {
            SFarmer farmer = client.others[clientId];
            if (farmer == null) return;

            doSFarmer(farmer);
        }

        public override void process(Server server, Server.Client client)
        {
            if (clientId != client.id) return;

            doSFarmer(client.farmer);
            server.broadcast(this, client.id);
        }

        private void doSFarmer( SFarmer farmer )
        {
            NPC npc = Game1.currentLocation.currentEvent.getActorByName(partner);
            if (npc == null) return;

            farmer.dancePartner = npc;
            npc.hasPartnerForDance = true;
            danceMessage();
        }

        private void danceMessage()
        {
            SFarmer farmer = Multiplayer.getFarmer(clientId);
            //ChatMenu.chat.Add(new ChatEntry(null, farmer.name + " will be dancing with " + partner + "."));
        }
    }
}
