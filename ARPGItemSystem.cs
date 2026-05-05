using System;
using System.IO;
using ARPGItemSystem.Common.Network;
using Terraria.ModLoader;

namespace ARPGItemSystem
{
    public class ARPGItemSystem : Mod
    {
        // Both ARPG mods are hard mutual requirements at runtime.
        // ARPGCharacterSystem already enforces the reverse direction; we mirror it here
        // so the user sees a clear error if either mod is loaded without the other.
        // Mutual `modReferences` (load-order) and mutual <Reference> (compile-time)
        // are both impossible — they create cycles tModLoader / MSBuild reject.
        public override void PostSetupContent()
        {
            if (!ModLoader.HasMod("ARPGCharacterSystem"))
                throw new Exception("ARPG Item System requires ARPG Character System to be installed and enabled.");
        }

        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            ReforgePacketHandler.HandlePacket(reader, whoAmI);
        }
    }
}
