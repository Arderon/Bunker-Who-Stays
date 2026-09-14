using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bunker.UI.GameV2.EditorTools
{
    // Generates the whole V2 sprite set as white alpha masks, tinted at use.
    // Fifteen sprites cover every screen — no per-screen bitmaps and no baked
    // text, since every stamp and badge is TMP text inside a 9-slice box.
    //
    // Tools > Bunker > Generate UI Sprites. Safe to re-run; it overwrites.
    public static class BunkerSpriteGenerator
    {
        private const string OutputFolder = "Assets/Art/UI/GameV2";

        [MenuItem("Tools/Bunker/Generate UI Sprites")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputFolder);

            // 9-slice plates. Border = radius + 1 so the corner arc is never
            // stretched; the 1px straight edge in the middle is what tiles.
            WriteRounded("panel_r2", 2);
            WriteRounded("panel_r4", 4);
            WriteRounded("panel_r8", 8);
            WriteDashedPanel("panel_r8_dashed", 8, dash: 16);

            // Tiled patterns. Period equals the tile size so they wrap seamlessly.
            Write("hatch_135", MakeHatch(24, 12, flip: false), tile: true);
            Write("hatch_135_wide", MakeHatch(28, 14, flip: false), tile: true);
            Write("hazard_stripe", MakeHatch(32, 16, flip: true), tile: true);
            Write("grain", MakeScanlines(), tile: true);

            Write("timer_ring", MakeRing(176, 14));

            Write("icon_triangle", MakeTriangle(48));
            Write("icon_cross", MakeStrokes(48, 5f, new[]
            {
                new Vector4(11.5f, 11.5f, 36.5f, 36.5f),
                new Vector4(11.5f, 36.5f, 36.5f, 11.5f)
            }));
            Write("icon_check", MakeStrokes(48, 5f, new[]
            {
                new Vector4(9.6f, 25f, 20.2f, 13.4f),
                new Vector4(20.2f, 13.4f, 38.4f, 34.6f)
            }));
            Write("icon_circle_hollow", MakeRing(48, 4));
            Write("icon_square", MakeSquare(48));

            // Filled disc — the timer ring's inner face, which has to occlude the
            // radial fill rather than let it show through the middle.
            Write("disc", MakeDisc(128));

            Write("vignette", MakeVignette(192));
            Write("glow_bottom", MakeGlow(128, 64));

            AssetDatabase.Refresh();
            Debug.Log($"[Bunker] UI sprites written to {OutputFolder}");
        }

        // --- Writing ----------------------------------------------------------

        private static void WriteRounded(string name, int radius)
        {
            int border = radius + 1;
            Write(name, MakeRounded(radius), border: new Vector4(border, border, border, border));
        }

        private static void WriteDashedPanel(string name, int radius, int dash)
        {
            // The dash has to survive slicing, so this sprite is used with Image
            // Type = Tiled: Unity then tiles the edge slices instead of
            // stretching them, and the dash period stays constant at any size.
            int border = radius + dash;
            Write(name, MakeDashedRing(radius, dash), border: new Vector4(border, border, border, border));
        }

        private static void Write(string name, Texture2D tex, bool tile = false, Vector4? border = null)
        {
            string path = $"{OutputFolder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            if (border.HasValue)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = border.Value;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }

            importer.SaveAndReimport();
        }

        // --- Generation -------------------------------------------------------

        private static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false);
        }

        private static Texture2D FromAlpha(int w, int h, System.Func<float, float, float> alphaAt)
        {
            var tex = NewTex(w, h);
            var px = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float a = Mathf.Clamp01(alphaAt(x + 0.5f, y + 0.5f));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // Signed distance to a rounded box centred in a `size` square.
        private static float RoundedSdf(float x, float y, float size, float radius)
        {
            float half = size * 0.5f;
            float b = half - radius;
            float dx = Mathf.Abs(x - half) - b;
            float dy = Mathf.Abs(y - half) - b;
            float ox = Mathf.Max(dx, 0f);
            float oy = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
        }

        private static Texture2D MakeRounded(int radius)
        {
            int size = radius * 2 + 4;
            return FromAlpha(size, size, (x, y) => 0.5f - RoundedSdf(x, y, size, radius));
        }

        // A rounded ring whose straight runs are dashed. Corners stay solid, so
        // the arc never breaks mid-curve.
        private static Texture2D MakeDashedRing(int radius, int dash)
        {
            int size = (radius + dash) * 2 + 4;
            float half = size * 0.5f;

            return FromAlpha(size, size, (x, y) =>
            {
                float d = RoundedSdf(x, y, size, radius + dash);
                float ring = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + 2.5f);
                if (ring <= 0f) return 0f;

                // Dash along whichever axis this edge runs.
                bool horizontalEdge = Mathf.Abs(y - half) > Mathf.Abs(x - half);
                float along = horizontalEdge ? x : y;
                bool onDash = Mathf.Repeat(along, dash * 2f) < dash;
                return onDash ? ring : 0f;
            });
        }

        private static Texture2D MakeHatch(int period, int on, bool flip)
        {
            return FromAlpha(period, period, (x, y) =>
            {
                float t = flip
                    ? Mathf.Repeat(x - y, period)
                    : Mathf.Repeat(x + y, period);
                return t < on ? 1f : 0f;
            });
        }

        private static Texture2D MakeRing(int size, int thickness)
        {
            float outer = size * 0.5f;
            float inner = outer - thickness;
            float c = outer;

            return FromAlpha(size, size, (x, y) =>
            {
                float dx = x - c, dy = y - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                return Mathf.Clamp01(outer - r) * Mathf.Clamp01(r - inner);
            });
        }

        private static Texture2D MakeSquare(int size)
        {
            return FromAlpha(size, size, (x, y) => 1f);
        }

        private static Texture2D MakeDisc(int size)
        {
            float c = size * 0.5f;
            return FromAlpha(size, size, (x, y) =>
            {
                float dx = x - c, dy = y - c;
                return c - Mathf.Sqrt(dx * dx + dy * dy);
            });
        }

        private static Texture2D MakeTriangle(int size)
        {
            float h = size * 0.5f;
            return FromAlpha(size, size, (x, y) =>
            {
                // Apex at the right edge, base along the left — rotate at use for
                // chevrons, carets and back arrows.
                float span = h * (1f - x / size);
                return span - Mathf.Abs(y - h) + 0.5f;
            });
        }

        private static float SegDistance(float px, float py, Vector4 s)
        {
            float vx = s.z - s.x, vy = s.w - s.y;
            float wx = px - s.x, wy = py - s.y;
            float len2 = vx * vx + vy * vy;
            float t = len2 <= 0f ? 0f : Mathf.Clamp01((wx * vx + wy * vy) / len2);
            float cx = s.x + vx * t, cy = s.y + vy * t;
            return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        }

        private static Texture2D MakeStrokes(int size, float width, Vector4[] segments)
        {
            float half = width * 0.5f;
            return FromAlpha(size, size, (x, y) =>
            {
                float best = float.MaxValue;
                foreach (var s in segments)
                {
                    float d = SegDistance(x, y, s);
                    if (d < best) best = d;
                }
                return half - best + 0.5f;
            });
        }

        // radial-gradient(120% 80% at 50% 30%, transparent 40%, black 100%)
        private static Texture2D MakeVignette(int size)
        {
            float cx = size * 0.5f, cy = size * 0.7f; // y is flipped vs CSS
            float rx = size * 0.60f, ry = size * 0.40f;

            return FromAlpha(size, size, (x, y) =>
            {
                float dx = (x - cx) / rx, dy = (y - cy) / ry;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                return Mathf.InverseLerp(0.40f, 1.0f, r);
            });
        }

        private static Texture2D MakeScanlines()
        {
            return FromAlpha(4, 4, (x, y) => y < 1f ? 1f : 0f);
        }

        private static Texture2D MakeGlow(int w, int h)
        {
            float cx = w * 0.5f;
            return FromAlpha(w, h, (x, y) =>
            {
                float dx = (x - cx) / (w * 0.60f);
                float dy = y / (float)h;           // brightest at the bottom edge
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                return 1f - Mathf.InverseLerp(0f, 0.70f, r);
            });
        }
    }
}
