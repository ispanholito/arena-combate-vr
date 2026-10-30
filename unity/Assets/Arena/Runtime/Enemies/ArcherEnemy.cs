using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Goblin arqueiro: mantém 5-9 m de distância, anda de lado e atira flechas com aviso (o arco brilha ao puxar).
    // A flecha é bloqueada pelo escudo e rebatida por um golpe da arma (volta e acerta inimigos). De perto foge.
    public sealed class ArcherEnemy : GoblinEnemy
    {
        float shootCd = 1.5f, drawT = -1f, strafeT;
        int side = 1;
        Transform bow;

        protected override void BuildVisual()
        {
            base.BuildVisual();
            // Troca a clava por um arco (o braço continua sendo o `arm`, para o brilho de aviso).
            var old = new System.Collections.Generic.List<GameObject>();
            foreach (Transform c in arm) if (c.name != ArmModelName) old.Add(c.gameObject);
            foreach (var go in old) DestroyImmediate(go);   // já: os renderers do inimigo são lidos logo depois
            bow = new GameObject("Arco").transform;
            bow.SetParent(arm, false);
            bow.localPosition = new Vector3(0, 0.45f + HandY * 0.4f, 0);
            clubMat = ArenaUtil.Lit(new Color(0.45f, 0.3f, 0.15f));
            for (int i = -2; i <= 2; i++)   // arco curvo em 5 segmentos
            {
                var seg = ArenaUtil.Prim(PrimitiveType.Cube, bow, new Vector3(0, i * 0.11f, Mathf.Abs(i) * -0.035f), new Vector3(0.035f, 0.12f, 0.035f), clubMat).transform;
                seg.localRotation = Quaternion.Euler(i * 14f, 0, 0);
            }
            ArenaUtil.Prim(PrimitiveType.Cube, bow, new Vector3(0, 0, -0.08f), new Vector3(0.008f, 0.5f, 0.008f), ArenaUtil.Unlit(new Color(0.9f, 0.9f, 0.85f)));
            // Aljava nas costas.
            ArenaUtil.Prim(PrimitiveType.Cylinder, model, new Vector3(-0.12f, 0.75f, -0.22f), new Vector3(0.1f, 0.2f, 0.1f), ArenaUtil.Lit(new Color(0.3f, 0.2f, 0.12f)));
            side = Random.value < 0.5f ? -1 : 1;
        }

        protected override void Think(float dt, Vector3 toPlayer, float d)
        {
            if (State.Stun > 0f) { drawT = -1f; base.Think(dt, toPlayer, d); return; }
            model.localRotation = Quaternion.identity;
            FacePlayer(toPlayer, dt, 8f);
            var dir = d > 1e-3f ? toPlayer / d : transform.forward;
            shootCd -= dt;
            strafeT -= dt;
            if (strafeT <= 0f) { strafeT = Random.Range(1.2f, 2.4f); side = -side; }

            if (drawT >= 0f)
            {
                // Puxando o arco: parado, mira na cabeça.
                drawT += dt;
                SetArm(Mathf.Lerp(-60f, -90f, Mathf.Min(1f, drawT / Def.Windup)));
                if (drawT >= Def.Windup) { Shoot(); drawT = -1f; shootCd = ArrowRules.Cooldown + Random.Range(0f, 0.8f); SetState(AiState.Approach); }
                return;
            }
            var lateral = new Vector3(-dir.z, 0, dir.x) * side;
            Vector3 want = d < ArrowRules.PreferredMin ? -dir * Def.MoveSpeed * 1.1f + lateral * 0.5f
                         : d > ArrowRules.PreferredMax ? dir * Def.MoveSpeed
                         : lateral * Def.MoveSpeed * 0.6f;
            Move(want, dt);
            LerpArm(-40f, 5f, dt);
            if (shootCd <= 0f && d < 14f && d > 1.6f && State.CanAct)
            {
                drawT = 0f;
                SetState(AiState.Windup);   // brilho de aviso no arco
            }
        }

        void Shoot()
        {
            var pl = Player;
            if (!pl) return;
            var from = bow.position + transform.forward * 0.15f;
            var target = pl.HeadPosition - Vector3.up * 0.25f;
            var flat = (target - from).Flat();
            float h = flat.magnitude;
            float lob = ArrowRules.LobAngle(h, ArrowRules.Speed);
            var aim = Quaternion.AngleAxis(-lob * Mathf.Rad2Deg, Vector3.Cross(Vector3.up, flat.normalized)) * (target - from).normalized;
            Arrow.Spawn(from, aim * ArrowRules.Speed, Def.AttackDamage, this);
            ArenaAudio.Play(Sfx.Whoosh, from, 0.7f, 1.6f);
        }
    }

    // Flecha: voa com gravidade e testa o caminho entre quadros contra escudo, arma e corpo do jogador.
    // Rebatida pela arma, volta contra o atirador e machuca inimigos.
    public sealed class Arrow : MonoBehaviour
    {
        Vector3 vel, prev;
        float damage, life = 4f;
        bool reflected, dead;
        ArenaEnemy shooter;

        public static Arrow Spawn(Vector3 pos, Vector3 velocity, float damage, ArenaEnemy shooter)
        {
            var go = new GameObject("Flecha");
            go.transform.position = pos;
            ArenaUtil.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0, -0.25f), new Vector3(0.02f, 0.02f, 0.6f), ArenaUtil.Lit(new Color(0.5f, 0.35f, 0.2f)));
            ArenaUtil.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0, 0.06f), new Vector3(0.04f, 0.04f, 0.08f), ArenaUtil.Lit(new Color(0.6f, 0.6f, 0.65f)));
            ArenaUtil.Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 0, -0.52f), new Vector3(0.08f, 0.005f, 0.1f), ArenaUtil.Unlit(new Color(0.9f, 0.9f, 0.9f)));
            var a = go.AddComponent<Arrow>();
            a.vel = velocity; a.prev = pos; a.damage = damage; a.shooter = shooter;
            go.transform.rotation = Quaternion.LookRotation(velocity);
            return a;
        }

        void Update()
        {
            if (dead || ArenaGame.Paused) return;
            float dt = Time.deltaTime;
            life -= dt;
            vel += Physics.gravity * dt;
            var now = prev + vel * dt;
            transform.position = now;
            if (vel.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(vel);
            if (reflected) CheckEnemies(now);
            else CheckPlayer(now);
            if (!dead && (life <= 0f || now.y < 0.02f || ArenaUtil.IsOutside(now, 0.2f))) Stick();
            prev = now;
        }

        void CheckPlayer(Vector3 now)
        {
            var game = ArenaGame.Instance;
            if (!game) return;
            var pl = game.Player;
            var shield = pl.Shield;
            var w = pl.Weapon;
            if (shield && shield.Equipped && shield.Intercepts(prev, prev, now, now, 0.05f))
            {
                ArenaAudio.Play(Sfx.Block, now, 0.8f, 1.3f);
                ArenaFx.Burst(now, new Color(1f, 0.82f, 0.56f), 6, 2f);
                ArenaInput.Haptic(0.4f, 0.05f, left: true);
                Kill();
                return;
            }
            if (w != null && w.Held && Geometry.SweptSegmentsHit(w.BasePrev.ToN(), w.TipPrev.ToN(), w.BasePoint.ToN(), w.TipPoint.ToN(),
                                                                  prev.ToN(), prev.ToN(), now.ToN(), now.ToN(), ArrowRules.DeflectRadius))
            {
                if (w.TipSpeed > ArrowRules.DeflectSpeed || (!pl.IsVr && pl.Input.DesktopParry)) Reflect(now);
                else { ArenaAudio.Play(Sfx.Block, now, 0.6f, 1.5f); Kill(); }
                return;
            }
            // Desktop: segurar o aparo olhando para a flecha também rebate.
            if (!pl.IsVr && pl.Input.DesktopParry && (now - pl.HeadPosition).magnitude < 1.2f &&
                Vector3.Dot(pl.HeadForward, -vel.Flat().normalized) > 0.5f) { Reflect(now); return; }
            var head = pl.HeadPosition;
            var waist = new Vector3(head.x, pl.FeetPosition.y + 0.6f, head.z);
            if (Geometry.SweptSegmentsHit(waist.ToN(), head.ToN(), waist.ToN(), head.ToN(), prev.ToN(), prev.ToN(), now.ToN(), now.ToN(), 0.22f))
            {
                pl.TakeDamage(damage, now - vel.normalized);
                ArenaAudio.Play(Sfx.Stab, now, 0.7f, 1.5f);
                Kill();
            }
        }

        void Reflect(Vector3 at)
        {
            reflected = true;
            life = 3f;
            var target = shooter && !shooter.Dead ? shooter.Center : at - vel.normalized * 10f;
            vel = (target - at).normalized * ArrowRules.Speed * 1.3f;
            ArenaAudio.Play(Sfx.Parry, at, 0.9f, 1.4f);
            ArenaFx.FloatText("REBATEU!", at + Vector3.up * 0.2f, new Color(0.62f, 0.88f, 1f), 1f);
            ArenaFx.Burst(at, new Color(1f, 0.91f, 0.63f), 12, 3f);
            ArenaInput.Haptic(0.8f, 0.07f);
        }

        void CheckEnemies(Vector3 now)
        {
            foreach (var e in ArenaEnemy.All.ToArray())
            {
                if (!e || e.Dead) continue;
                e.BodyAxis(out var a, out var b);
                if (!Geometry.SweptSegmentsHit(a.ToN(), b.ToN(), a.ToN(), b.ToN(), prev.ToN(), prev.ToN(), now.ToN(), now.ToN(), e.Radius + 0.05f)) continue;
                e.ApplyDamage(ArrowRules.ReflectedDamage, 0.8f, vel, 1.5f, new Color(0.62f, 0.88f, 1f));
                Kill();
                return;
            }
        }

        // Para no chão/parede e some depois de um tempo.
        void Stick() { dead = true; Destroy(gameObject, 2f); }
        void Kill() { dead = true; Destroy(gameObject); }
    }
}
