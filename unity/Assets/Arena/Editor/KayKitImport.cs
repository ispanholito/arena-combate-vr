using System.Collections.Generic;
using System.IO;
using Arena.Core;
using UnityEditor;
using UnityEngine;

namespace Arena.EditorTools
{
    // Importação do KayKit (Kay Lousberg, CC0 — kaylousberg.com), 2026-10-28.
    //   - Dungeon: as peças em Resources/KayKit/Dungeon ficam legíveis (Read/Write) para juntar o cenário em lote no jogo.
    //   - Esqueleto: o personagem do KayKit é UMA malha com esqueleto de animação; o nosso inimigo físico é feito de 11
    //     ossos separados (RagBone). Aqui cada triângulo vai para o osso que mais o move, e cada pedaço é encaixado no
    //     osso correspondente do nosso corpo (junta no lugar da junta, comprimento ajustado). Salva uma malha por osso em
    //     Resources/KayKit/Skeletons/Parts. Assim arrancar um braço arranca o braço do modelo.
    // Pelo CLI: unity command eval --code 'return Arena.EditorTools.KayKitImport.Run();'
    public static class KayKitImport
    {
        const string Root = "Assets/Arena/Resources/KayKit";
        const float Cross = 0.8f;   // escala da grossura (o modelo é mais "cabeçudo" que o nosso corpo)

