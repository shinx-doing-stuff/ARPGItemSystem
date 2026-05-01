using System;
using ARPGEnemySystem.Common.Elements;
using ARPGItemSystem.Common.Players;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ARPGItemSystem.Common.Systems
{
    public class ResistanceShieldUISystem : ModSystem
    {
        private readonly struct ShieldRow
        {
            public readonly Element Element;
            public readonly Color Tint;
            public ShieldRow(Element element, Color tint) { Element = element; Tint = tint; }
        }

        // Bottom (index 0) = Physical at vanilla anchor. Top = Lightning.
        private static readonly ShieldRow[] Stack =
        {
            new(Element.Physical,  Color.White),
            new(Element.Fire,      Color.OrangeRed),
            new(Element.Cold,      Color.LightSkyBlue),
            new(Element.Lightning, Color.Yellow),
        };

        private const int VerticalGap = 4;
        private const float ShieldScale = 1.2f;
        private const float ShieldTextScale = 0.8f;

        public override void Load()
        {
            if (Main.netMode == NetmodeID.Server) return;
            On_Main.DrawDefenseCounter += DrawResistanceShields;
        }

        public override void Unload()
        {
            On_Main.DrawDefenseCounter -= DrawResistanceShields;
        }

        private static void DrawResistanceShields(On_Main.orig_DrawDefenseCounter orig, int inventoryX, int inventoryY)
        {
            // Skip orig — replacing vanilla's single defense icon with our four resistance shields.
            // DefenseIconPosition is set by DrawAccSlots (which runs before this hook), so it is fresh here.
            DrawShields(Main.spriteBatch, AccessorySlotLoader.DefenseIconPosition - new Vector2(120, -20));
        }

        private static void DrawShields(SpriteBatch spriteBatch, Vector2 anchor)
        {
            var player = Main.LocalPlayer;
            if (player is null || !player.active || player.dead) return;

            var elem = player.GetModPlayer<PlayerElementalPlayer>();

            // Texture confirmed via IL decompilation of Main.DrawDefenseCounter:
            // ldsfld TextureAssets::Extra, ldc.i4.s 58 -> TextureAssets.Extra[ExtrasID.DefenseShield]
            Texture2D shield = TextureAssets.Extra[ExtrasID.DefenseShield].Value;
            if (shield is null) return;

            Vector2 origin = new Vector2(shield.Width / 2f, shield.Height / 2f);
            int rowHeight = (int)(shield.Height * ShieldScale) + VerticalGap;

            for (int i = 0; i < Stack.Length; i++)
            {
                var row = Stack[i];
                Vector2 pos = anchor - new Vector2(0, i * rowHeight);

                spriteBatch.Draw(shield, pos, null, row.Tint, 0f, origin, ShieldScale, SpriteEffects.None, 0f);

                float resistance = elem.GetResistance(row.Element);
                string text = $"{(int)Math.Round(resistance)}%";

                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text);
                Vector2 textPos = pos - textSize * ShieldTextScale / 2f;

                Utils.DrawBorderString(spriteBatch, text, textPos, Color.White, ShieldTextScale);
            }
        }
    }
}
