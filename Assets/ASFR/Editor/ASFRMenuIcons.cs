using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace ASFR
{
    static class ASFRMenuIcons
    {
        const int Size = 256;
        static readonly Color Red = new Color(.95f, .2f, .28f, 1);
        public static Texture2D MaterialPreview(Material material, string folder, int index)
        {
            Texture2D preview = null;
            Texture2D silhouette = null;
            Texture2D cutout = null;
            Material maskMaterial = null;
            try
            {
                // Shader-editor thumbnails bake in their own background. Render our
                // own sphere and a separate coverage pass to retain only its outline.
                preview = SpherePreview(material, Color.clear);
                if (!preview)
                    throw new InvalidOperationException("Could not render material icon for " + material.name);
                var maskShader = Shader.Find("Unlit/Color");
                if (!maskShader)
                    throw new InvalidOperationException("Unity's Unlit/Color shader is missing; cannot render the material icon mask.");
                maskMaterial = new Material(maskShader)
                {color = Color.white, hideFlags = HideFlags.HideAndDontSave};
                silhouette = SpherePreview(maskMaterial, Color.black);
                var pixels = ReadPixels(preview);
                var mask = ReadPixels(silhouette);
                for (int i = 0; i < pixels.Length; i++)
                {
                    float coverage = Mathf.Clamp01(mask[i].r);
                    // Clear hidden RGB as well, preventing a dark square in consumers
                    // which filter transparent edges. The source material is untouched.
                    pixels[i].a = Mathf.Min(pixels[i].a, coverage);
                    if (coverage < .001f)
                        pixels[i] = Color.clear;
                }

                cutout = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                cutout.SetPixels(pixels);
                cutout.Apply();
                return Save(cutout, folder + "/MaterialIcon_" + index + ".png");
            }
            finally
            {
                if (preview)
                    UnityEngine.Object.DestroyImmediate(preview);
                if (silhouette)
                    UnityEngine.Object.DestroyImmediate(silhouette);
                if (cutout)
                    UnityEngine.Object.DestroyImmediate(cutout);
                if (maskMaterial)
                    UnityEngine.Object.DestroyImmediate(maskMaterial);
            }
        }

        static Texture2D SpherePreview(Material material, Color background)
        {
            var utility = new PreviewRenderUtility();
            GameObject sphere = null;
            try
            {
                sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.hideFlags = HideFlags.HideAndDontSave;
                var mesh = sphere.GetComponent<MeshFilter>().sharedMesh;
                sphere.SetActive(false);
                utility.BeginStaticPreview(new Rect(0, 0, Size, Size));
                utility.camera.transform.position = new Vector3(0, 0, -2.2f);
                utility.camera.transform.rotation = Quaternion.identity;
                utility.camera.fieldOfView = 30;
                utility.camera.nearClipPlane = .1f;
                utility.camera.farClipPlane = 10;
                utility.camera.clearFlags = CameraClearFlags.Color;
                utility.camera.backgroundColor = background;
                utility.lights[0].intensity = 1.3f;
                utility.lights[0].transform.rotation = Quaternion.Euler(35, 35, 0);
                utility.lights[1].intensity = .8f;
                utility.lights[1].transform.rotation = Quaternion.Euler(340, 210, 0);
                utility.DrawMesh(mesh, Matrix4x4.identity, material, 0);
                utility.Render(true);
                return utility.EndStaticPreview();
            }
            finally
            {
                utility.Cleanup();
                if (sphere)
                    UnityEngine.Object.DestroyImmediate(sphere);
            }
        }

        static Color[] ReadPixels(Texture2D texture)
        {
            var rt = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                readable = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                readable.Apply();
                return readable.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                if (readable)
                    UnityEngine.Object.DestroyImmediate(readable);
            }
        }

        static Texture2D Save(Texture2D texture, string path)
        {
            // Copy through the GPU: editor previews are not always CPU-readable.
            var rt = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                readable = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                readable.Apply();
                File.WriteAllBytes(path, readable.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                if (readable)
                    UnityEngine.Object.DestroyImmediate(readable);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static void Decorate(VRCExpressionsMenu root, string folder)
        {
            var visited = new HashSet<VRCExpressionsMenu>();
            var icons = new Dictionary<string, Texture2D>();
            void Walk(VRCExpressionsMenu menu)
            {
                if (!menu || !visited.Add(menu))
                    return;
                foreach (var control in menu.controls)
                {
                    if (!control.icon)
                    {
                        if (!icons.TryGetValue(control.name, out var icon))
                        {
                            var canvas = new Canvas();
                            Draw(canvas, control.name);
                            var texture = canvas.Texture();
                            try
                            {
                                icon = Save(texture, folder + "/Icon_" + string.Concat(control.name.Where(char.IsLetterOrDigit)) + ".png");
                            }
                            finally
                            {
                                UnityEngine.Object.DestroyImmediate(texture);
                            }

                            icons.Add(control.name, icon);
                        }

                        control.icon = icon;
                    }

                    Walk(control.subMenu);
                }

                EditorUtility.SetDirty(menu);
            }

            Walk(root);
        }

        // All coordinates are on a 128-unit canvas. Stroke coverage provides antialiasing.
        sealed class Canvas
        {
            readonly Color[] pixels = new Color[Size * Size];
            public void Line(float ax, float ay, float bx, float by, float width = 6, bool red = false)
            {
                var a = new Vector2(ax, ay) * 2;
                var b = new Vector2(bx, by) * 2;
                var ab = b - a;
                float radius = width;
                for (int y = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - 1)); y < Mathf.Min(Size, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius + 1)); y++)
                    for (int x = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - 1)); x < Mathf.Min(Size, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius + 1)); x++)
                    {
                        var p = new Vector2(x + .5f, y + .5f);
                        float t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0;
                        float alpha = Mathf.Clamp01(radius + .5f - (p - a - t * ab).magnitude);
                        int i = y * Size + x;
                        if (alpha > 0)
                        {
                            var color = red ? Red : Color.white;
                            float combined = alpha + pixels[i].a * (1 - alpha);
                            color = (color * alpha + pixels[i] * pixels[i].a * (1 - alpha)) / combined;
                            color.a = combined;
                            pixels[i] = color;
                        }
                    }
            }

            public void Ring(float x, float y, float r, float width = 6, bool red = false, float start = 0, float end = 360)
            {
                for (float a = start; a < end; a += 4)
                {
                    float b = Mathf.Min(a + 4, end) * Mathf.Deg2Rad, c = a * Mathf.Deg2Rad;
                    Line(x + r * Mathf.Cos(c), y + r * Mathf.Sin(c), x + r * Mathf.Cos(b), y + r * Mathf.Sin(b), width, red);
                }
            }

            public void Poly(params float[] xy)
            {
                for (int i = 0; i + 3 < xy.Length; i += 2)
                    Line(xy[i], xy[i + 1], xy[i + 2], xy[i + 3]);
            }

            public Texture2D Texture()
            {
                var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                t.SetPixels(pixels);
                t.Apply();
                return t;
            }
        }

        static void Person(Canvas c, float x, float y, float scale = 1)
        {
            c.Ring(x, y + 18 * scale, 9 * scale, 5);
            c.Ring(x, y - 10 * scale, 18 * scale, 5, false, 0, 180);
            c.Line(x - 18 * scale, y - 10 * scale, x + 18 * scale, y - 10 * scale, 5);
        }

        static void Shirt(Canvas c)
        {
            c.Poly(43, 101, 24, 91, 14, 70, 34, 61, 39, 74, 39, 25, 89, 25, 89, 74, 94, 61, 114, 70, 104, 91, 85, 101);
            c.Ring(64, 103, 20, 6, false, 190, 350);
        }

        static void Eye(Canvas c)
        {
            c.Poly(14, 64, 34, 83, 64, 94, 94, 83, 114, 64, 94, 45, 64, 34, 34, 45, 14, 64);
            c.Ring(64, 64, 17, 6, true);
        }

        static void Draw(Canvas c, string label)
        {
            switch (label)
            {
                case "Options":
                    c.Ring(64, 64, 31, 12);
                    c.Ring(64, 64, 13, 6, true);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4;
                        c.Line(64 + 33 * Mathf.Cos(a), 64 + 33 * Mathf.Sin(a), 64 + 46 * Mathf.Cos(a), 64 + 46 * Mathf.Sin(a), 12);
                    }

                    break;
                case "Transform / Reset":
                    c.Ring(64, 60, 37, 8, false, 135, 405);
                    c.Line(64, 69, 64, 110, 8, true);
                    break;
                case "ASFR":
                case "Transformation":
                    c.Poly(64, 112, 99, 77, 64, 16, 29, 77, 64, 112);
                    c.Line(29, 77, 99, 77, 5, true);
                    c.Line(64, 112, 64, 16, 4);
                    break;
                case "ExtraInteractions":
                    Person(c, 43, 64);
                    Person(c, 87, 57, .85f);
                    c.Line(52, 28, 79, 28, 6, true);
                    break;
                case "Allow Others":
                    Person(c, 47, 64);
                    c.Line(80, 47, 90, 37, 7, true);
                    c.Line(90, 37, 113, 68, 7, true);
                    break;
                case "Petrify Others":
                    Person(c, 90, 56, .85f);
                    c.Poly(20, 105, 42, 105, 31, 78, 49, 78, 21, 39, 27, 68, 13, 68, 20, 105);
                    break;
                case "Allow Statue Grab":
                    c.Poly(37, 32, 24, 60, 26, 68, 32, 69, 43, 54, 43, 96, 49, 101, 55, 96, 55, 70, 55, 110, 62, 114, 68, 110, 68, 72, 68, 102, 75, 106, 81, 101, 81, 68, 81, 88, 87, 92, 94, 87, 94, 49, 84, 25, 46, 25);
                    c.Line(48, 17, 83, 17, 5, true);
                    break;
                case "Hide Grab Ball":
                    c.Ring(64, 64, 32);
                    c.Line(24, 24, 104, 104, 8, true);
                    break;
                case "Statue Camera View":
                    c.Poly(20, 36, 20, 90, 40, 90, 47, 102, 78, 102, 85, 90, 108, 90, 108, 36, 20, 36);
                    c.Ring(64, 63, 20, 7, true);
                    break;
                case "Clothing":
                case "Transform everything":
                    Shirt(c);
                    break;
                case "Keep clothing unchanged":
                    Shirt(c);
                    c.Line(52, 54, 61, 43, 6, true);
                    c.Line(61, 43, 78, 64, 6, true);
                    break;
                case "Remove clothing":
                    Shirt(c);
                    c.Line(23, 20, 106, 107, 8, true);
                    break;
                case "Duration (0–60 seconds)":
                    c.Ring(64, 59, 37);
                    c.Line(64, 59, 64, 83, 6, true);
                    c.Line(64, 59, 82, 49, 6, true);
                    c.Line(53, 110, 75, 110);
                    c.Line(64, 110, 64, 98);
                    break;
                case "Transformation amount":
                    c.Ring(64, 61, 39, 7, false, 0, 180);
                    c.Line(25, 35, 103, 35);
                    c.Line(64, 61, 88, 85, 7, true);
                    c.Ring(64, 61, 5, 5, true);
                    break;
                case "Vision":
                case "Vision overlay":
                    Eye(c);
                    break;
                case "Darkness":
                    c.Ring(61, 66, 39, 8, false, 70, 290);
                    c.Ring(80, 66, 29, 6, false, 95, 265);
                    break;
                case "Ice":
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3;
                        var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        var n = new Vector2(-v.y, v.x);
                        var p = new Vector2(64, 64) + v * 28;
                        c.Line(64, 64, 64 + v.x * 44, 64 + v.y * 44, 5);
                        c.Line(p.x, p.y, p.x - v.x * 11 + n.x * 11, p.y - v.y * 11 + n.y * 11, 4, true);
                        c.Line(p.x, p.y, p.x - v.x * 11 - n.x * 11, p.y - v.y * 11 - n.y * 11, 4, true);
                    }

                    break;
                case "Custom Texture":
                    c.Poly(21, 24, 21, 104, 107, 104, 107, 24, 21, 24);
                    c.Poly(25, 31, 50, 60, 69, 41, 91, 74, 104, 54);
                    c.Ring(46, 83, 8, 6, true);
                    break;
                case "Intensity":
                    c.Ring(64, 64, 22, 7, true);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4;
                        c.Line(64 + 35 * Mathf.Cos(a), 64 + 35 * Mathf.Sin(a), 64 + 47 * Mathf.Cos(a), 64 + 47 * Mathf.Sin(a), 5);
                    }

                    break;
                default:
                    throw new InvalidOperationException("Missing ASFR menu icon: " + label);
            }
        }
    }
}
