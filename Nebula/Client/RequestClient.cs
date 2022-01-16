﻿using NebulaAPI;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Nebula.Packets;

namespace PersonalLogistics.Nebula.Client
{
    public static class RequestClient
    {
        public static void RequestStateFromHost()
        {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new ClientStateRequest(PlogPlayerId.ComputeLocalPlayerId()));
        }

        public static void SendDesiredItemUpdate(int itemID, int requestMin, int recycleMax)
        {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new DesiredItemUpdate(PlogPlayerRegistry.LocalPlayer().playerId,
                itemID, requestMin, recycleMax));
        }

        public static void NotifyBufferUpsert(int itemId, int itemCount, long gameTick)
        {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new BufferedItemUpsert(PlogPlayerRegistry.LocalPlayer().playerId, itemId,
                itemCount, gameTick));
        }
    }
}