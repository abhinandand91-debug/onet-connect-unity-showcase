using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Onet.EditorTools
{
    /// <summary>
    /// Pressing Play always starts in the Boot scene (like a real launch), whichever scene is open.
    /// Toggle with Onet ▸ Play From Boot. Skipped in batch mode so automated runs keep their own entry scene.
    /// </summary>
    [InitializeOnLoad]
    static class PlayFromBoot
    {
        const string Pref = "onet_play_from_boot";
        const string Menu = "Onet/Play From Boot";
        const string BootPath = "Assets/OnetConnect/Scenes/Boot.unity";

        static PlayFromBoot() => EditorApplication.delayCall += Apply;

        static bool Enabled { get => EditorPrefs.GetBool(Pref, true); set => EditorPrefs.SetBool(Pref, value); }

        static void Apply()
        {
            if (Application.isBatchMode) return;
            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootPath);
            EditorSceneManager.playModeStartScene = Enabled ? boot : null;
        }

        [MenuItem(Menu)]
        static void Toggle() { Enabled = !Enabled; Apply(); }

        [MenuItem(Menu, true)]
        static bool ToggleValidate() { Menu_SetChecked(); return true; }

        static void Menu_SetChecked() => UnityEditor.Menu.SetChecked(Menu, Enabled);
    }
}
