using System.Collections.Generic;
using System.IO;
using Arena.Core;
using UnityEditor;
using UnityEngine;

namespace Arena.EditorTools
{
    // Modelos gerados por IA (Gemini → TRELLIS/Hunyuan; ver docs/pipeline-personagens.md), 2026-10-28.
    // Entrada: arte/<nome>/<nome>.json (malha já reduzida pelo glb2arena.js, coordenadas do glTF) e <nome>_cor.png.
    // O modelo vem em pose T, sem esqueleto: cada triângulo vai para um dos 11 ossos do corpo físico pela POSIÇÃO
    // (cabeça acima do pescoço, braços além do ombro, pernas abaixo do quadril...), e cada pedaço é encaixado no osso
    // correspondente do RagdollGoblin (junta na junta, comprimento ajustado). Salva uma malha por osso em
    // Resources/Models/<Nome>/<Nome>_<Osso>.asset; RagdollGoblin.DressModel usa.
    // Pelo CLI: unity command eval --code 'return Arena.EditorTools.AiModelImport.Goblin();'
    public static class AiModelImport
    {
        [System.Serializable] class Raw { public float[] positions, normals, uvs; public int[] indices; }

        [MenuItem("Arena/5 - Importar goblin gerado por IA (arte/goblin)")]
        static void Menu() => Debug.Log(Goblin());

        public static string Goblin() => Split("goblin", "Goblin", 1.32f);

        // Proporções medidas na referência (fração da altura): pescoço, cintura, quadril, joelho, ombro, cotovelo, ponta da mão.
        const float Neck = 0.745f, Waist = 0.535f, Hip = 0.40f, Knee = 0.19f, ShoulderX = 0.17f, ElbowX = 0.30f, HandTipX = 0.66f;

