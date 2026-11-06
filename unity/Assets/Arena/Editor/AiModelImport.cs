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

        // Goblin v3 (2026-11-06, Hunyuan3D a partir de arte/goblin/goblin_ref_v3_2.png): proporções medidas no próprio
        // modelo (tools: largura por altura e coluna central). Cabeça ×0,85: o Gemini sempre faz cabeça de goblin grande.
        public static string Goblin() => Split("goblin", "Goblin", 1.32f, GoblinV3);

        // Proporções (fração da altura): pescoço, cintura (cinto), quadril (virilha), joelho, ombro, cotovelo, ponta da mão,
        // fim da tanga entre as pernas, escala da cabeça (em volta do pescoço).
        public struct Proportions { public float Neck, Waist, Hip, Knee, ShoulderX, ElbowX, HandTipX, ClothBottom, HeadScale, JawY, JawZ; public bool HeadAnyX; }
        public static readonly Proportions GoblinV1 = new Proportions { Neck = 0.745f, Waist = 0.535f, Hip = 0.40f, Knee = 0.19f, ShoulderX = 0.17f, ElbowX = 0.30f, HandTipX = 0.66f, ClothBottom = 0.28f, HeadScale = 1f };
        public static readonly Proportions GoblinV3 = new Proportions { Neck = 0.795f, Waist = 0.49f, Hip = 0.37f, Knee = 0.19f, ShoulderX = 0.16f, ElbowX = 0.37f, HandTipX = 0.69f, ClothBottom = 0.26f, HeadScale = 0.85f, HeadAnyX = true, JawY = 0.69f, JawZ = 0.05f };

        public static string Split(string folder, string name, float height, Proportions pr)
        {
            float Neck = pr.Neck, Waist = pr.Waist, Hip = pr.Hip, Knee = pr.Knee, ShoulderX = pr.ShoulderX, ElbowX = pr.ElbowX, HandTipX = pr.HandTipX;
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
            // Profundidade (z) do eixo do braço, medida no meio do braço: perto do corpo só é braço o que está em volta desse
            // eixo (as costas/escápula na mesma altura ficam no tronco).
            float armZ = 0f; { int n = 0; foreach (var p in P) { float ax = Mathf.Abs(p.x); if (ax > 0.3f * H && ax < 0.45f * H && p.y > Waist * H) { armZ += p.z; n++; } } armZ = n > 0 ? armZ / n : 0f; }
            float legX = 0f; { int n = 0; foreach (var p in P) if (p.y < Knee * H && p.y > 0.05f * H) { legX += Mathf.Abs(p.x); n++; } legX = n > 0 ? legX / n : 0.07f * H; }

            var maps = new Dictionary<RagBone, (Vector3 a, Vector3 b, Vector3 A, Vector3 B)>
            {
                [RagBone.Bacia] = (new Vector3(0, Hip * H, 0), new Vector3(0, Waist * H, 0), new Vector3(0, 0.47f, 0), new Vector3(0, 0.63f, 0)),
                [RagBone.Tronco] = (new Vector3(0, Waist * H, 0), new Vector3(0, Neck * H, 0), new Vector3(0, 0.63f, 0), new Vector3(0, 0.97f, 0)),
                [RagBone.Cabeca] = (new Vector3(0, Neck * H, 0), new Vector3(0, H, 0), new Vector3(0, 0.97f, 0), new Vector3(0, 0.97f + (1f - Neck) * H, 0)),   // sem esticar: a cabeça mantém a altura do modelo
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
                if (p.y > Neck * H && (pr.HeadAnyX || ax < 0.08f * H || p.y > 0.8f * H)) return RagBone.Cabeca;   // HeadAnyX: braços bem abaixo do pescoço
                // Goblin corcunda: o queixo desce na frente do peito, abaixo da linha do pescoço.
                if (pr.JawY > 0f && p.y > pr.JawY * H && p.z > pr.JawZ * H && ax < 0.1f * H) return RagBone.Cabeca;
                if (ax > ShoulderX * H && p.y > Waist * H && (ax > 0.2f * H || new Vector2(p.y - shoulderY, p.z - armZ).magnitude < 0.08f * H)) return ax < ElbowX * H ? (right ? RagBone.BracoD : RagBone.BracoE) : (right ? RagBone.AntebracoD : RagBone.AntebracoE);
                if (p.y > Waist * H) return RagBone.Tronco;
                if (p.y > Hip * H || (ax < 0.035f * H && p.y > pr.ClothBottom * H)) return RagBone.Bacia;   // tanga entre as pernas fica na bacia
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
                    var fitted = Fit(m.a, m.b, m.A, m.B, P[i], out var rot);
                    if (bone == RagBone.Cabeca && pr.HeadScale != 1f) fitted = m.A + (fitted - m.A) * pr.HeadScale;   // cabeça menor, presa no pescoço
                    part.v.Add(fitted);
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

            // ---------- Pele contínua (2026-11-06): uma malha só, em pose T, com pesos por osso ----------
            // As peças rígidas acima continuam (inimigo antigo, membro arrancado e medida do capacete). A pele dobra nas
            // juntas: perto de cada junta o vértice se divide entre os dois ossos. mesh.bindposes guarda, por osso, a
            // matriz F que leva a pose T à pose de repouso do corpo (a mesma do Fit); o RagdollGoblin.DressModel combina F
            // com a posição de repouso do osso. Submalha i = triângulos do osso i (RagBone 0..10), para esconder o que for
            // arrancado.
            float ZAt(float y) { float s2 = 0; int n2 = 0; foreach (var p in P) if (Mathf.Abs(p.x) < 0.06f * H && Mathf.Abs(p.y - y) < 0.03f * H) { s2 += p.z; n2++; } return n2 > 0 ? s2 / n2 : 0f; }
            var joints = new List<(RagBone parent, RagBone child, Vector3 J, Vector3 d, float R, float gate)>();
            var waistJ = new Vector3(0, Waist * H, ZAt(Waist * H));
            var neckJ = new Vector3(0, Neck * H, ZAt(Neck * H));
            joints.Add((RagBone.Bacia, RagBone.Tronco, waistJ, Vector3.up, 0.05f * H, 0.2f * H));
            joints.Add((RagBone.Tronco, RagBone.Cabeca, neckJ, Vector3.up, 0.035f * H, 0.11f * H));
            foreach (int sd in new[] { -1, 1 })
            {
                bool r = sd > 0;
                var sh = new Vector3(sd * ShoulderX * H, shoulderY, ZAt(shoulderY));
                var el = new Vector3(sd * ElbowX * H, elbowY, sh.z);
                var hp = new Vector3(sd * legX, Hip * H, ZAt(Hip * H));
                var kn = new Vector3(sd * legX, Knee * H, hp.z);
                joints.Add((RagBone.Tronco, r ? RagBone.BracoD : RagBone.BracoE, sh, (el - sh).normalized, 0.05f * H, 0.13f * H));
                joints.Add((r ? RagBone.BracoD : RagBone.BracoE, r ? RagBone.AntebracoD : RagBone.AntebracoE, el, (new Vector3(sd * HandTipX * H, handY, el.z) - el).normalized, 0.035f * H, 0.08f * H));
                joints.Add((RagBone.Bacia, r ? RagBone.CoxaD : RagBone.CoxaE, hp, Vector3.down, 0.05f * H, 0.12f * H));
                joints.Add((r ? RagBone.CoxaD : RagBone.CoxaE, r ? RagBone.CanelaD : RagBone.CanelaE, kn, Vector3.down, 0.035f * H, 0.08f * H));
            }
            var weights = new BoneWeight[vc];
            for (int i = 0; i < vc; i++)
            {
                var p = P[i];
                var prim = Classify(p);
                weights[i] = new BoneWeight { boneIndex0 = (int)prim, weight0 = 1f };
                float bestOther = 0f;
                foreach (var j in joints)
                {
                    if (prim != j.parent && prim != j.child) continue;
                    if ((p - j.J).magnitude > j.gate) continue;
                    float t = Vector3.Dot(p - j.J, j.d) / j.R;   // >0 = lado do filho
                    float wChild = prim == j.child ? 0.5f + 0.5f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)) : 0.5f - 0.5f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-t));
                    float wOther = prim == j.child ? 1f - wChild : wChild;
                    // Longe da junta (corcunda acima do ombro, axila) a divisão some: senão a pele estica como capa.
                    wOther *= 1f - Mathf.SmoothStep(0f, 1f, (p - j.J).magnitude / j.gate);
                    if (wOther <= bestOther) continue;
                    bestOther = wOther;
                    var other = prim == j.child ? j.parent : j.child;
                    weights[i] = new BoneWeight { boneIndex0 = (int)prim, weight0 = 1f - wOther, boneIndex1 = (int)other, weight1 = wOther };
                }
            }
            var subs = new List<int>[11];
            for (int s2 = 0; s2 < 11; s2++) subs[s2] = new List<int>();
            for (int t = 0; t < idx.Length; t += 3)
            {
                int i0 = idx[t], i1 = idx[t + 2], i2 = idx[t + 1];
                var bone = Classify((P[i0] + P[i1] + P[i2]) / 3f);
                subs[(int)bone].Add(i0); subs[(int)bone].Add(i1); subs[(int)bone].Add(i2);
            }
            var F = new Matrix4x4[11];
            for (int bi = 0; bi < 11; bi++)
            {
                var m = maps[(RagBone)bi];
                var d = (m.b - m.a); float len = d.magnitude; d /= len;
                var D = (m.B - m.A); float LEN = D.magnitude; D /= LEN;
                var R = Matrix4x4.Rotate(Quaternion.FromToRotation(d, D));
                // Escala só ao longo do osso: S = I + (s - 1)·d·dᵀ.
                float sc = LEN / len - 1f;
                var S = Matrix4x4.identity;
                for (int r0 = 0; r0 < 3; r0++) for (int c0 = 0; c0 < 3; c0++) S[r0, c0] += sc * d[r0] * d[c0];
                var M = Matrix4x4.Translate(m.A) * R * S * Matrix4x4.Translate(-m.a);
                if ((RagBone)bi == RagBone.Cabeca && pr.HeadScale != 1f) M = Matrix4x4.Translate(m.A) * Matrix4x4.Scale(Vector3.one * pr.HeadScale) * Matrix4x4.Translate(-m.A) * M;
                F[bi] = M;
            }
            {
                var skinMesh = new Mesh { name = $"{name}_Skin" };
                skinMesh.SetVertices(P); skinMesh.SetNormals(N); skinMesh.SetUVs(0, U);
                skinMesh.subMeshCount = 11;
                for (int s2 = 0; s2 < 11; s2++) skinMesh.SetTriangles(subs[s2], s2);
                skinMesh.boneWeights = weights;
                skinMesh.bindposes = F;
                skinMesh.RecalculateBounds();
                var path = $"{dir}/{name}_Skin.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(skinMesh, path);
                int blended = 0; foreach (var w in weights) if (w.weight1 > 0.01f) blended++;
                sb.Append("| pele: ").Append(vc).Append(" vértices, ").Append(blended).Append(" divididos entre 2 ossos");
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
