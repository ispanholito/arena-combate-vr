using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Esqueleto: luta como o goblin (ataque anunciado, aparável), mas com espada. Cortes fortes arrancam cabeça,
    // braços e pernas (SkeletonBody); as peças caem com física e voltam a se encaixar em 4 s, a menos que sejam
    // quebradas com um golpe no chão.
    public sealed class SkeletonEnemy : GoblinEnemy
    {
        readonly SkeletonBody body = new SkeletonBody();
        readonly Dictionary<Limb, Transform> limbs = new Dictionary<Limb, Transform>();
        readonly Dictionary<Limb, (Vector3 pos, Quaternion rot)> sockets = new Dictionary<Limb, (Vector3, Quaternion)>();
        readonly Dictionary<Limb, BonePiece> fallen = new Dictionary<Limb, BonePiece>();
        Material bone, dark;

        protected override bool CanAttackNow => body.CanAttack;
        protected override float TurnSpeed => 10f * body.TurnMultiplier;
        protected override float WindupTime => windup * body.WindupMultiplier;
        protected override float MoveMultiplier => body.SpeedMultiplier;

        protected override void BuildVisual()
        {
            bloodColor = new Color(0.85f, 0.85f, 0.78f);
            bodyBottom = 0.2f; bodyTop = 1.3f;
            bone = ArenaUtil.Lit(new Color(0.86f, 0.84f, 0.76f));
            dark = ArenaUtil.Lit(new Color(0.05f, 0.03f, 0.03f), new Color(0.4f, 0.05f, 0.02f));
            clubColor = new Color(0.55f, 0.57f, 0.6f);

            // Tronco (fica sempre): bacia, coluna e costelas.
            ArenaUtil.Prim(PrimitiveType.Cube, model, new Vector3(0, 0.76f, 0), new Vector3(0.3f, 0.08f, 0.14f), bone);
            ArenaUtil.Prim(PrimitiveType.Cylinder, model, new Vector3(0, 0.98f, 0), new Vector3(0.05f, 0.22f, 0.05f), bone);
            for (int i = 0; i < 4; i++)
                ArenaUtil.Prim(PrimitiveType.Cube, model, new Vector3(0, 0.95f + i * 0.07f, 0.02f), new Vector3(0.34f - i * 0.03f, 0.025f, 0.16f), bone);

            // Cabeça
            var head = Group("Cabeca", new Vector3(0, 1.2f, 0));
            ArenaUtil.Prim(PrimitiveType.Sphere, head, new Vector3(0, 0.1f, 0), new Vector3(0.2f, 0.22f, 0.22f), bone);
            foreach (int s in new[] { -1, 1 })
                ArenaUtil.Prim(PrimitiveType.Sphere, head, new Vector3(s * 0.05f, 0.12f, 0.09f), Vector3.one * 0.06f, dark);
            ArenaUtil.Prim(PrimitiveType.Cube, head, new Vector3(0, 0.0f, 0.06f), new Vector3(0.12f, 0.04f, 0.08f), bone);
            Register(Limb.Cabeca, head);

            // Braço direito com a espada: é o `arm` da IA (arma ao longo de +Y, de 0,1 a 0,88).
            arm = Group("BracoDireito", new Vector3(0.2f, 1.12f, 0f));
            ArenaUtil.Prim(PrimitiveType.Cylinder, arm, new Vector3(0, 0.08f, 0), new Vector3(0.04f, 0.1f, 0.04f), bone);
            clubMat = ArenaUtil.Lit(clubColor, new Color(0.05f, 0.05f, 0.06f));
            ArenaUtil.Prim(PrimitiveType.Cube, arm, new Vector3(0, 0.5f, 0), new Vector3(0.05f, 0.7f, 0.012f), clubMat);
            ArenaUtil.Prim(PrimitiveType.Cube, arm, new Vector3(0, 0.17f, 0), new Vector3(0.14f, 0.025f, 0.03f), ArenaUtil.Lit(new Color(0.35f, 0.25f, 0.12f)));
            Register(Limb.BracoDireito, arm);

            var armL = Group("BracoEsquerdo", new Vector3(-0.2f, 1.12f, 0f));
            ArenaUtil.Prim(PrimitiveType.Cylinder, armL, new Vector3(0, -0.2f, 0), new Vector3(0.04f, 0.2f, 0.04f), bone);
            ArenaUtil.Prim(PrimitiveType.Cylinder, armL, new Vector3(0, -0.55f, 0.04f), new Vector3(0.035f, 0.17f, 0.035f), bone);
            Register(Limb.BracoEsquerdo, armL);

            foreach (var (limb, x) in new[] { (Limb.PernaDireita, 0.1f), (Limb.PernaEsquerda, -0.1f) })
            {
                var leg = Group(limb.ToString(), new Vector3(x, 0.74f, 0f));
                ArenaUtil.Prim(PrimitiveType.Cylinder, leg, new Vector3(0, -0.19f, 0), new Vector3(0.05f, 0.19f, 0.05f), bone);
                ArenaUtil.Prim(PrimitiveType.Cylinder, leg, new Vector3(0, -0.56f, 0), new Vector3(0.045f, 0.18f, 0.045f), bone);
                ArenaUtil.Prim(PrimitiveType.Cube, leg, new Vector3(0, -0.73f, 0.05f), new Vector3(0.08f, 0.03f, 0.16f), bone);
                Register(limb, leg);
            }
        }

        Transform Group(string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(model, false);
            t.localPosition = localPos;
            return t;
        }

        void Register(Limb l, Transform t)
        {
            limbs[l] = t;
            sockets[l] = (t.localPosition, t.localRotation);
        }

        // ---------- Desmembrar ----------
        public override void OnMeleeHitAt(Vector3 point, float speed, float edge, Vector3 velocity, WeaponDef weapon)
        {
            if (Dead || !SkeletonBody.CanSever(weapon.Kind, speed, edge)) return;
            float side = transform.InverseTransformPoint(point).x;
            var limb = SkeletonBody.LimbAt(HeightFraction(point), side);
            if (!body.Sever(limb)) return;
            var t = limbs[limb];
            t.SetParent(null, true);
            var piece = t.gameObject.AddComponent<BonePiece>();
            piece.Init(this, limb, velocity.normalized * 2.5f + Vector3.up * 2f);
            fallen[limb] = piece;
            ArenaAudio.Play(Sfx.Bone, point);
            ArenaFx.Burst(point, bloodColor, 12, 3f);
            ArenaFx.FloatText(limb switch
            {
                Limb.Cabeca => "DECAPITADO!",
                Limb.BracoDireito => "DESARMADO!",
                _ => "DESMEMBRADO!",
            }, Center + Vector3.up * 0.7f, new Color(1f, 0.95f, 0.8f), 1.1f);
            if (limb == Limb.BracoDireito && (state == AiState.Windup || state == AiState.Strike)) SetState(AiState.Recover);
            UpdatePosture();
        }

        // Peça caída quebrada pela arma: não volta mais.
        public void OnPieceBroken(Limb l)
        {
            if (!body.Break(l)) return;
            fallen.Remove(l);
            limbs.Remove(l);
        }

        protected override void Update()
        {
            base.Update();
            if (Dead) return;
            foreach (var l in body.Tick(Time.deltaTime))
            {
                if (fallen.TryGetValue(l, out var piece) && piece)
                {
                    var (pos, rot) = sockets[l];
                    piece.ReturnTo(model, pos, rot);
                    ArenaAudio.Play(Sfx.Rattle, piece.transform.position, 0.8f);
                }
                fallen.Remove(l);
            }
            UpdatePosture();
        }

        // Sem pernas, o corpo fica rente ao chão.
        void UpdatePosture()
        {
            float drop = body.SpeedMultiplier >= 1f ? 0f : body.SpeedMultiplier > 0.3f ? 0.12f : 0.62f;
            var p = model.localPosition;
            p.y = Mathf.MoveTowards(p.y, -drop, Time.deltaTime * 2f);
            model.localPosition = p;
        }

        protected override void Die()
        {
            // As peças caídas ficam um pouco no chão e somem.
            foreach (var p in fallen.Values) if (p) Destroy(p.gameObject, 3f);
            fallen.Clear();
            base.Die();
        }

        protected override void OnDestroy()
        {
            foreach (var p in fallen.Values) if (p) Destroy(p.gameObject);
            base.OnDestroy();
        }
    }

    // Parte do esqueleto caída no chão: física, pode ser quebrada pela arma, e volta voando para o encaixe.
    public sealed class BonePiece : MonoBehaviour
    {
        SkeletonEnemy owner;
        Limb limb;
        Rigidbody rb;
        Transform returnParent;
        Vector3 returnPos, startPos;
        Quaternion returnRot, startRot;
        float returnT = -1f;

        public void Init(SkeletonEnemy o, Limb l, Vector3 velocity)
        {
            owner = o; limb = l;
            // Colisor do tamanho das partes.
            var rends = GetComponentsInChildren<Renderer>();
            var b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in rends) b.Encapsulate(r.bounds);
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = transform.InverseTransformPoint(b.center);
            col.size = Vector3.Max(transform.InverseTransformVector(b.size).Abs(), Vector3.one * 0.05f);
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * 6f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        void OnCollisionEnter(Collision c)
        {
            if (returnT >= 0f || !owner) return;
            if (c.rigidbody && c.rigidbody.GetComponent<WeaponImpactRelay>() && c.relativeVelocity.magnitude > SkeletonBody.BreakSpeed)
            {
                owner.OnPieceBroken(limb);
                ArenaAudio.Play(Sfx.Bone, transform.position, 1f, 0.8f);
                ArenaFx.Burst(transform.position, new Color(0.86f, 0.84f, 0.76f), 18, 3f);
                Destroy(gameObject);
            }
        }

        public void ReturnTo(Transform parent, Vector3 localPos, Quaternion localRot)
        {
            returnParent = parent; returnPos = localPos; returnRot = localRot;
            startPos = transform.position; startRot = transform.rotation;
            returnT = 0f;
            if (rb) Destroy(rb);
            var col = GetComponent<Collider>();
            if (col) Destroy(col);
        }

        void Update()
        {
            if (returnT < 0f) return;
            if (!returnParent) { Destroy(gameObject); return; }
            returnT += Time.deltaTime / 0.35f;
            var target = returnParent.TransformPoint(returnPos);
            var targetRot = returnParent.rotation * returnRot;
            float k = Mathf.SmoothStep(0f, 1f, returnT);
            transform.SetPositionAndRotation(Vector3.Lerp(startPos, target, k), Quaternion.Slerp(startRot, targetRot, k));
            if (returnT >= 1f)
            {
                transform.SetParent(returnParent, false);
                transform.localPosition = returnPos; transform.localRotation = returnRot;
                Destroy(this);
            }
        }
    }

    static class VecExt
    {
        public static Vector3 Abs(this Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
