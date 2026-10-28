using System;
using System.Collections.Generic;
using Onet.Data;
using UnityEngine;

namespace Onet.Runtime
{
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int highestUnlocked = 1;
        public int currentLevel = 1;
        public int coins = 0;
        public List<int> stars = new List<int>();          // index = level-1
        public List<int> bestScores = new List<int>();
        public int[] boosterCounts = new int[4];           // indexed by BoosterType
        public bool boostersSeeded;
        public bool musicOn = true, sfxOn = true, hapticsOn = true;
        public List<string> tutorialSeen = new List<string>(); // tutorial steps already shown ("L1", "g_Down", ...)
    }

    /// <summary>
    /// JSON save behind an <see cref="ISaveStore"/>: PlayerPrefs on mobile / editor, YouTube cloud save in Playables.
    /// </summary>
    public class SaveService
    {
        readonly ISaveStore _store;
        public SaveData Data { get; private set; }

        public event Action Changed;

        public SaveService(ISaveStore store = null) => _store = store ?? new PlayerPrefsSaveStore();

        /// <summary>Call once the store is <see cref="ISaveStore.Ready"/>. Unreadable data (e.g. an older format) starts fresh.</summary>
        public void Load(GameConfig config)
        {
            string json = _store.Read();
            try { Data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json) ?? new SaveData(); }
            catch (Exception e) { Debug.LogWarning("Unreadable save, starting fresh: " + e.Message); Data = new SaveData(); }
            if (Data.stars == null) Data.stars = new List<int>();
            if (Data.bestScores == null) Data.bestScores = new List<int>();
            if (Data.tutorialSeen == null) Data.tutorialSeen = new List<string>();
            if (Data.boosterCounts == null || Data.boosterCounts.Length < 4) Data.boosterCounts = new int[4];
            if (!Data.boostersSeeded && config != null)
            {
                foreach (var b in config.boosters) Data.boosterCounts[(int)b.type] = b.startingCount;
                Data.boostersSeeded = true;
                Save();
            }
        }

        public void Save()
        {
            if (Data == null) return;
            _store.Write(JsonUtility.ToJson(Data));
            Changed?.Invoke();
        }

        public bool HasSeenTutorial(string step) => Data.tutorialSeen.Contains(step);

        public void MarkTutorialSeen(string step)
        {
            if (Data.tutorialSeen.Contains(step)) return;
            Data.tutorialSeen.Add(step);
            Save();
        }

        public void ResetTutorial() { Data.tutorialSeen.Clear(); Save(); }

        public int GetStars(int level) => level - 1 < Data.stars.Count ? Data.stars[level - 1] : 0;
        public int TotalStars() { int s = 0; foreach (int v in Data.stars) s += v; return s; }

        /// <summary>Records a win; returns true if this is a new best star count.</summary>
        public bool RecordWin(int level, int stars, int score, int coins)
        {
            while (Data.stars.Count < level) Data.stars.Add(0);
            while (Data.bestScores.Count < level) Data.bestScores.Add(0);
            bool improved = stars > Data.stars[level - 1];
            Data.stars[level - 1] = Mathf.Max(Data.stars[level - 1], stars);
            Data.bestScores[level - 1] = Mathf.Max(Data.bestScores[level - 1], score);
            Data.highestUnlocked = Mathf.Max(Data.highestUnlocked, level + 1);
            Data.currentLevel = level + 1;
            Data.coins += coins;
            Save();
            return improved;
        }

        public int GetBooster(BoosterType t) => Data.boosterCounts[(int)t];

        public bool TryConsumeBooster(BoosterType t)
        {
            if (Data.boosterCounts[(int)t] <= 0) return false;
            Data.boosterCounts[(int)t]--;
            Save();
            return true;
        }

        public void AddBooster(BoosterType t, int amount) { Data.boosterCounts[(int)t] += amount; Save(); }

        public bool TrySpendCoins(int amount)
        {
            if (Data.coins < amount) return false;
            Data.coins -= amount;
            Save();
            return true;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Onet/Debug/Delete Save")]
        static void DeleteSave() { PlayerPrefsSaveStore.Delete(); Debug.Log("Onet save deleted"); }
#endif
    }
}
