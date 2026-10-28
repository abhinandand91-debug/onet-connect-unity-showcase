using System;
using System.Collections.Generic;
using UnityEngine;

namespace Onet.Data
{
    [Serializable]
    public class Chapter
    {
        public string title = "Fruit Garden";
        public TileTheme theme;
        public int firstLevel = 1;
        public int lastLevel = 20;
        [Tooltip("Stars required (total) to unlock this chapter; 0 = unlocks on level completion")]
        public int starGate = 0;
        public int chapterCompleteCoins = 100;
    }

    /// <summary>
    /// Ordered list of all levels + chapter grouping. Levels beyond the
    /// authored list can loop through an "endless" pool with fresh deals.
    /// </summary>
    [CreateAssetMenu(menuName = "Onet/Level Database", fileName = "LevelDatabase")]
    public class LevelDatabase : ScriptableObject
    {
        public List<LevelData> levels = new List<LevelData>();
        public List<Chapter> chapters = new List<Chapter>();

        [Header("After the last authored level")]
        [Tooltip("Levels replayed (with fresh deals) once content runs out")]
        public List<LevelData> endlessPool = new List<LevelData>();

        public int Count => levels.Count;

        public LevelData Get(int levelNumber, out bool isEndless)
        {
            isEndless = false;
            if (levelNumber >= 1 && levelNumber <= levels.Count) return levels[levelNumber - 1];
            if (endlessPool.Count == 0) return levels.Count > 0 ? levels[levels.Count - 1] : null;
            isEndless = true;
            return endlessPool[(levelNumber - levels.Count - 1) % endlessPool.Count];
        }

        public Chapter ChapterFor(int levelNumber)
        {
            foreach (var c in chapters)
                if (levelNumber >= c.firstLevel && levelNumber <= c.lastLevel) return c;
            return chapters.Count > 0 ? chapters[chapters.Count - 1] : null;
        }

        /// <summary>Theme resolution order: level override → chapter → first chapter.</summary>
        public TileTheme ThemeFor(LevelData level)
        {
            if (level != null && level.theme != null) return level.theme;
            var ch = ChapterFor(level != null ? level.levelNumber : 1);
            return ch?.theme;
        }
    }
}
