using Onet.Core;
using Onet.Data;
using UnityEditor;
using UnityEngine;

namespace Onet.EditorTools
{
    /// <summary>Compact inspector: thumbnail of the shape + metrics + "Open in Level Editor".</summary>
    [CustomEditor(typeof(LevelData))]
    public class LevelDataInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            var level = (LevelData)target;
            if (GUILayout.Button("Open in Level Editor", GUILayout.Height(26))) LevelEditorWindow.Open(level);

            var m = level.metrics;
            EditorGUILayout.HelpBox(m.verified
                ? $"Verified ✔  Difficulty {m.difficultyScore} ({DifficultyModel.Label(m.difficultyScore)})  •  {level.SlotCount / 2} pairs  •  {level.gravity}"
                : "Not validated – open the Level Editor and press Validate.",
                m.verified ? MessageType.Info : MessageType.Warning);

            DrawThumb(level);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        static void DrawThumb(LevelData level)
        {
            if (level.layout == null || level.layout.Length != level.width * level.height) return;
            const float c = 10f;
            Rect r = GUILayoutUtility.GetRect(level.width * c, level.height * c, GUILayout.ExpandWidth(false));
            for (int y = 0; y < level.height; y++)
                for (int x = 0; x < level.width; x++)
                {
                    var lc = level.layout[y * level.width + x];
                    Color col = lc == LayoutCell.Slot ? new Color(0.9f, 0.88f, 0.8f)
                              : lc == LayoutCell.Blocked ? new Color(0.35f, 0.33f, 0.3f) : new Color(0, 0, 0, 0.1f);
                    EditorGUI.DrawRect(new Rect(r.x + x * c, r.y + y * c, c - 1, c - 1), col);
                }
        }
    }
}