        public static string Split(string folder, string name, float height)
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "arte", folder));
            var jsonPath = Path.Combine(root, folder + ".json");
            if (!File.Exists(jsonPath)) return "não achei " + jsonPath;
            var raw = JsonUtility.FromJson<Raw>(File.ReadAllText(jsonPath));
            int vc = raw.positions.Length / 3;
            var P = new Vector3[vc]; var N = new Vector3[vc]; var U = new Vector2[vc];
            // glTF (destro) → Unity (canhoto): x invertido; UV com origem embaixo.
            for (int i = 0; i < vc; i++)
            {
                P[i] = new Vector3(-raw.positions[i * 3], raw.positions[i * 3 + 1], raw.positions[i * 3 + 2]);
                N[i] = new Vector3(-raw.normals[i * 3], raw.normals[i * 3 + 1], raw.normals[i * 3 + 2]);
                U[i] = new Vector2(raw.uvs[i * 2], 1f - raw.uvs[i * 2 + 1]);
            }
            // Escala e chão: altura pedida, pés em y = 0, centro em x/z.
            var min = P[0]; var max = P[0];
            foreach (var p in P) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            float s = height / (max.y - min.y);
            var c = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
            for (int i = 0; i < vc; i++) P[i] = (P[i] - c) * s;
            float H = height;
            // Frente = lado para onde o nariz aponta (o ponto mais saliente no centro da cabeça). Queremos +z.
            float noseZ = 0f;
            foreach (var p in P) if (p.y > Neck * H && Mathf.Abs(p.x) < 0.05f * H && Mathf.Abs(p.z) > Mathf.Abs(noseZ)) noseZ = p.z;
            bool flip = noseZ < 0f;
            if (flip) for (int i = 0; i < vc; i++) { P[i] = new Vector3(-P[i].x, P[i].y, -P[i].z); N[i] = new Vector3(-N[i].x, N[i].y, -N[i].z); }

            // Linha dos braços e centro das pernas medidos no próprio modelo.
            float ArmY(float x0, float x1) { float sum = 0; int n = 0; foreach (var p in P) { float ax = Mathf.Abs(p.x); if (ax > x0 * H && ax < x1 * H && p.y > Waist * H && p.y < 0.8f * H) { sum += p.y; n++; } } return n > 0 ? sum / n : 0.7f * H; }
            float shoulderY = ArmY(ShoulderX + 0.03f, ShoulderX + 0.08f), elbowY = ArmY(ElbowX - 0.03f, ElbowX + 0.03f), handY = ArmY(HandTipX - 0.1f, HandTipX);
            float legX = 0f; { int n = 0; foreach (var p in P) if (p.y < Knee * H && p.y > 0.05f * H) { legX += Mathf.Abs(p.x); n++; } legX = n > 0 ? legX / n : 0.07f * H; }

            var maps = new Dictionary<RagBone, (Vector3 a, Vector3 b, Vector3 A, Vector3 B)>
            {
                [RagBone.Bacia] = (new Vector3(0, Hip * H, 0), new Vector3(0, Waist * H, 0), new Vector3(0, 0.47f, 0), new Vector3(0, 0.63f, 0)),
                [RagBone.Tronco] = (new Vector3(0, Waist * H, 0), new Vector3(0, Neck * H, 0), new Vector3(0, 0.63f, 0), new Vector3(0, 0.97f, 0)),
                [RagBone.Cabeca] = (new Vector3(0, Neck * H, 0), new Vector3(0, H, 0), new Vector3(0, 0.97f, 0), new Vector3(0, 1.32f, 0)),
            };
            foreach (int sd in new[] { -1, 1 })
            {
                bool r = sd > 0;
                maps[r ? RagBone.BracoD : RagBone.BracoE] = (new Vector3(sd * ShoulderX * H, shoulderY, 0), new Vector3(sd * ElbowX * H, elbowY, 0), new Vector3(sd * 0.23f, 0.93f, 0), new Vector3(sd * 0.23f, 0.69f, 0));
                maps[r ? RagBone.AntebracoD : RagBone.AntebracoE] = (new Vector3(sd * ElbowX * H, elbowY, 0), new Vector3(sd * HandTipX * H, handY, 0), new Vector3(sd * 0.23f, 0.69f, 0), new Vector3(sd * 0.23f, 0.36f, 0));
                maps[r ? RagBone.CoxaD : RagBone.CoxaE] = (new Vector3(sd * legX, Hip * H, 0), new Vector3(sd * legX, Knee * H, 0), new Vector3(sd * 0.1f, 0.5f, 0), new Vector3(sd * 0.1f, 0.28f, 0));
                maps[r ? RagBone.CanelaD : RagBone.CanelaE] = (new Vector3(sd * legX, Knee * H, 0), new Vector3(sd * legX, 0f, 0), new Vector3(sd * 0.1f, 0.28f, 0), new Vector3(sd * 0.1f, 0f, 0));
            }

            // Osso de cada triângulo pelo centro dele. (No corpo, +x = direita do goblin = RagBone ...D.)
            RagBone Classify(Vector3 p)
            {
                float ax = Mathf.Abs(p.x); bool right = p.x > 0f;
                if (p.y > 0.8f * H || (p.y > Neck * H && ax < 0.08f * H)) return RagBone.Cabeca;
                if (ax > ShoulderX * H && p.y > Waist * H) return ax < ElbowX * H ? (right ? RagBone.BracoD : RagBone.BracoE) : (right ? RagBone.AntebracoD : RagBone.AntebracoE);
                if (p.y > Waist * H) return RagBone.Tronco;
                if (p.y > Hip * H || (ax < 0.035f * H && p.y > 0.28f * H)) return RagBone.Bacia;   // tanga entre as pernas fica na bacia
                if (p.y > Knee * H) return right ? RagBone.CoxaD : RagBone.CoxaE;
                return right ? RagBone.CanelaD : RagBone.CanelaE;
            }

            var parts = new Dictionary<RagBone, (List<Vector3> v, List<Vector3> n, List<Vector2> u, List<int> t)>();
            var idx = raw.indices;
            for (int t = 0; t < idx.Length; t += 3)
            {
                // Inverte a ordem do triângulo (troca de mão do glTF para a Unity; a inversão dupla do giro não muda isso).
                int i0 = idx[t], i1 = idx[t + 2], i2 = idx[t + 1];
                var bone = Classify((P[i0] + P[i1] + P[i2]) / 3f);
                if (!parts.TryGetValue(bone, out var part)) parts[bone] = part = (new List<Vector3>(), new List<Vector3>(), new List<Vector2>(), new List<int>());
                var m = maps[bone];
                foreach (int i in new[] { i0, i1, i2 })
                {
                    part.v.Add(Fit(m.a, m.b, m.A, m.B, P[i], out var rot));
                    part.n.Add((rot * N[i]).normalized);
                    part.u.Add(U[i]);
                    part.t.Add(part.v.Count - 1);
                }
            }

            var dir = $"Assets/Arena/Resources/Models/{name}";
            Directory.CreateDirectory(Path.GetFullPath(dir));
            File.Copy(Path.Combine(root, folder + "_cor.png"), Path.GetFullPath($"{dir}/{name}_cor.png"), true);
            AssetDatabase.Refresh();
            var sb = new System.Text.StringBuilder();
            foreach (var kv in parts)
            {
                var mesh = new Mesh { name = $"{name}_{kv.Key}" };
                mesh.SetVertices(kv.Value.v); mesh.SetNormals(kv.Value.n); mesh.SetUVs(0, kv.Value.u); mesh.SetTriangles(kv.Value.t, 0);
                mesh.RecalculateBounds();
                mesh.Optimize();
                var path = $"{dir}/{name}_{kv.Key}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                sb.Append(kv.Key).Append(':').Append(kv.Value.t.Count / 3).Append(' ');
            }
            AssetDatabase.SaveAssets();
            return $"{name}: {idx.Length / 3} triângulos, girado={flip}, ombro y={shoulderY:F2} cotovelo y={elbowY:F2} mão y={handY:F2} perna x={legX:F2} | {sb}";
        }

        static Vector3 Fit(Vector3 a, Vector3 b, Vector3 A, Vector3 B, Vector3 v, out Quaternion rot)
        {
            var d = b - a; float len = d.magnitude; d /= len;
            var D = B - A; float LEN = D.magnitude; D /= LEN;
            rot = Quaternion.FromToRotation(d, D);
            var rel = v - a;
            float t = Vector3.Dot(rel, d);
            var perp = rel - d * t;
            return A + rot * (d * t * (LEN / len) + perp);
        }
    }
}
