using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace Onet.Runtime.Platform
{
    /// <summary>
    /// C# side of the YouTube Playables SDK, through <c>Plugins/WebGL/YouTubePlayables.jslib</c>.
    /// Outside a WebGL player, or when the page is not running inside YouTube, <see cref="InPlayables"/> is false and
    /// every call is a no-op (async calls reply with failure), so callers keep their mobile / editor behaviour.
    /// </summary>
    public static class Playables
    {
        /// <summary>YouTube asked the game to stop. Handlers run before the main loop is halted, so save here.</summary>
        public static event Action Paused;
        public static event Action Resumed;
        public static event Action<bool> AudioEnabledChanged;
        /// <summary>Esc in the web page. The template forwards it here because Unity never receives the key there.</summary>
        public static event Action EscapePressed;

        public static bool InPlayables { get; private set; }
        public static bool AudioEnabled { get; private set; } = true;

        const int KindPause = 1, KindResume = 2, KindAudio = 3, KindEscape = 4, KindReply = 10;

        delegate void EventCallback(int kind, int request, int ok, IntPtr text);

        static readonly Dictionary<int, Action<bool, string>> Pending = new Dictionary<int, Action<bool, string>>();
        static int _nextRequest;
        static bool _gameReady;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int YtP_InPlayables();
        [DllImport("__Internal")] static extern int YtP_IsAudioEnabled();
        [DllImport("__Internal")] static extern void YtP_Init(EventCallback callback);
        [DllImport("__Internal")] static extern void YtP_GameReady();
        [DllImport("__Internal")] static extern void YtP_LoadData(int request);
        [DllImport("__Internal")] static extern void YtP_SaveData(int request, string json);
        [DllImport("__Internal")] static extern void YtP_RequestRewardedAd(int request, string rewardId);
        [DllImport("__Internal")] static extern void YtP_RequestInterstitialAd(int request);
#else
        static int YtP_InPlayables() => 0;
        static int YtP_IsAudioEnabled() => 1;
        static void YtP_Init(EventCallback callback) { }
        static void YtP_GameReady() { }
        static void YtP_LoadData(int request) => OnEvent(KindReply, request, 0, IntPtr.Zero);
        static void YtP_SaveData(int request, string json) => OnEvent(KindReply, request, 0, IntPtr.Zero);
        static void YtP_RequestRewardedAd(int request, string rewardId) => OnEvent(KindReply, request, 0, IntPtr.Zero);
        static void YtP_RequestInterstitialAd(int request) => OnEvent(KindReply, request, 0, IntPtr.Zero);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            Paused = Resumed = null;
            AudioEnabledChanged = null;
            EscapePressed = null;
            Pending.Clear();
            _gameReady = false;
            InPlayables = YtP_InPlayables() == 1;
            AudioEnabled = YtP_IsAudioEnabled() == 1;
            YtP_Init(OnEvent);
        }

        /// <summary>The game accepts input. Sent once; later calls are ignored.</summary>
        public static void GameReady()
        {
            if (_gameReady) return;
            _gameReady = true;
            YtP_GameReady();
        }

        /// <summary>Cloud save text ("" for a new player). Must finish before any <see cref="SaveData"/>.</summary>
        public static void LoadData(Action<bool, string> done) => YtP_LoadData(Track(done));

        public static void SaveData(string json, Action<bool> done) => YtP_SaveData(Track((ok, _) => done?.Invoke(ok)), json);

        /// <summary>
        /// Shows a YouTube rewarded ad. <paramref name="done"/> gets (shown, granted): shown is false when ads are
        /// unavailable (not in Playables, or monetisation not set up); granted is true only if the player earned it.
        /// </summary>
        public static void RequestRewardedAd(string rewardId, Action<bool, bool> done) =>
            YtP_RequestRewardedAd(Track((ok, text) => done?.Invoke(ok, ok && text == "1")), rewardId);

        /// <summary>
        /// Offers YouTube an ad break. <paramref name="done"/> runs once it is over, whether or not an ad was shown
        /// (false when the request failed). Never tie a reward to this.
        /// </summary>
        public static void RequestInterstitialAd(Action<bool> done) => YtP_RequestInterstitialAd(Track((ok, _) => done?.Invoke(ok)));

        static int Track(Action<bool, string> done)
        {
            int id = ++_nextRequest;
            Pending[id] = done;
            return id;
        }

        [MonoPInvokeCallback(typeof(EventCallback))]
        static void OnEvent(int kind, int request, int ok, IntPtr text)
        {
            try
            {
                switch (kind)
                {
                    case KindPause: Paused?.Invoke(); break;
                    case KindResume: Resumed?.Invoke(); break;
                    case KindAudio:
                        AudioEnabled = ok == 1;
                        AudioEnabledChanged?.Invoke(AudioEnabled);
                        break;
                    case KindEscape: EscapePressed?.Invoke(); break;
                    case KindReply:
                        if (!Pending.TryGetValue(request, out var done)) return;
                        Pending.Remove(request);
                        done?.Invoke(ok == 1, text == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(text));
                        break;
                }
            }
            catch (Exception e) { Debug.LogException(e); }   // never let an exception unwind into the JS caller
        }
    }
}
