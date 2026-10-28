using System;
using UnityEngine;

namespace Onet.Data
{
    [Serializable]
    public class IconCategory
    {
        public string name = "Animals";
        public Color dot = Color.white;
        [Tooltip("Indices into TileTheme.icons that belong to this collection")]
        public int[] indices;
    }

    /// <summary>
    /// Visual skin for a chapter (Fruits, Sweets, Flowers, Chairs, Stationery…).
    /// Tile type N (1-based) uses icons[N-1]. Swapping themes never changes
    /// level logic, so the same LevelData can be re-skinned for live-ops events.
    /// </summary>
    [CreateAssetMenu(menuName = "Onet/Tile Theme", fileName = "Theme_")]
    public class TileTheme : ScriptableObject
    {
        public string themeId = "fruits";
        public string displayName = "Fruits";

        [Tooltip("One sprite per tile type. Must contain at least LevelData.tileTypes entries.")]
        public Sprite[] icons;

        [Tooltip("Optional 'tapped' artwork per tile type (same order as icons). Shown while selected or hinted.")]
        public Sprite[] happyIcons;

        [Header("Collections (lead sets that rotate with the level, see ApplyForLevel)")]
        public IconCategory[] categories;

        [Tooltip("Per-type tile colours (cycled). Multiplied onto the tile art.")]
        public Color[] tileTints;

        [Header("Tile chrome")]
        public Sprite tileBackground;
        public Sprite tileShadow;
        public Sprite selectedFrame;
        [Tooltip("Optional full tile sprites for the selected / hint states. When set, they replace the tint + frame overlay.")]
        public Sprite tileSelected;
        public Sprite tileHint;
        public Color selectedTint = new Color(1f, 0.93f, 0.55f);
        public Color hintTint = new Color(0.6f, 1f, 0.6f);

        [Header("Scene")]
        public Sprite background;
        public Color boardPanelColor = new Color(0.25f, 0.27f, 0.25f, 0.85f);

        [Header("Connection line")]
        public Color lineColor = new Color(1f, 0.85f, 0.4f);
        public Sprite pathStarSprite;

        public int IconCount => icons == null ? 0 : icons.Length;

        // Type N shows icons[_order[N-1]]. Choosing a collection moves its icons to the front, so early
        // (small) levels use that collection and larger levels spill over into the rest.
        [NonSerialized] int[] _order;

        public int CollectionCount => categories == null ? 0 : categories.Length;

        /// <summary>Levels up to this one (the tutorial) always lead with the first collection.</summary>
        public const int TutorialLevels = 3;

        /// <summary>
        /// Which collection leads on <paramref name="levelNumber"/>: the first one through the tutorial, then a
        /// different one every level (Treats, Magic, More, Animals, …) so the board keeps changing as players progress.
        /// Deterministic, so a retry looks the same.
        /// </summary>
        public int CollectionForLevel(int levelNumber)
        {
            if (CollectionCount == 0 || levelNumber <= TutorialLevels) return 0;
            return (levelNumber - TutorialLevels) % CollectionCount;
        }

        public void ApplyForLevel(int levelNumber) => ApplyCollection(CollectionForLevel(levelNumber));

        public void ApplyCollection(int collection)
        {
            int n = IconCount;
            _order = new int[n];
            int k = 0;
            var used = new bool[n];
            if (categories != null && collection >= 0 && collection < categories.Length && categories[collection].indices != null)
                foreach (int i in categories[collection].indices)
                    if (i >= 0 && i < n && !used[i]) { used[i] = true; _order[k++] = i; }
            for (int i = 0; i < n; i++) if (!used[i]) _order[k++] = i;
        }

        int Map(int tileType)
        {
            if (_order == null || _order.Length != IconCount) ApplyCollection(0);
            return _order[tileType - 1];
        }

        public Color GetTint(int tileType)
        {
            if (tileTints == null || tileTints.Length == 0) return Color.white;
            return tileTints[(tileType - 1) % tileTints.Length];
        }

        public Sprite GetHappyIcon(int tileType)
        {
            if (happyIcons == null || tileType <= 0 || tileType > IconCount || Map(tileType) >= happyIcons.Length) return null;
            return happyIcons[Map(tileType)];
        }

        public Sprite GetIcon(int tileType)
        {
            if (icons == null || tileType <= 0 || tileType > icons.Length) return null;
            return icons[Map(tileType)];
        }
    }
}
