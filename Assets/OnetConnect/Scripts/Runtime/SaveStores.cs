using Onet.Runtime.Platform;
using UnityEngine;

namespace Onet.Runtime
{
    /// <summary>Where the JSON save lives. <see cref="SaveService"/> reads once when <see cref="Ready"/>, then only writes.</summary>
    public interface ISaveStore
    {
        bool Ready { get; }
        string Read();
        void Write(string json);
    }

    /// <summary>Mobile / editor storage.</summary>
    public class PlayerPrefsSaveStore : ISaveStore
    {
        const string Key = "onet_save_v1";

        public bool Ready => true;
        public string Read() => PlayerPrefs.GetString(Key, "");

        public void Write(string json)
        {
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
        }

        public static void Delete() => PlayerPrefs.DeleteKey(Key);
    }

    /// <summary>
    /// YouTube Playables cloud save, the only storage Playables allows. Loads once on construction; writes are refused
    /// until that load has finished, and are coalesced so at most one saveData is in flight (the latest state wins).
    /// If the load fails the store stays read-only for the session, so a bad connection never overwrites real progress.
    /// </summary>
    public class PlayablesSaveStore : ISaveStore
    {
        string _loaded = "";
        bool _writable, _inFlight, _abandoned;
        string _queued;

        public bool Ready { get; private set; }

        public PlayablesSaveStore()
        {
            Playables.LoadData((ok, text) =>
            {
                if (_abandoned) return;
                if (ok) { _loaded = text ?? ""; _writable = true; }
                else Debug.LogWarning("[Playables] loadData failed, progress will not be saved this session: " + text);
                Ready = true;
            });
        }

        /// <summary>loadData never answered: start with a fresh, unsaved game rather than hang on the loading screen.</summary>
        public void Abandon()
        {
            if (Ready) return;
            _abandoned = true;
            Ready = true;
            Debug.LogWarning("[Playables] loadData timed out, progress will not be saved this session");
        }

        public string Read() => _loaded;

        public void Write(string json)
        {
            if (!_writable) return;
            if (_inFlight) { _queued = json; return; }
            _inFlight = true;
            Playables.SaveData(json, ok =>
            {
                if (!ok) Debug.LogWarning("[Playables] saveData failed");
                _inFlight = false;
                if (_queued == null) return;
                string next = _queued;
                _queued = null;
                Write(next);
            });
        }
    }
}
