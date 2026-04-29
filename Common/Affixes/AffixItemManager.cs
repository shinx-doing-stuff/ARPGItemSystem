using System.Collections.Generic;
using System.IO;
using System.Linq;
using ARPGItemSystem.Common.GlobalItems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Utilities;

namespace ARPGItemSystem.Common.Affixes
{
    public abstract class AffixItemManager : GlobalItem
    {
        public List<Affix> Affixes = new();
        public bool Initialized;

        public override bool InstancePerEntity => true;

        public abstract ItemCategory Category { get; }
        protected abstract int RollPrefixCount();
        protected abstract int RollSuffixCount();

        public override bool? PrefixChance(Item item, int pre, UnifiedRandom rand)
            => pre == -3;

        public override GlobalItem Clone(Item from, Item to)
        {
            var clone = (AffixItemManager)base.Clone(from, to);
            clone.Affixes = Affixes.ToList();
            clone.Initialized = Initialized;
            return clone;
        }

        public override void OnCreated(Item item, ItemCreationContext context)
        {
            Reroll(item);
            Initialized = true;
        }

        public override bool OnPickup(Item item, Player player)
        {
            if (Affixes.Count == 0) Reroll(item);
            Initialized = true;
            return true;
        }

        public override void UpdateInventory(Item item, Player player)
        {
            if (Initialized) return;
            Reroll(item);
            Initialized = true;
        }

        public void Reroll(Item item)
        {
            Affixes.Clear();
            for (int i = 0; i < RollPrefixCount(); i++) AddRoll(item, AffixKind.Prefix);
            for (int i = 0; i < RollSuffixCount(); i++) AddRoll(item, AffixKind.Suffix);
        }

        private void AddRoll(Item item, AffixKind kind)
        {
            int tier = utils.GetTier();
            var rolled = AffixRoller.Roll(Category, kind, item, Affixes, tier);
            if (rolled.HasValue) Affixes.Add(rolled.Value);
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            var useManaTip = tooltips.FirstOrDefault(tip => tip.Name == "UseMana" && tip.Mod == "Terraria");
            if (useManaTip is not null)
            {
                useManaTip.Text = Language.GetTextValue(
                    "CommonItemTooltip.UsesMana", Main.LocalPlayer.GetManaCost(item));
            }

            for (int i = 0; i < Affixes.Count; i++)
            {
                var affix = Affixes[i];
                var def = AffixRegistry.Get(affix.Id);
                var text = string.Format(def.TooltipFormat, affix.Magnitude);
                var color = def.Kind == AffixKind.Prefix
                    ? Microsoft.Xna.Framework.Color.LightGreen
                    : Microsoft.Xna.Framework.Color.DeepSkyBlue;
                tooltips.Add(new TooltipLine(Mod, $"Affix_{affix.Id}", text) { OverrideColor = color });
            }
        }

        public override void SaveData(Item item, TagCompound tag)
        {
            int n = Affixes.Count;
            var ids = new List<int>(n);
            var magnitudes = new List<int>(n);
            var tiers = new List<int>(n);
            var kinds = new List<byte>(n);

            foreach (var a in Affixes)
            {
                ids.Add((int)a.Id);
                magnitudes.Add(a.Magnitude);
                tiers.Add(a.Tier);
                kinds.Add((byte)AffixRegistry.Get(a.Id).Kind);
            }

            tag["AffixIds"] = ids;
            tag["Magnitudes"] = magnitudes;
            tag["Tiers"] = tiers;
            tag["Kinds"] = kinds;
        }

        public override void LoadData(Item item, TagCompound tag)
        {
            if (!tag.ContainsKey("AffixIds"))
            {
                Reroll(item);
                Initialized = true;
                return;
            }

            var ids = tag.GetList<int>("AffixIds").ToList();
            var magnitudes = tag.GetList<int>("Magnitudes").ToList();
            var tiers = tag.GetList<int>("Tiers").ToList();
            // Kinds written by SaveData for future-proofing; registry is authoritative for kind on load.
            _ = tag.GetList<byte>("Kinds");

            Affixes.Clear();
            for (int i = 0; i < ids.Count; i++)
                Affixes.Add(new Affix((AffixId)ids[i], magnitudes[i], tiers[i]));

            Affixes.RemoveAll(a => a.Id == AffixId.None || !AffixRegistry.All.ContainsKey(a.Id));

            if (Affixes.Count == 0 && (RollPrefixCount() > 0 || RollSuffixCount() > 0))
                Initialized = false;
            else
                Initialized = true;
        }

        public override void NetSend(Item item, BinaryWriter writer)
        {
            writer.Write(Affixes.Count);
            foreach (var a in Affixes)
            {
                writer.Write((int)a.Id);
                writer.Write(a.Magnitude);
                writer.Write(a.Tier);
                writer.Write((byte)AffixRegistry.Get(a.Id).Kind);
            }
        }

        public override void NetReceive(Item item, BinaryReader reader)
        {
            int count = reader.ReadInt32();
            Affixes.Clear();
            for (int i = 0; i < count; i++)
            {
                var id = (AffixId)reader.ReadInt32();
                int magnitude = reader.ReadInt32();
                int tier = reader.ReadInt32();
                _ = reader.ReadByte();
                Affixes.Add(new Affix(id, magnitude, tier));
            }
            Initialized = true;
        }
    }
}
