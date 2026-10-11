using UnityEngine;

namespace Warfront.Game
{
    /// <summary>
    /// Runtime generated art library.
    ///
    /// WARFRONT ships zero binary art assets: surfaces, materials, effect colors and the UI sprite are
    /// created at boot so the project is reproducible from source and carries no licensing questions.
    /// Shader lookup is URP-first and degrades gracefully to Built-in RP shaders, so the project also
    /// runs if URP is not configured yet.
    /// </summary>
    public class GameAssets
    {
        public Material Ground;
        public Material Asphalt;
        public Material Concrete;
        public Material Metal;
        public Material Rust;
        public Material Sand;
        public Material Wood;
        public Material Cover;
        public Material PlayerBody;
        public Material PlayerAccent;
        public Material EnemyBody;
        public Material EnemyAccent;
        public Material WeaponMetal;
        public Material WeaponGrip;
        public Material MuzzleFlash;
        public Material Tracer;
        public Material Impact;
        public Material Blood;
        public Material Beacon;
        public Material BeaconLocked;
        public Material ExtractionZone;
        public Material LootContainer;
        public Material ObjectiveMarker;
        public Material ShadowBlob;

        public Sprite WhiteSprite;
        public Texture2D WhiteTexture;
        public Font UIFont;

        public bool UsingUrp { get; private set; }
        public string ResolvedLitShaderName { get; private set; }
        public string ResolvedUnlitShaderName { get; private set; }

        private Shader _litShader;
        private Shader _unlitShader;

        public void Initialize()
        {
            _litShader = FindShader(new[]
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Simple Lit",
                "Standard",
                "Legacy Shaders/Diffuse"
            });
            _unlitShader = FindShader(new[]
            {
                "Universal Render Pipeline/Unlit",
                "Unlit/Color",
                "Sprites/Default",
                "Legacy Shaders/Unlit/Color"
            });

            UsingUrp = _litShader != null && _litShader.name.StartsWith("Universal");
            ResolvedLitShaderName = _litShader != null ? _litShader.name : "none";
            ResolvedUnlitShaderName = _unlitShader != null ? _unlitShader.name : "none";

            Ground = Lit("Ground", new Color(0.20f, 0.21f, 0.19f), 0.05f, 0f);
            Asphalt = Lit("Asphalt", new Color(0.16f, 0.16f, 0.17f), 0.1f, 0f);
            Concrete = Lit("Concrete", new Color(0.42f, 0.42f, 0.40f), 0.08f, 0f);
            Metal = Lit("Metal", new Color(0.34f, 0.36f, 0.38f), 0.45f, 0.65f);
            Rust = Lit("Rust", new Color(0.36f, 0.22f, 0.14f), 0.2f, 0.15f);
            Sand = Lit("Sand", new Color(0.52f, 0.46f, 0.33f), 0.05f, 0f);
            Wood = Lit("Wood", new Color(0.32f, 0.24f, 0.16f), 0.1f, 0f);
            Cover = Lit("Cover", new Color(0.28f, 0.30f, 0.27f), 0.1f, 0.05f);
            PlayerBody = Lit("PlayerBody", new Color(0.22f, 0.28f, 0.24f), 0.25f, 0.1f);
            PlayerAccent = Lit("PlayerAccent", new Color(0.55f, 0.72f, 0.42f), 0.3f, 0.05f);
            EnemyBody = Lit("EnemyBody", new Color(0.32f, 0.30f, 0.26f), 0.15f, 0.05f);
            EnemyAccent = Lit("EnemyAccent", new Color(0.72f, 0.36f, 0.18f), 0.2f, 0.1f);
            WeaponMetal = Lit("WeaponMetal", new Color(0.18f, 0.18f, 0.19f), 0.5f, 0.7f);
            WeaponGrip = Lit("WeaponGrip", new Color(0.13f, 0.13f, 0.13f), 0.2f, 0f);
            MuzzleFlash = Unlit("MuzzleFlash", new Color(1f, 0.85f, 0.45f, 1f));
            Tracer = Unlit("Tracer", new Color(1f, 0.93f, 0.65f, 0.85f));
            Impact = Unlit("Impact", new Color(1f, 0.72f, 0.3f, 1f));
            Blood = Lit("Blood", new Color(0.35f, 0.06f, 0.06f), 0.1f, 0f);
            Beacon = Unlit("Beacon", new Color(0.35f, 0.95f, 0.55f, 1f));
            BeaconLocked = Unlit("BeaconLocked", new Color(0.95f, 0.45f, 0.2f, 1f));
            ExtractionZone = Unlit("ExtractionZone", new Color(0.35f, 0.9f, 0.55f, 1f));
            LootContainer = Lit("LootContainer", new Color(0.35f, 0.34f, 0.28f), 0.2f, 0.2f);
            ObjectiveMarker = Unlit("ObjectiveMarker", new Color(0.95f, 0.8f, 0.25f, 1f));

