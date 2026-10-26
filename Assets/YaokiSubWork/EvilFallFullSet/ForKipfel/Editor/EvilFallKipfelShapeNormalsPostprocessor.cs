// Editor-only masked shape-normal correction for two reviewed EvilFall Kipfel meshes.
// Mutates the imported Mesh instance in place; never creates/replaces a Mesh asset.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvilFall.Kipfel.Editor
{
    internal sealed class EvilFallKipfelShapeNormalsPostprocessor : AssetPostprocessor
    {
        internal const string TargetPath = "Assets/YaokiSubWork/EvilFallFullSet/ForKipfel/EvilFall_Kipfel.fbx";
        internal const string TargetGuid = "02362ac3b72a643bca5074e35b3eec18";
        private static readonly string[] MeshNames = { "MainBodyBreast", "MainBodySuit" };
        private static string DataPath(string meshName) { return "Assets/YaokiSubWork/EvilFallFullSet/ForKipfel/EvilFall_Kipfel." + meshName + ".ShapeNormals.json"; }
        public override int GetPostprocessOrder() { return int.MaxValue; }
        public override uint GetVersion() { return 3; }
        private bool IsTarget { get { return string.Equals(assetPath, TargetPath, StringComparison.Ordinal); } }
        private void OnPreprocessModel()
        {
            if (!IsTarget) return;
            foreach (var name in MeshNames) context.DependsOnSourceAsset(DataPath(name));
        }
        private void OnPostprocessModel(GameObject root)
        {
            if (!IsTarget) return;
            var applied = new List<ShapeNormalPatchJob>();
            try
            {
                var guidMatch = Regex.Match(File.ReadAllText(TargetPath + ".meta"), @"(?m)^guid:\s*([0-9a-f]{32})\s*$");
                if (!guidMatch.Success || guidMatch.Groups[1].Value != TargetGuid)
                    throw new InvalidDataException("Target model GUID changed; regenerate correction data.");
                var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var meshes = new HashSet<Mesh>(renderers.Where(r => r.sharedMesh != null).Select(r => r.sharedMesh));
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true)) if (filter.sharedMesh != null) meshes.Add(filter.sharedMesh);
                var jobs = new List<ShapeNormalPatchJob>();
                foreach (var name in MeshNames)
                {
                    context.DependsOnSourceAsset(DataPath(name));
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<ShapeNormalsPatchData>(File.ReadAllText(DataPath(name), Encoding.UTF8));
                    if (data == null || data.modelAssetPath != TargetPath || data.modelGuid != TargetGuid || data.meshName != name)
                        throw new InvalidDataException("Correction JSON targets a different model or mesh.");
                    if (meshes.Count != data.expectedModelMeshCount)
                        throw new InvalidDataException("Model mesh count changed; review the correction data.");
                    var targets = meshes.Where(m => m.name == name).ToArray();
                    if (targets.Length != 1) throw new InvalidDataException("Expected exactly one " + name + " mesh.");
                    var mesh = targets[0]; var owners = renderers.Where(r => r.sharedMesh == mesh).ToArray();
                    // Preflight BOTH targets completely before either Mesh is mutated.
                    ShapeNormalsPatch.Apply(mesh, data, owners, true);
                    jobs.Add(new ShapeNormalPatchJob { mesh=mesh, data=data, owners=owners,
                        frames=ShapeNormalsPatch.CaptureFrames(mesh), bounds=mesh.bounds,
                        weights=owners.Select(r => Enumerable.Range(0,mesh.blendShapeCount).Select(r.GetBlendShapeWeight).ToArray()).ToArray() });
                }
                foreach (var job in jobs)
                {
                    ShapeNormalsPatch.Apply(job.mesh, job.data, job.owners);
                    applied.Add(job);
                }
                Debug.Log("EvilFall: applied guarded masked shape normals to MainBodyBreast and MainBodySuit; geometry and all other meshes are unchanged.", root);
            }
            catch (Exception error)
            {
                // The currently failing Mesh is restored inside Apply; restore any earlier successful target too.
                for (int i=applied.Count-1;i>=0;--i)
                {
                    var job=applied[i];
                    try { ShapeNormalsPatch.Restore(job.mesh,job.frames,job.bounds,job.owners,job.weights); }
                    catch (Exception rollbackError) { context.LogImportError("EvilFall normal rollback failed: " + rollbackError.Message, root); }
                }
                context.LogImportError("EvilFall shape normal correction refused: " + error.Message, root);
                throw;
            }
        }
    }
    internal sealed class ShapeNormalPatchJob
    {
        internal Mesh mesh; internal ShapeNormalsPatchData data; internal SkinnedMeshRenderer[] owners;
        internal List<CapturedShapeFrame> frames; internal Bounds bounds; internal float[][] weights;
    }

    #pragma warning disable 0649 // Newtonsoft.Json populates these serialized DTO fields.
    [Serializable]
    public sealed class ShapeNormalsPatchData
    {
        public int schemaVersion;
        public string modelAssetPath, modelGuid, meshName, expectedLocalFileId;
        public int expectedModelMeshCount, expectedVertexCount, expectedSubMeshCount;
        public string guardVersion, basePositionUvFingerprintSha256, immutableFingerprintSha256, sourceBlendSha256;
        public string[] vertexKeys, patchedShapeNames;
        public ShapeNormalFrameData[] frames;
    }

    [Serializable]
    public sealed class ShapeNormalFrameData
    {
        public string shapeName;
        public float weight;
        public float[] deltaNormals;
        public int[] applyMask;
    }

    #pragma warning restore 0649

    internal sealed class CapturedShapeFrame
    {
        internal string name;
        internal float weight;
        internal Vector3[] vertices, normals, tangents;
    }

    internal static class ShapeNormalsPatch
    {
        // A source geometry update must regenerate the JSON. Guards fail before any mutation.
        internal static void Apply(Mesh mesh, ShapeNormalsPatchData data, SkinnedMeshRenderer[] owners, bool validateOnly = false)
        {
            if (data.schemaVersion != 2 || data.guardVersion != "EFN-M1")
                throw new InvalidDataException("Unknown shape normal data schema or fingerprint version.");
            if (mesh.name != data.meshName || mesh.vertexCount != data.expectedVertexCount || mesh.subMeshCount != data.expectedSubMeshCount)
                throw new InvalidDataException("Mesh identity/counts differ from the reviewed mesh.");
            if (mesh.blendShapeCount != 6 || data.frames == null || data.frames.Length != 6)
                throw new InvalidDataException("Expected the six reviewed blendshapes; mesh=" + mesh.blendShapeCount + ", parsed frames=" + (data.frames == null ? -1 : data.frames.Length));
            if (data.vertexKeys == null || data.vertexKeys.Length != mesh.vertexCount)
                throw new InvalidDataException("Vertex identity table length differs from the mesh.");

            var oldFrames = CaptureFrames(mesh);
            if (oldFrames.Count != 6)
                throw new InvalidDataException("Expected one frame per shape; multiple frames require regeneration.");
            var keys = VertexKeys(mesh, oldFrames, false);
            var uvFingerprint = BasePositionUvFingerprint(mesh, oldFrames);
            var immutable = ImmutableFingerprint(mesh, oldFrames, keys);
            if (uvFingerprint != data.basePositionUvFingerprintSha256 || immutable != data.immutableFingerprintSha256)
                throw new InvalidDataException("Geometry/UV/base normal/tangent/shape displacement fingerprint changed; do not apply stale correction data.");
            var dataIndexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < data.vertexKeys.Length; ++i)
            {
                if (dataIndexByKey.ContainsKey(data.vertexKeys[i]))
                    throw new InvalidDataException("Ambiguous correction vertex identities.");
                dataIndexByKey.Add(data.vertexKeys[i], i);
            }
            var newNormals = new Vector3[oldFrames.Count][];
            for (int f = 0; f < oldFrames.Count; ++f)
            {
                var frame = data.frames[f];
                if (frame.shapeName != oldFrames[f].name || frame.weight != oldFrames[f].weight ||
                    frame.deltaNormals == null || frame.deltaNormals.Length != mesh.vertexCount * 3 ||
                    frame.applyMask == null || frame.applyMask.Length != mesh.vertexCount)
                    throw new InvalidDataException("Shape order/name/weight/normal count differs from reviewed data.");
                bool declaredPatch = data.patchedShapeNames != null && data.patchedShapeNames.Contains(frame.shapeName);
                newNormals[f] = (Vector3[])oldFrames[f].normals.Clone();
                for (int i = 0; i < mesh.vertexCount; ++i)
                {
                    int oldIndex;
                    if (!dataIndexByKey.TryGetValue(keys[i], out oldIndex))
                        throw new InvalidDataException("An imported split vertex has no source normal correction.");
                    int mask = frame.applyMask[oldIndex];
                    if (mask != 0 && mask != 1) throw new InvalidDataException("Normal applyMask must contain only0 or1.");
                    if (mask == 0) continue; // Preserve this import's LIVE DN, including all skipped fans/collapses.
                    if (!declaredPatch) throw new InvalidDataException("An unlisted shape frame contains a correction mask.");
                    int p = oldIndex * 3;
                    newNormals[f][i] = new Vector3(Finite(frame.deltaNormals[p]), Finite(frame.deltaNormals[p + 1]), Finite(frame.deltaNormals[p + 2]));
                }
            }

            if (validateOnly) return;

            // All validation has finished. Preserve the same Mesh object, all frame geometry and defaults.
            int originalInstanceId = mesh.GetInstanceID();
            var originalBounds = mesh.bounds;
            var ownerWeights = owners.Select(r => Enumerable.Range(0, mesh.blendShapeCount).Select(r.GetBlendShapeWeight).ToArray()).ToArray();
            try
            {
                RebuildFrames(mesh, oldFrames, newNormals);
                mesh.bounds = originalBounds;
                RestoreWeights(owners, ownerWeights);
                if (mesh.GetInstanceID() != originalInstanceId ||
                    ImmutableFingerprint(mesh, CaptureFrames(mesh), VertexKeys(mesh, CaptureFrames(mesh), false)) != immutable)
                    throw new InvalidOperationException("A protected mesh field changed while replacing shape normals.");
            }
            catch
            {
                RebuildFrames(mesh, oldFrames, oldFrames.Select(f => f.normals).ToArray());
                mesh.bounds = originalBounds;
                RestoreWeights(owners, ownerWeights);
                throw;
            }
        }

        internal static void Restore(Mesh mesh, List<CapturedShapeFrame> frames, Bounds bounds, SkinnedMeshRenderer[] owners, float[][] weights)
        {
            RebuildFrames(mesh, frames, frames.Select(f => f.normals).ToArray());
            mesh.bounds = bounds; RestoreWeights(owners, weights);
        }

        private static void RestoreWeights(SkinnedMeshRenderer[] owners, float[][] weights)
        {
            for (int r = 0; r < owners.Length; ++r)
                for (int s = 0; s < weights[r].Length; ++s) owners[r].SetBlendShapeWeight(s, weights[r][s]);
        }

        private static void RebuildFrames(Mesh mesh, List<CapturedShapeFrame> frames, Vector3[][] normals)
        {
            mesh.ClearBlendShapes();
            for (int i = 0; i < frames.Count; ++i)
                mesh.AddBlendShapeFrame(frames[i].name, frames[i].weight, frames[i].vertices, normals[i], frames[i].tangents);
        }

        internal static List<CapturedShapeFrame> CaptureFrames(Mesh mesh)
        {
            var result = new List<CapturedShapeFrame>();
            for (int s = 0; s < mesh.blendShapeCount; ++s)
            {
                if (mesh.GetBlendShapeFrameCount(s) != 1)
                    throw new InvalidDataException("Only the reviewed one-frame shapes are supported.");
                var f = new CapturedShapeFrame {
                    name = mesh.GetBlendShapeName(s), weight = mesh.GetBlendShapeFrameWeight(s, 0),
                    vertices = new Vector3[mesh.vertexCount], normals = new Vector3[mesh.vertexCount], tangents = new Vector3[mesh.vertexCount]
                };
                mesh.GetBlendShapeFrameVertices(s, 0, f.vertices, f.normals, f.tangents);
                result.Add(f);
            }
            return result;
        }

        private static List<Vector4>[] CaptureUvs(Mesh mesh)
        {
            var uv = new List<Vector4>[8];
            for (int c = 0; c < 8; ++c)
            {
                uv[c] = new List<Vector4>(); mesh.GetUVs(c, uv[c]);
                if (uv[c].Count != 0 && uv[c].Count != mesh.vertexCount)
                    throw new InvalidDataException("Unexpected UV channel length.");
            }
            return uv;
        }

        internal static string[] VertexKeys(Mesh mesh, List<CapturedShapeFrame> frames, bool uvOnly)
        {
            var v = mesh.vertices; var n = mesh.normals; var t = mesh.tangents; var uv = CaptureUvs(mesh);
            if (!uvOnly && (n.Length != v.Length || t.Length != v.Length))
                throw new InvalidDataException("Reviewed base normals/tangents are missing.");
            var keys = new string[v.Length];
            for (int i = 0; i < v.Length; ++i)
            {
                using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(Encoding.ASCII.GetBytes(uvOnly ? "EFN-U1\0" : "EFN-V1\0"));
                    Write(writer, v[i]);
                    if (!uvOnly) { Write(writer, n[i]); Write(writer, t[i]); }
                    writer.Write(8);
                    for (int c = 0; c < 8; ++c) { writer.Write(uv[c].Count == 0 ? 0 : 1); if (uv[c].Count != 0) Write(writer, uv[c][i]); }
                    if (!uvOnly) foreach (var f in frames) { Write(writer, f.vertices[i]); Write(writer, f.tangents[i]); }
                    writer.Flush(); keys[i] = Sha256(stream.ToArray());
                }
            }
            return keys;
        }

        internal static string BasePositionUvFingerprint(Mesh mesh, List<CapturedShapeFrame> frames)
        {
            var keys = VertexKeys(mesh, frames, true);
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("EFN-U-M1\0")); writer.Write(mesh.vertexCount); writer.Write(keys.Length);
                foreach (var key in keys.OrderBy(k => k, StringComparer.Ordinal)) WriteString(writer, key);
                writer.Flush(); return Sha256(stream.ToArray());
            }
        }

        internal static string ImmutableFingerprint(Mesh mesh, List<CapturedShapeFrame> frames, string[] keys)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("EFN-M1\0")); WriteString(writer, mesh.name);
                writer.Write(mesh.vertexCount); writer.Write(mesh.subMeshCount); writer.Write(mesh.blendShapeCount);
                foreach (var f in frames) { WriteString(writer, f.name); writer.Write(1); WriteFloat(writer, f.weight); }
                writer.Write(keys.Length);
                foreach (var key in keys.OrderBy(k => k, StringComparer.Ordinal)) WriteString(writer, key);
                for (int s = 0; s < mesh.subMeshCount; ++s)
                {
                    var topology = mesh.GetTopology(s);
                    if (topology != MeshTopology.Triangles) throw new InvalidDataException("Expected triangle submeshes.");
                    writer.Write((int)topology); var indices = mesh.GetIndices(s, true); var triangles = new List<string>();
                    if (indices.Length % 3 != 0) throw new InvalidDataException("Invalid triangle index count.");
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        string a = keys[indices[i]], b = keys[indices[i + 1]], c = keys[indices[i + 2]];
                        triangles.Add(new[] {a + "|" + b + "|" + c, b + "|" + c + "|" + a, c + "|" + a + "|" + b}.OrderBy(x => x, StringComparer.Ordinal).First());
                    }
                    writer.Write(triangles.Count);
                    foreach (var triangle in triangles.OrderBy(x => x, StringComparer.Ordinal)) WriteString(writer, triangle);
                }
                writer.Flush(); return Sha256(stream.ToArray());
            }
        }

        private static float Finite(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite correction data.");
            return value == 0f ? 0f : value;
        }
        private static void WriteFloat(BinaryWriter w, float value) { w.Write(Finite(value)); }
        private static void Write(BinaryWriter w, Vector3 v) { WriteFloat(w, v.x); WriteFloat(w, v.y); WriteFloat(w, v.z); }
        private static void Write(BinaryWriter w, Vector4 v) { WriteFloat(w, v.x); WriteFloat(w, v.y); WriteFloat(w, v.z); WriteFloat(w, v.w); }
        private static void WriteString(BinaryWriter w, string text) { var bytes = Encoding.UTF8.GetBytes(text); w.Write(bytes.Length); w.Write(bytes); }
        private static string Sha256(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
