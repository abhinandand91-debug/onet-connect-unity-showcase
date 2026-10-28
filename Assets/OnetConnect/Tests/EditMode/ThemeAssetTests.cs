using System.Linq;
using NUnit.Framework;
using Onet.Data;
using UnityEditor;
using UnityEngine;

namespace Onet.Tests
{
    /// <summary>Guards the art pipeline: themes are complete, distinct, and big enough for every level.</summary>
    public class ThemeAssetTests
    {
        const string Data = "Assets/OnetConnect/Data";

        static LevelDatabase Db() => AssetDatabase.LoadAssetAtPath<LevelDatabase>(Data + "/LevelDatabase.asset");

        [Test]
        public void EveryChapter_HasItsOwnCompleteTheme()
        {
            var db = Db();
            Assert.NotNull(db);
            Assert.GreaterOrEqual(db.chapters.Count, 3);
            var themes = db.chapters.Select(c => c.theme).ToList();
            Assert.IsTrue(themes.All(t => t != null), "chapter without a theme");
            Assert.AreEqual(themes.Distinct().Count(), themes.Count, "chapters share a theme");
            foreach (var t in themes)
            {
                Assert.GreaterOrEqual(t.IconCount, 28, t.name);
                Assert.IsTrue(t.icons.All(s => s != null), $"{t.name} has a missing icon");
                Assert.AreEqual(t.IconCount, t.icons.Distinct().Count(), $"{t.name} repeats an icon");
                Assert.GreaterOrEqual(t.happyIcons.Length, t.IconCount, $"{t.name} happy variants"); Assert.NotNull(t.tileSelected); Assert.NotNull(t.tileHint);
                Assert.NotNull(t.tileBackground); Assert.IsTrue(t.selectedFrame != null || t.tileSelected != null, $"{t.name} has no selection look"); Assert.NotNull(t.pathStarSprite); Assert.NotNull(t.background);
            }
        }

        [Test]
        public void EveryLevel_FitsItsThemeIconCount()
        {
            var db = Db();
            foreach (var level in db.levels)
            {
                var theme = db.ThemeFor(level);
                Assert.NotNull(theme, $"level {level.levelNumber} has no theme");
                Assert.LessOrEqual(level.tileTypes, theme.IconCount, $"level {level.levelNumber}");
            }
        }

        [Test]
        public void SpriteAtlases_Exist_ForEveryTheme()
        {
            foreach (var n in new[] { "Fruits", "Sweets", "Flowers", "Common", "CuteIcons", "CuteHappy", "CuteChrome" })
                Assert.NotNull(AssetDatabase.LoadMainAssetAtPath($"Assets/OnetConnect/Atlases/Atlas_{n}.spriteatlasv2"), n);
        }
    }

    public class BoosterIconTests
    {
        [Test]
        public void EveryBooster_HasAnIcon()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/OnetConnect/Data/GameConfig.asset");
            foreach (var b in config.boosters) Assert.NotNull(b.icon, b.type.ToString());
        }
    }
}
