using System;
using System.Collections.Generic;
using UnityEngine;

namespace Onet.Data
{
    public enum BoosterType
    {
        Hint = 0,      // highlights one legal pair
        Shuffle = 1,   // re-deals remaining tiles (guarantees a move)
        AutoMatch = 2, // "bomb": removes one legal pair for the player
        AddTime = 3,   // +N seconds
    }

    [Serializable]
    public struct BoosterDef
    {
        public BoosterType type;
        public int startingCount;
        public int coinPrice;
        public int unlockLevel;
        public Sprite icon;
    }

    /// <summary>Global tuning shared by all levels (single asset in Resources/).</summary>
    [CreateAssetMenu(menuName = "Onet/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Scoring")]
        public int basePairScore = 10;
        public float comboWindow = 3f;
        public int maxCombo = 5;

        [Header("Feel")]
        public float pathDisplayTime = 0.35f;
        public float matchPopTime = 0.2f;
        public float gravitySlideTime = 0.18f;
        public float hintHighlightTime = 3f;
        public bool autoShuffleOnDeadlock = true;

        [Header("Boosters")]
        public float addTimeSeconds = 10f;
        public List<BoosterDef> boosters = new List<BoosterDef>
        {
            new BoosterDef { type = BoosterType.Hint,      startingCount = 12, coinPrice = 50,  unlockLevel = 3 },
            new BoosterDef { type = BoosterType.Shuffle,   startingCount = 2,  coinPrice = 80,  unlockLevel = 6 },
            new BoosterDef { type = BoosterType.AutoMatch, startingCount = 2,  coinPrice = 100, unlockLevel = 12 },
            new BoosterDef { type = BoosterType.AddTime,   startingCount = 12, coinPrice = 60,  unlockLevel = 8 },
        };

        [Header("Continue on time-out")]
        public float continueSeconds = 30f;
        public int continueCoinPrice = 150;

        [Header("YouTube Playables ads (ignored outside Playables)")]
        [Tooltip("Offer 'Watch Ad' on time-out for a free continue (rewarded ad). Ad types can also be switched off in the Playables Developer Portal.")]
        public bool rewardedAdContinue = true;
        [Tooltip("Offer YouTube an ad break between levels and after Retry from the time-out screen.")]
        public bool interstitialAds = true;
        [Tooltip("No ad breaks before this level, so the tutorial levels play uninterrupted.")]
        public int interstitialFromLevel = 4;
        [Tooltip("Minimum real seconds between ad breaks.")]
        public float interstitialMinGap = 180f;

        public BoosterDef Get(BoosterType t)
        {
            foreach (var b in boosters) if (b.type == t) return b;
            return default;
        }
    }
}
