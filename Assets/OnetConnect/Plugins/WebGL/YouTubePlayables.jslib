// Bridge between the YouTube Playables SDK (window.ytgame, loaded by the WebGL template before any game code) and
// Onet.Runtime.Platform.Playables. Every entry point is safe to call when the SDK is missing or not in the Playables
// environment. Events and async replies go back to C# through one callback: (kind, request, ok, utf8Text).
var YtPlayablesLib = {
  $YtP: {
    cb: 0,
    gameReadySent: false,

    sdk: function () {
      return (typeof ytgame !== 'undefined' && ytgame) ? ytgame : null;
    },

    send: function (kind, request, ok, text) {
      if (!YtP.cb) return;
      var ptr = 0;
      if (text !== undefined && text !== null) {
        var size = lengthBytesUTF8(text) + 1;
        ptr = _malloc(size);
        stringToUTF8(text, ptr, size);
      }
      {{{ makeDynCall('viiii', 'YtP.cb') }}}(kind, request, ok, ptr);
      if (ptr) _free(ptr);
    },

    // Kind 10 is a reply to request `request`; ok is 1 on success.
    reply: function (request, promise, toText) {
      promise.then(function (value) {
        YtP.send(10, request, 1, toText ? toText(value) : null);
      }, function (err) {
        YtP.send(10, request, 0, String(err && err.message ? err.message : err));
      });
    },

    audioContextSet: function (running) {
      if (typeof WEBAudio === 'undefined' || !WEBAudio.audioContext) return;
      try { running ? WEBAudio.audioContext.resume() : WEBAudio.audioContext.suspend(); } catch (e) {}
    }
  },

  YtP_InPlayables: function () {
    var yt = YtP.sdk();
    return (yt && yt.IN_PLAYABLES_ENV) ? 1 : 0;
  },

  YtP_IsAudioEnabled: function () {
    var yt = YtP.sdk();
    try { return (!yt || yt.system.isAudioEnabled()) ? 1 : 0; } catch (e) { return 1; }
  },

  // Pause stops Unity's main loop after C# has saved, so nothing (game loop, rendering, input) runs until resume.
  YtP_Init__deps: ['emscripten_pause_main_loop', 'emscripten_resume_main_loop'],
  YtP_Init: function (callback) {
    YtP.cb = callback;
    // The template's capture-phase Esc listener calls this, so Esc reaches the game without Unity ever seeing
    // (and preventDefault-ing) the key event.
    window.ytpEscape = function () { YtP.send(4, 0, 1, null); };
    var yt = YtP.sdk();
    if (!yt) return;
    try {
      yt.system.onPause(function () {
        YtP.send(1, 0, 1, null);
        YtP.audioContextSet(false);
        _emscripten_pause_main_loop();
      });
      yt.system.onResume(function () {
        _emscripten_resume_main_loop();
        YtP.audioContextSet(true);
        YtP.send(2, 0, 1, null);
      });
      yt.system.onAudioEnabledChange(function (enabled) {
        YtP.send(3, 0, enabled ? 1 : 0, null);
      });
    } catch (e) {
      console.warn('[Playables] subscribe failed', e);
    }
  },

  // firstFrameReady is sent by the template as soon as its loading screen is on screen.
  YtP_GameReady: function () {
    var yt = YtP.sdk();
    if (!yt || YtP.gameReadySent) return;
    YtP.gameReadySent = true;
    try { yt.game.gameReady(); } catch (e) { console.warn('[Playables] gameReady failed', e); }
  },

  YtP_LoadData: function (request) {
    var yt = YtP.sdk();
    if (!yt) { YtP.send(10, request, 0, 'sdk missing'); return; }
    YtP.reply(request, yt.game.loadData(), function (data) { return data || ''; });
  },

  YtP_SaveData: function (request, json) {
    var yt = YtP.sdk();
    if (!yt) { YtP.send(10, request, 0, 'sdk missing'); return; }
    YtP.reply(request, yt.game.saveData(UTF8ToString(json)), null);
  },

  // Resolves when the ad break is over (or none was shown); the SDK pauses the game around it via onPause / onResume.
  YtP_RequestInterstitialAd: function (request) {
    var yt = YtP.sdk();
    if (!yt || !yt.ads) { YtP.send(10, request, 0, 'ads unavailable'); return; }
    YtP.reply(request, yt.ads.requestInterstitialAd(), null);
  },

  // Reply text is "1" when the player earned the reward, "0" when they closed the ad early.
  YtP_RequestRewardedAd: function (request, rewardId) {
    var yt = YtP.sdk();
    if (!yt || !yt.ads) { YtP.send(10, request, 0, 'ads unavailable'); return; }
    YtP.reply(request, yt.ads.requestRewardedAd(UTF8ToString(rewardId)), function (granted) { return granted ? '1' : '0'; });
  }
};

autoAddDeps(YtPlayablesLib, '$YtP');
mergeInto(LibraryManager.library, YtPlayablesLib);
