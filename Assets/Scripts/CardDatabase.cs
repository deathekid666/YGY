using System.Collections.Generic;

namespace YugiohAR
{
    /// <summary>
    /// How a card's recognition should be handled by CardEffectRouter.
    /// Add new categories here as you add more complex cards later.
    /// </summary>
    public enum EffectCategory
    {
        NormalSummon,   // e.g. Dark Magician, Blue-Eyes, Summoned Skull
        FlipEffect,     // e.g. Man-Eater Bug — triggers on face-down -> face-up
        SpellTarget,    // e.g. Monster Reborn — needs the player to pick a target
        SpellSimple,    // e.g. Pot of Greed — resolves immediately, no targeting
        TrapSetTrigger  // e.g. Mirror Force, Trap Hole — set face-down, fires later
    }

    /// <summary>Attack or Defense position — determines how a monster battles.</summary>
    public enum BattlePosition
    {
        Attack,
        Defense
    }

    public class CardData
    {
        public string id;              // matches the Vuforia target name, e.g. "dark_magician"
        public string displayName;     // "Dark Magician"
        public EffectCategory effect;  // which behavior bucket this card belongs to
        public int atk;                // 0 for non-monsters
        public int def;                // 0 for non-monsters
        public int level;              // 0 for non-monsters; drives tribute cost

        public CardData(string id, string displayName, EffectCategory effect, int atk = 0, int def = 0, int level = 0)
        {
            this.id = id;
            this.displayName = displayName;
            this.effect = effect;
            this.atk = atk;
            this.def = def;
            this.level = level;
        }
    }

    /// <summary>
    /// Central lookup table for the 10 starter cards. Everything else in the
    /// project references a card by id and asks this table what it means —
    /// no card names hardcoded in game logic scripts.
    /// </summary>
    public static class CardDatabase
    {
        public static readonly Dictionary<string, CardData> Cards = new Dictionary<string, CardData>
        {
            { "dark_magician",      new CardData("dark_magician",      "Dark Magician",           EffectCategory.NormalSummon, 2500, 2100, 7) },
            { "blue_eyes",          new CardData("blue_eyes",          "Blue-Eyes White Dragon",  EffectCategory.NormalSummon, 3000, 2500, 8) },
            { "summoned_skull",     new CardData("summoned_skull",     "Summoned Skull",          EffectCategory.NormalSummon, 2500, 1200, 6) },
            { "kuriboh",            new CardData("kuriboh",            "Kuriboh",                 EffectCategory.NormalSummon, 300, 200, 1) },
            { "man_eater_bug",      new CardData("man_eater_bug",      "Man-Eater Bug",           EffectCategory.FlipEffect, 450, 600, 2) },
            { "monster_reborn",     new CardData("monster_reborn",     "Monster Reborn",          EffectCategory.SpellTarget) },
            { "pot_of_greed",       new CardData("pot_of_greed",       "Pot of Greed",            EffectCategory.SpellSimple) },
            { "mirror_force",       new CardData("mirror_force",       "Mirror Force",            EffectCategory.TrapSetTrigger) },
            { "trap_hole",          new CardData("trap_hole",          "Trap Hole",               EffectCategory.TrapSetTrigger) },
            { "exodia",             new CardData("exodia",             "Exodia the Forbidden One",EffectCategory.NormalSummon, 1000, 1000, 3) },
        };

        public static CardData Get(string id)
        {
            return Cards.TryGetValue(id, out var data) ? data : null;
        }

        /// <summary>
        /// Standard Yu-Gi-Oh tribute rule: level 5-6 needs 1 tribute, level
        /// 7+ needs 2. Level 4 and below summon freely.
        /// </summary>
        public static int TributesRequired(int level)
        {
            if (level >= 7) return 2;
            if (level >= 5) return 1;
            return 0;
        }
    }
}
