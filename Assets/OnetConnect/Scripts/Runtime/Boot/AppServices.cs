using System.Collections;
using Onet.Data;
using Onet.Runtime.Platform;
using UnityEngine;

namespace Onet.Runtime.Boot
{
    /// <summary>
    /// The single place that owns app-lifetime services: config, level database, save data and the scene loader.
    /// Created once in the Boot scene and kept alive across scene loads; scenes never construct these themselves.
    /// When a scene is played directly (editor shortcut, tests) <see cref="CreateFallback"/> supplies the same services
    /// for the lifetime of that scene only, so gameplay code has exactly one way to reach them.
    /// Inside YouTube Playables the save comes from cloud storage, which loads asynchronously: wait for
    /// <see cref="IsReady"/> before reading <see cref="Save"/>. This object also applies YouTube's pause and mute.
    /// </summary>
    public class AppServices : MonoBehaviour
    {
        public const string BootScene = "Boot";
        public const string GameScene = "Game";

        [SerializeField] GameConfig config;
        [SerializeField] LevelDatabase database;
        [SerializeField] bool persistent = true;
        [SerializeField] float cloudLoadTimeout = 8f;

        public static AppServices Instance { get; private set; }
        public static bool Exists => Instance != null;

        public GameConfig Config => config;
        public LevelDatabase Database => database;
        public SaveService Save { get; private set; }
        public SceneLoader Loader { get; private set; }

        /// <summary>Level the next Game scene should open on; 0 means "continue from the save".</summary>
        public int RequestedLevel { get; set; }

        /// <summary>True once <see cref="Save"/> holds the player's data.</summary>
        public bool IsReady { get; private set; }

        float _timeScaleBeforePause = 1f;
        bool _platformPaused;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Loader = GetComponent<SceneLoader>();
            if (Loader == null) Loader = gameObject.AddComponent<SceneLoader>();
            if (persistent) DontDestroyOnLoad(gameObject);

            Playables.Paused += OnPlatformPause;
            Playables.Resumed += OnPlatformResume;
            Playables.AudioEnabledChanged += ApplyAudio;
            ApplyAudio(Playables.AudioEnabled);

            if (Playables.InPlayables)
            {
                var cloud = new PlayablesSaveStore();
                Save = new SaveService(cloud);
                StartCoroutine(LoadWhenReady(cloud));
            }
            else
            {
                Save = new SaveService();
                Save.Load(config);
                IsReady = true;
            }
        }

        IEnumerator LoadWhenReady(PlayablesSaveStore cloud)
        {
            for (float t = 0f; !cloud.Ready && t < cloudLoadTimeout; t += Time.unscaledDeltaTime) yield return null;
            cloud.Abandon();
            Save.Load(config);
            IsReady = true;
        }

        void OnDestroy()
        {
            Playables.Paused -= OnPlatformPause;
            Playables.Resumed -= OnPlatformResume;
            Playables.AudioEnabledChanged -= ApplyAudio;
            if (_platformPaused) OnPlatformResume();
            if (Instance == this) Instance = null;
        }

        // GameController shows its own pause card; this stops everything else (tweens, FX, sound) and saves.
        void OnPlatformPause()
        {
            if (_platformPaused) return;
            _platformPaused = true;
            if (IsReady) Save.Save();
            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        void OnPlatformResume()
        {
            if (!_platformPaused) return;
            _platformPaused = false;
            Time.timeScale = _timeScaleBeforePause;
            AudioListener.pause = false;
        }

        // YouTube's mute wins over everything in game, so it drives the global listener volume.
        static void ApplyAudio(bool enabled) => AudioListener.volume = enabled ? 1f : 0f;

        /// <summary>Scene-scoped context for scenes started without Boot. Dies with the scene.</summary>
        public static AppServices CreateFallback(GameConfig config, LevelDatabase database)
        {
            var go = new GameObject("AppServices (scene fallback)");
            go.SetActive(false);                       // let us set the fields before Awake runs
            var ctx = go.AddComponent<AppServices>();
            ctx.config = config; ctx.database = database; ctx.persistent = false;
            go.SetActive(true);
            return ctx;
        }

        /// <summary>Tears the singleton down (tests, "reset app" debug command).</summary>
        public static void Shutdown()
        {
            if (Instance != null) Destroy(Instance.gameObject);
            Instance = null;
        }
    }
}
