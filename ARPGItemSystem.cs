using System.IO;
using ARPGItemSystem.Common.Network;
using Terraria.ModLoader;

namespace ARPGItemSystem
{
    public class ARPGItemSystem : Mod
    {
        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            ReforgePacketHandler.HandlePacket(reader, whoAmI);
        }
    }
}