        [MenuItem("Arena/4 - Importar KayKit (cenário + esqueleto em pedaços)")]
        static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Dungeon", Root + "/Skeletons" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;
                bool changed = false;
                if (!mi.isReadable) { mi.isReadable = true; changed = true; }
                if (mi.importAnimation) { mi.importAnimation = false; changed = true; }
                if (mi.importCameras || mi.importLights) { mi.importCameras = mi.importLights = false; changed = true; }
                if (changed) { mi.SaveAndReimport(); n++; }
            }
            string skel = SplitSkeleton("Skeleton_Warrior", "Warrior");
            return $"KayKit: {n} modelos reconfigurados; {skel}";
        }

        // Segmento do modelo (bind pose, em metros) → segmento do nosso corpo (RagdollGoblin.Build, escala 1).
        struct Map { public Vector3 a, b, A, B; public Map(Vector3 a, Vector3 b, Vector3 A, Vector3 B) { this.a = a; this.b = b; this.A = A; this.B = B; } }

        static Dictionary<RagBone, Map> Maps()
        {
            var m = new Dictionary<RagBone, Map>
            {
                [RagBone.Bacia] = new Map(new Vector3(0, 0.41f, 0), new Vector3(0, 0.60f, 0), new Vector3(0, 0.49f, 0), new Vector3(0, 0.63f, 0)),
                [RagBone.Tronco] = new Map(new Vector3(0, 0.60f, 0), new Vector3(0, 1.24f, 0), new Vector3(0, 0.63f, 0), new Vector3(0, 0.99f, 0)),
                [RagBone.Cabeca] = new Map(new Vector3(0, 1.24f, 0), new Vector3(0, 1.74f, 0), new Vector3(0, 0.97f, 0), new Vector3(0, 1.37f, 0)),
            };
            foreach (int s in new[] { -1, 1 })
            {
                bool r = s > 0;
                m[r ? RagBone.BracoD : RagBone.BracoE] = new Map(new Vector3(0.21f * s, 1.11f, 0), new Vector3(0.45f * s, 1.11f, 0), new Vector3(0.23f * s, 0.93f, 0), new Vector3(0.23f * s, 0.69f, 0));
                m[r ? RagBone.AntebracoD : RagBone.AntebracoE] = new Map(new Vector3(0.45f * s, 1.11f, 0), new Vector3(0.88f * s, 1.05f, 0), new Vector3(0.23f * s, 0.69f, 0), new Vector3(0.23f * s, 0.42f, 0));
                m[r ? RagBone.CoxaD : RagBone.CoxaE] = new Map(new Vector3(0.17f * s, 0.52f, 0), new Vector3(0.17f * s, 0.29f, 0), new Vector3(0.1f * s, 0.5f, 0), new Vector3(0.1f * s, 0.28f, 0));
                m[r ? RagBone.CanelaD : RagBone.CanelaE] = new Map(new Vector3(0.17f * s, 0.29f, 0), new Vector3(0.17f * s, 0f, 0), new Vector3(0.1f * s, 0.28f, 0), new Vector3(0.1f * s, 0f, 0));
            }
            return m;
        }

        // Nome do osso do KayKit → nosso osso.
        static RagBone BoneOf(string name)
        {
            bool l = name.EndsWith(".l"), r = name.EndsWith(".r");
            if (name.StartsWith("upperarm")) return l ? RagBone.BracoE : RagBone.BracoD;
            if (name.StartsWith("lowerarm") || name.StartsWith("wrist") || name.StartsWith("hand")) return l ? RagBone.AntebracoE : RagBone.AntebracoD;
            if (name.StartsWith("upperleg")) return l ? RagBone.CoxaE : RagBone.CoxaD;
            if (name.StartsWith("lowerleg") || name.StartsWith("foot") || name.StartsWith("toes")) return l ? RagBone.CanelaE : RagBone.CanelaD;
            if (name == "head") return RagBone.Cabeca;
            if (name == "chest" || name == "spine") return RagBone.Tronco;
            return RagBone.Bacia;
        }

        sealed class Part { public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>(); public readonly List<Vector2> UV = new List<Vector2>(); public readonly List<int> T = new List<int>(); }

        static string SplitSkeleton(string model, string tag)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Skeletons/{model}.fbx");
            if (!go) return $"{model}.fbx não encontrado (já cortado? as malhas ficam em Parts)";
            var maps = Maps();
            var parts = new Dictionary<RagBone, Part>();
            Part P(RagBone b) { if (!parts.TryGetValue(b, out var p)) parts[b] = p = new Part(); return p; }

            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                var verts = mesh.vertices; var norms = mesh.normals; var uvs = mesh.uv; var w = mesh.boneWeights; var bind = mesh.bindposes;
                // Posição de cada vértice no modelo em repouso (soma ponderada dos ossos) e o osso dominante.
                var pos = new Vector3[verts.Length]; var nor = new Vector3[verts.Length]; var dom = new RagBone[verts.Length];
                var mats = new Matrix4x4[smr.bones.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = smr.bones[i].localToWorldMatrix * bind[i];
                for (int v = 0; v < verts.Length; v++)
                {
                    var bw = w[v];
                    var mtx = Sum(mats, bw);
                    pos[v] = mtx.MultiplyPoint3x4(verts[v]);
                    nor[v] = mtx.MultiplyVector(norms[v]).normalized;
                    dom[v] = BoneOf(smr.bones[bw.boneIndex0].name);
                }
                var tris = mesh.triangles;
                for (int t = 0; t < tris.Length; t += 3)
                {
                    // O triângulo vai para o osso da maioria dos vértices (empate: o do 1º).
                    var b0 = dom[tris[t]]; var b1 = dom[tris[t + 1]]; var b2 = dom[tris[t + 2]];
                    var bone = b1 == b2 ? b1 : b0;
                    AddTri(P(bone), maps[bone], pos, nor, uvs, tris[t], tris[t + 1], tris[t + 2]);
                }
            }
            // Peças rígidas presas a um osso (capacete na cabeça).
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = mr.GetComponent<MeshFilter>(); if (!mf) continue;
                var bone = BoneOf(mr.transform.parent ? mr.transform.parent.name : "");
                var mesh = mf.sharedMesh; var verts = mesh.vertices; var norms = mesh.normals;
                var mtx = mr.transform.localToWorldMatrix;
                var pos = new Vector3[verts.Length]; var nor = new Vector3[verts.Length];
                for (int v = 0; v < verts.Length; v++) { pos[v] = mtx.MultiplyPoint3x4(verts[v]); nor[v] = mtx.MultiplyVector(norms[v]).normalized; }
                var tris = mesh.triangles;
                for (int t = 0; t < tris.Length; t += 3) AddTri(P(bone), maps[bone], pos, nor, mesh.uv, tris[t], tris[t + 1], tris[t + 2]);
            }

            var dir = $"{Root}/Skeletons/Parts";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder($"{Root}/Skeletons", "Parts");
            int saved = 0;
            foreach (var kv in parts)
            {
                var p = kv.Value;
                var mesh = new Mesh { name = $"{tag}_{kv.Key}" };
                mesh.SetVertices(p.V); mesh.SetNormals(p.N); mesh.SetUVs(0, p.UV); mesh.SetTriangles(p.T, 0);
                mesh.RecalculateBounds();
                var path = $"{dir}/{tag}_{kv.Key}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                saved++;
            }
            AssetDatabase.SaveAssets();
            return $"esqueleto cortado em {saved} pedaços ({string.Join(", ", parts.Keys)})";
        }

        static Matrix4x4 Sum(Matrix4x4[] m, BoneWeight w)
        {
            Matrix4x4 r = default;
            void Add(int i, float k) { if (k <= 0f) return; for (int e = 0; e < 16; e++) r[e] += m[i][e] * k; }
            Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
            return r;
        }

        // Leva o vértice do segmento do modelo (a→b) para o nosso (A→B): gira, ajusta o comprimento e afina a grossura.
        static Vector3 Fit(Map m, Vector3 v, out Quaternion rot)
        {
            var d = (m.b - m.a); float len = d.magnitude; d /= len;
            var D = (m.B - m.A); float LEN = D.magnitude; D /= LEN;
            rot = Quaternion.FromToRotation(d, D);
            var rel = v - m.a;
            float t = Vector3.Dot(rel, d);
            var perp = rel - d * t;
            return m.A + rot * (d * t * (LEN / len) + perp * Cross);
        }

        static void AddTri(Part p, Map m, Vector3[] pos, Vector3[] nor, Vector2[] uv, int i0, int i1, int i2)
        {
            foreach (int i in new[] { i0, i1, i2 })
            {
                p.V.Add(Fit(m, pos[i], out var rot));
                p.N.Add(rot * nor[i]);
                p.UV.Add(uv != null && uv.Length > i ? uv[i] : Vector2.zero);
                p.T.Add(p.V.Count - 1);
            }
        }
    }
}
