using System.IO;
using UnityEditor;
using UnityEngine;

namespace Onet.EditorTools
{
    /// <summary>
    /// Builds the game's particle effects from code (no store packs): each one is a single ParticleSystem
    /// described by a small <see cref="Recipe"/>. The prefabs are saved in Assets/OnetConnect/Fx and wired
    /// into the FxDirector by <see cref="GameSceneBuilder"/>.
    /// </summary>
    public static class ParticleFxFactory
    {
        private const string Folder = "Assets/OnetConnect/Fx";
        private const string TexturePath = Folder + "/particle.png";

        /// <summary>Everything that makes one effect different from another.</summary>
        public struct Recipe
        {
            public int Count;          // particles in the burst
            public float Speed;        // start speed (the minimum is half of this)
            public float Size;         // start size (the minimum is half of this)
            public float Life;         // seconds each particle lives
            public float Gravity;      // positive falls, negative rises
            public Color[] Colors;     // picked at random per particle
            public float Duration;     // > 0 makes it emit over time instead of one burst
            public float BoxWidth;     // > 0 emits from a wide box (for confetti showers)
        }

        private static Material material;

        /// <summary>Creates (or replaces) the prefab for one effect and returns it.</summary>
        public static GameObject Create(string name, Recipe recipe)
        {
            Directory.CreateDirectory(Folder);
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = recipe.Duration > 0f ? recipe.Duration : 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(recipe.Life * 0.6f, recipe.Life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(recipe.Speed * 0.5f, recipe.Speed);
            main.startSize = new ParticleSystem.MinMaxCurve(recipe.Size * 0.5f, recipe.Size);
            main.gravityModifier = recipe.Gravity;
            main.maxParticles = Mathf.Max(recipe.Count, 1) * 4;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startColor = RandomColors(recipe.Colors);

            var emission = ps.emission;
            if (recipe.Duration > 0f)
            {
                emission.rateOverTime = recipe.Count / recipe.Duration;
            }
            else
            {
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)recipe.Count) });
            }

            var shape = ps.shape;
            if (recipe.BoxWidth > 0f)
            {
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(recipe.BoxWidth, 0.1f, 0.1f);
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.1f;
            }

            // fade out and shrink towards the end of each particle's life
            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = fade;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SharedMaterial();

            string path = $"{Folder}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>One soft round particle material shared by every effect.</summary>
        private static Material SharedMaterial()
        {
            if (material != null) return material;
            string path = $"{Folder}/Particle.mat";
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("Sprites/Default"));
            material.mainTexture = SoftDotTexture();
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>A white soft dot, drawn once and saved next to the prefabs.</summary>
        private static Texture2D SoftDotTexture()
        {
            if (!File.Exists(TexturePath))
            {
                var canvas = new PixelCanvas(64, 64);
                canvas.Glow(32f, 32f, 32f, Color.white);
                canvas.Circle(32f, 32f, 12f, 12f, Color.white);
                Texture2D texture = canvas.ToTexture();
                File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(TexturePath);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static ParticleSystem.MinMaxGradient RandomColors(Color[] colors)
        {
            if (colors == null || colors.Length == 0) return new ParticleSystem.MinMaxGradient(Color.white);
            if (colors.Length == 1) return new ParticleSystem.MinMaxGradient(colors[0]);

            // "random colour" mode picks a point on this gradient for every particle
            var keys = new GradientColorKey[Mathf.Min(colors.Length, 8)];
            for (int i = 0; i < keys.Length; i++) keys[i] = new GradientColorKey(colors[i], i / (float)(keys.Length - 1));
            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
        }
    }
}
