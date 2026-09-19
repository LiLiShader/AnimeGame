using System;
using UnityEngine;

namespace AstralUI
{
    [CreateAssetMenu(menuName = "Astral UI/Catalog")]
    public class AstralCatalog : ScriptableObject
    {
        public Font font;
        public Sprite[] art;
        public CharacterData[] characters;
        public EquipmentData[] equipment;
        public ChapterData[] chapters;
        public Sprite Sprite(string id) { return Array.Find(art, s => s != null && s.name == id); }
    }
    [Serializable] public class CharacterData
    {
        public string name, english, id, element, quote, skill, talent;
        public int hp, attack, defense;
        public Color accent;
    }
    [Serializable] public class EquipmentData
    {
        public string name, id, type, skill, description;
        public int attack, rarity;
        public float crit, critDamage;
    }
    [Serializable] public class ChapterData
    {
        public string name, english, background, description;
    }
    [Serializable] public class AstralSave
    {
        public int character, equipment, chapter, stage = 3, difficulty;
        public int crystals = 5780, energy = 240, coins = 125600;
        public int[] levels = new int[12];
        public int[] equipped = { 0, 2, 10, 3, 4, 6, 5, 1 };
        public bool[] locked = new bool[12];
        public bool[] owned = new bool[12];
        public int[] progress = new int[15];
        public int[] affinity = { 10, 6, 8, 5, 1, 1, 1, 1 };
        public int totalRuns, totalKills, victories;
        public float bestSurvival;
        public bool mailClaimed, questClaimed, muted, reducedMotion;
        public AstralSave()
        {
            for (int i = 0; i < 12; i++) { levels[i] = 60 + (i % 3) * 5; owned[i] = true; }
            levels[0] = 80; locked[0] = locked[1] = locked[2] = true;
            progress[0] = 3;
        }
    }
}