            ShadowBlob = Unlit("ShadowBlob", new Color(0.05f, 0.05f, 0.05f, 1f));

            WhiteTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            WhiteTexture.SetPixels(pixels);
            WhiteTexture.Apply();
            WhiteSprite = Sprite.Create(WhiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            WhiteSprite.name = "WarfrontWhiteSprite";

            UIFont = ResolveFont();
        }

        public Font ResolveFont()
        {
            try
            {
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font != null) return font;
            }
            catch (System.Exception)
            {
                // Unity 2021 and older name the built-in font differently.
            }

            try
            {
                Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (font != null) return font;
            }
            catch (System.Exception)
            {
            }

            try
            {
                Font font = Font.CreateDynamicFontFromOSFont("Arial", 24);
                if (font != null) return font;
            }
            catch (System.Exception)
            {
            }

            Debug.LogWarning("[Warfront] No built-in font found; UI text may not render. Assign GameAssets.UIFont manually.");
            return null;
        }

        public Material Lit(string name, Color color, float smoothness, float metallic)
        {
            Material material = CreateMaterial(name, _litShader, color);
            if (material == null) return null;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            return material;
        }

        public Material Unlit(string name, Color color)
        {
            return CreateMaterial(name, _unlitShader, color);
        }

        public Material Transparent(string name, Color color)
        {
            Material material = Unlit(name, color);
            if (material == null) return null;
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f); // URP transparent surface type
                material.SetFloat("_Blend", 0f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            return material;
        }

        private Material CreateMaterial(string name, Shader shader, Color color)
        {
            if (shader == null)
            {
                Debug.LogError("[Warfront] No usable shader found for '" + name + "'. Check the render pipeline setup.");
                return null;
            }
            Material material = new Material(shader);
            material.name = name;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }

        private static Shader FindShader(string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                Shader shader = Shader.Find(candidates[i]);
                if (shader != null) return shader;
            }
            return null;
        }

        /// <summary>Convenience palette so UI and 3D art share one identity.</summary>
        public static class Palette
        {
            public static readonly Color Background = new Color(0.06f, 0.07f, 0.07f, 0.92f);
            public static readonly Color Panel = new Color(0.11f, 0.12f, 0.12f, 0.95f);
            public static readonly Color PanelLight = new Color(0.16f, 0.17f, 0.17f, 0.95f);
            public static readonly Color Primary = new Color(0.62f, 0.78f, 0.42f, 1f);
            public static readonly Color PrimaryDim = new Color(0.38f, 0.48f, 0.27f, 1f);
            public static readonly Color Warning = new Color(0.95f, 0.68f, 0.25f, 1f);
            public static readonly Color Danger = new Color(0.88f, 0.28f, 0.22f, 1f);
            public static readonly Color Text = new Color(0.92f, 0.93f, 0.9f, 1f);
            public static readonly Color TextDim = new Color(0.62f, 0.64f, 0.62f, 1f);
            public static readonly Color Accent = new Color(0.45f, 0.7f, 0.95f, 1f);
        }
    }
}
