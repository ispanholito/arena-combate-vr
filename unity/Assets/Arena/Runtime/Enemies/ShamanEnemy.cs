using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Goblin Xamã: fica a 6-11 m, ergue o cajado (aviso: a caveira brilha) e lança um orbe de magia lento que curva
    // atrás de você. Qualquer golpe da arma no orbe REBATE para onde você golpeou e ele explode nos inimigos.
    // O escudo só bloqueia. De perto ele foge.
    public sealed class ShamanEnemy : GoblinEnemy
    {
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        float castCd = 2f, castT = -1f, strafeT;
        int side = 1;
        Transform staffTip;

        protected override void BuildVisual()
        {
            base.BuildVisual();
            var old = new System.Collections.Generic.List<GameObject>();
            foreach (Transform c in arm) old.Add(c.gameObject);
            foreach (var go in old) DestroyImmediate(go);
            clubMat = ArenaUtil.Lit(new Color(0.35f, 0.22f, 0.12f));
            var staff = ArenaUtil.Prim(PrimitiveType.Cylinder, arm, new Vector3(0, 0.45f, 0), new Vector3(0.04f, 0.45f, 0.04f), clubMat).transform;
            staffTip = ArenaUtil.Prim(PrimitiveType.Sphere, arm, new Vector3(0, 0.95f, 0), Vector3.one * 0.13f,
                                      ArenaUtil.Lit(new Color(0.85f, 0.85f, 0.75f), new Color(0.3f, 0.6f, 0.2f))).transform;
            // Capuz e penas: dá para reconhecer de longe.
            ArenaUtil.Prim(PrimitiveType.Sphere, model, new Vector3(0, 1.0f, -0.02f), new Vector3(0.36f, 0.3f, 0.36f), ArenaUtil.Lit(new Color(0.35f, 0.15f, 0.45f)));
            ArenaUtil.Prim(PrimitiveType.Cube, model, new Vector3(0.08f, 1.2f, -0.05f), new Vector3(0.03f, 0.18f, 0.02f), ArenaUtil.Lit(new Color(0.3f, 0.8f, 0.4f)));
            side = Random.value < 0.5f ? -1 : 1;
        }

        protected override void Think(float dt, Vector3 toPlayer, float d)
        {
            if (State.Stun > 0f || !State.CanAct) { castT = -1f; base.Think(dt, toPlayer, d); return; }
            model.localRotation = Quaternion.identity;
            FacePlayer(toPlayer, dt, 7f);
            var dir = d > 1e-3f ? toPlayer / d : transform.forward;
            castCd -= dt; strafeT -= dt;
            if (strafeT <= 0f) { strafeT = Random.Range(1.4f, 2.6f); side = -side; }
            if (castT >= 0f)
            {
                castT += dt;
                SetArm(Mathf.Lerp(-30f, -150f, Mathf.Min(1f, castT / Def.Windup)));
                if (castT >= Def.Windup) { Cast(); castT = -1f; castCd = MagicOrbRules.Cooldown + Random.Range(0f, 1f); SetState(AiState.Approach); }
                return;
            }
            var lateral = new Vector3(-dir.z, 0, dir.x) * side;
            Vector3 want = d < MagicOrbRules.PreferredMin ? -dir * Def.MoveSpeed * 1.1f + lateral * 0.5f
                         : d > MagicOrbRules.PreferredMax ? dir * Def.MoveSpeed
                         : lateral * Def.MoveSpeed * 0.6f;
            Move(want, dt);
            LerpArm(-30f, 5f, dt);
            if (castCd <= 0f && d < 16f && d > 2f && State.CanAct) { castT = 0f; SetState(AiState.Windup); }
        }

        void Cast()
        {
            var pl = Player;
            if (!pl || !staffTip) return;
            // Sai sempre da altura do peito para cima e na frente dele: com o corpo inclinado (tranco ao apanhar) a ponta do
            // cajado podia ficar perto do chão e o orbe nascia "dentro" do piso e estourava na hora ("atirando no chão").
            var from = staffTip.position + transform.forward * 0.2f;
            from.y = Mathf.Max(from.y, transform.position.y + 1.1f);
            var aim = pl.HeadPosition - Vector3.up * 0.3f;
            if (Vector3.Angle(transform.forward, Flat(aim - from)) > 40f) return;   // não estava virado para você: não lança
            MagicOrb.Spawn(from, (aim - from).normalized * MagicOrbRules.Speed, Def.AttackDamage, this);
            ArenaAudio.Play(Sfx.Skill, from, 0.7f, 0.7f);
        }
    }

    // Orbe de magia: sem gravidade, curva devagar na direção do jogador. Rebatido pela arma, procura um inimigo no cone
    // do golpe e explode.
    public sealed class MagicOrb : MonoBehaviour
    {
        Vector3 vel, prev;
        float damage, life = MagicOrbRules.Life;
        bool reflected, dead, passed;
        ArenaEnemy shooter, target;
        static readonly Color Col = new Color(0.55f, 1f, 0.35f);

        public static MagicOrb Spawn(Vector3 pos, Vector3 velocity, float damage, ArenaEnemy shooter)
        {
            var go = new GameObject("OrbeMagico");
            go.transform.position = pos;
            ArenaUtil.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * MagicOrbRules.Radius * 2f, ArenaUtil.Unlit(Col));
            ArenaUtil.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * MagicOrbRules.Radius * 3f, ArenaUtil.Unlit(new Color(Col.r, Col.g, Col.b, 0.3f)));
            var o = go.AddComponent<MagicOrb>();
            o.vel = velocity; o.prev = pos; o.damage = damage; o.shooter = shooter;
            return o;
        }

        void Update()
        {
            if (dead || ArenaGame.Paused) return;
            float dt = Time.deltaTime;
            life -= dt;
            var game = ArenaGame.Instance;
            // Curva: atrás do jogador (normal) ou do alvo escolhido (rebatido).
            Vector3? aim = reflected ? (target && !target.Dead ? target.Center : (Vector3?)null)
                                     : game ? game.Player.HeadPosition - Vector3.up * 0.3f : (Vector3?)null;
            // Passou do jogador (errou): para de curvar e segue reto. Antes ficava dando a volta e mergulhava no chão.
            if (!reflected && aim.HasValue && Vector3.Dot(vel, aim.Value - transform.position) < 0f) passed = true;
            if (aim.HasValue && !(passed && !reflected))
            {
                float turn = (reflected ? 180f : MagicOrbRules.TurnDegPerSec) * Mathf.Deg2Rad * dt;
                vel = Vector3.RotateTowards(vel, (aim.Value - transform.position).normalized * vel.magnitude, turn, 0f);
            }
            var now = prev + vel * dt;
            transform.position = now;
            transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 18f) * 0.12f);
            if (reflected) CheckEnemies(now); else CheckPlayer(now);
            if (!dead && (life <= 0f || now.y < 0.05f || ArenaUtil.IsOutside(now, 0.2f))) Pop(now, false);
            prev = now;
        }

        void CheckPlayer(Vector3 now)
        {
            var pl = ArenaGame.Instance ? ArenaGame.Instance.Player : null;
            if (!pl) return;
            var w = pl.Weapon;
            if (pl.Shield && pl.Shield.Equipped && pl.Shield.Intercepts(prev, prev, now, now, 0.1f))
            {
                ArenaAudio.Play(Sfx.Block, now, 0.8f, 0.9f);
                ArenaInput.Haptic(0.5f, 0.06f, left: true);
                Pop(now, false);
                return;
            }
            if (w != null && w.Held && Geometry.SweptSegmentsHit(w.BasePrev.ToN(), w.TipPrev.ToN(), w.BasePoint.ToN(), w.TipPoint.ToN(),
                                                                  prev.ToN(), prev.ToN(), now.ToN(), now.ToN(), MagicOrbRules.DeflectRadius))
            {
                if (w.TipSpeed >= MagicOrbRules.DeflectSpeed || (!pl.IsVr && pl.Input.DesktopParry)) { Reflect(now, pl); return; }
            }
            // Desktop: segurar o aparo olhando para o orbe rebate.
            if (!pl.IsVr && pl.Input.DesktopParry && (now - pl.HeadPosition).magnitude < 1.4f &&
                Vector3.Dot(pl.HeadForward, (now - pl.HeadPosition).normalized) > 0.5f) { Reflect(now, pl); return; }
            var head = pl.HeadPosition;
            var waist = new Vector3(head.x, pl.FeetPosition.y + 0.6f, head.z);
            if (Geometry.SweptSegmentsHit(waist.ToN(), head.ToN(), waist.ToN(), head.ToN(), prev.ToN(), prev.ToN(), now.ToN(), now.ToN(), 0.22f + MagicOrbRules.Radius))
            {
                pl.TakeDamage(damage, now - vel.normalized);
                Pop(now, false);
            }
        }

        void Reflect(Vector3 at, ArenaPlayer pl)
        {
            reflected = true;
            life = 4f;
            // Direção do golpe (VR) ou do olhar (desktop), puxada para a frente do jogador.
            var swing = pl.IsVr && pl.Weapon.TipSpeed > 0.5f ? pl.Weapon.TipVelocity.normalized : pl.HeadForward;
            var dir = (swing + pl.HeadForward).normalized;
            target = null; float best = MagicOrbRules.AimConeDeg;
            foreach (var e in ArenaEnemy.All)
            {
                if (!e || e.Dead) continue;
                float ang = Vector3.Angle(dir, e.Center - at);
                if (ang < best) { best = ang; target = e; }
            }
            if (!target && shooter && !shooter.Dead) target = shooter;
            vel = (target ? (target.Center - at).normalized : dir) * MagicOrbRules.ReflectSpeed;
            ArenaAudio.Play(Sfx.Parry, at, 1f, 1.1f);
            ArenaFx.FloatText("REBATEU!", at + Vector3.up * 0.2f, Col, 1.1f);
            ArenaFx.Burst(at, Col, 14, 3f);
            ArenaInput.Haptic(0.9f, 0.08f);
            TutorialEvents.Raise("rebater");
        }

        void CheckEnemies(Vector3 now)
        {
            foreach (var e in ArenaEnemy.All)
            {
                if (!e || e.Dead) continue;
                e.BodyAxis(out var a, out var b);
                if (Geometry.SweptSegmentsHit(a.ToN(), b.ToN(), a.ToN(), b.ToN(), prev.ToN(), prev.ToN(), now.ToN(), now.ToN(), e.Radius + MagicOrbRules.Radius))
                { Pop(now, true); return; }
            }
        }

        // Rebatido: explode em área nos inimigos. Normal: só some com um brilho.
        void Pop(Vector3 at, bool boom)
        {
            dead = true;
            ArenaFx.Burst(at, Col, boom ? 26 : 10, boom ? 4f : 2f);
            if (boom)
            {
                ArenaFx.Ring(at, Col, MagicOrbRules.ReflectRadius);
                ArenaAudio.Play(Sfx.Explosion, at, 0.8f, 1.3f);
                foreach (var e in ArenaEnemy.All.ToArray())
                {
                    if (!e || e.Dead) continue;
                    float d = Vector3.Distance(e.Center, at);
                    if (d < MagicOrbRules.ReflectRadius + e.Radius)
                        e.ApplyDamage(MagicOrbRules.ReflectedDamage * Combat.ExplosionFalloff(d, MagicOrbRules.ReflectRadius) * (e is LichBoss ? LichRules.ReflectedOrbBonus : 1f), 0.8f, e.Center - at, 3f, Col);
                }
            }
            Destroy(gameObject);
        }
    }
}
