using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Habilidades das classes ligadas aos botões.
    // Lâmina: A = Investida (aponte a espada para frente), gatilho = Postura de Contra (ou chamar a arma de volta),
    //         B = Lâmina de Vento, grip = arremessar a arma.
    // Elementalista: segurar gatilho e soltar = Bola de Fogo (arremesse com a mão), A = Lança de Gelo, segurar B e soltar = Raízes.
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class SkillCaster : MonoBehaviour
    {
        ArenaPlayer player;
        float fireCharge, slashCd, hintCd;
        Transform fireOrb, rootMarker;
        bool rootAim;
        Vector3 handPrev, handVel;

        PlayerState S => player.State;
        // Mirando Bola de Fogo ou Raízes (a habilidade da arma não carrega nem solta nesse meio tempo).
        public bool Aiming => fireCharge > 0f || rootAim;
        ArenaInput I => player.Input;

        void Awake() { player = GetComponent<ArenaPlayer>(); }

        void Update()
        {
            if (ArenaGame.Blocked) return;
            float dt = Time.deltaTime;
            hintCd -= dt; slashCd -= dt;
            // Velocidade da mão relativa ao corpo (andando, a Bola de Fogo ia na direção do passo e não do arremesso).
            var handLocal = player.trackingSpace.InverseTransformPoint(player.RightHand.position);
            if (dt > 0f) { handVel = player.trackingSpace.TransformDirection((handLocal - handPrev) / dt); handPrev = handLocal; }
            if (player.TreeOpen) { CancelAims(); return; }
            // Grip perto de uma arma no chão: pega (em vez de arremessar a da mão).
            if (I.PickupPressed && ((ArenaGame.Instance && ArenaGame.Instance.Sanctuary && ArenaGame.Instance.Sanctuary.TryInteract(player)) || TreasureChest.TryOpen(player) || WeaponPickup.TryPickup(player))) { CancelAims(); return; }
            if (S.Class.Id == ClassId.Lamina) Lamina(); else Elementalista(dt);
        }

        void Hint(string msg)
        {
            if (hintCd > 0f || !ArenaGame.Instance) return;
            hintCd = 1.2f;
            ArenaGame.Instance.Message(msg, 1f);
            ArenaAudio.Play2D(Sfx.Deny, 0.6f);
        }

        bool Cast(string id)
        {
            var r = S.TryCast(id);
            switch (r)
            {
                case SkillCastResult.Ok: return true;
                case SkillCastResult.Locked: Hint("Habilidade bloqueada: abra a árvore"); break;
                case SkillCastResult.NoStamina: Hint("Sem estamina"); break;
                case SkillCastResult.NoMana: Hint("Sem mana"); break;
            }
            return false;
        }

        // ---------- Lâmina ----------
        void Lamina()
        {
            var w = player.Weapon;
            if (I.APressed) Investida();
            if (I.TriggerPressed)
            {
                if (!w.Held) w.Recall();
                else if (Cast(SkillIds.Contra))
                {
                    ArenaGame.Instance?.Message("Postura de Contra!", 0.7f);
                    ArenaAudio.Play2D(Sfx.Skill, 0.6f);
                    ArenaInput.Haptic(0.5f, 0.08f);
                }
            }
            if (I.BPressed && Cast(SkillIds.VentoLamina))
            {
                ArenaGame.Instance?.Message("Lâmina carregada: 2 cortes", 0.9f);
                ArenaAudio.Play2D(Sfx.Skill, 0.6f);
                ArenaInput.Haptic(0.6f, 0.1f);
            }
            if (I.GripPressed && w.Held) w.Throw(player.IsVr ? handVel : Vector3.zero);
            if (!player.IsVr && I.TriggerPressed && !w.Held) w.Recall();

            // Cortes de vento saem nos golpes rápidos enquanto houver carga
            if (S.WindCharges > 0 && w.Held && w.TipSpeed > 5f && slashCd <= 0f && S.ConsumeWindCharge())
            {
                slashCd = 0.35f;
                float dmg = Combat.WindSlashDamage * (S.Has(SkillIds.FioAfiado) ? Combat.SharpEdgeBonus : 1f);
                LaunchSlash(w.TipPoint, player.HeadForward, w.TipVelocity, dmg, new Color(0.81f, 0.96f, 1f), Combat.WindSlashWidth, Combat.WindSlashHitRadius);
                ArenaInput.Haptic(0.8f, 0.08f);
            }
        }

        void Investida()
        {
            var w = player.Weapon;
            if (!w.Held) { Hint("Pegue a espada primeiro"); return; }
            Vector3 dir;
            if (!player.IsVr) { dir = player.HeadForward; w.DesktopPoint(0.35f); }
            else
            {
                var bd = w.BladeDir; var flat = bd.Flat();
                if (Mathf.Abs(bd.y) > 0.55f || flat.magnitude < 0.5f || Vector3.Dot(flat.normalized, player.HeadForward) < 0.6f)
                { Hint("Aponte a espada para frente"); return; }
                dir = flat.normalized;
            }
            if (Cast(SkillIds.Investida)) player.StartCharge(dir);
        }

        public static void LaunchSlash(Vector3 start, Vector3 travel, Vector3 sweep, float dmg, Color color, float width = 1.2f, float hitRadius = 0.55f)
        {
            var f = travel.normalized;
            var w = sweep - Vector3.Dot(sweep, f) * f;
            if (w.sqrMagnitude < 1e-4f) w = new Vector3(f.z, 0, -f.x);
            w.Normalize();
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = ArenaUtil.Unlit(new Color(color.r, color.g, color.b, 0.85f));
            var vis = new GameObject("CorteDeVento");
            go.transform.SetParent(vis.transform, false);
            go.transform.localScale = new Vector3(width, 0.05f, 0.22f);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.right, Quaternion.Inverse(Quaternion.LookRotation(f)) * w);
            var p = Projectile.Spawn(vis, start, f * 15f);
            p.Life = 0.8f; p.Radius = hitRadius; p.Pierce = 99;
            p.OnHitEnemy = (e, _) => e.ApplyDamage(dmg, 0.7f, f, 2f, new Color(0.81f, 0.96f, 1f));
            ArenaAudio.Play(Sfx.Wind, start, 0.7f);
        }

        // ---------- Elementalista ----------
        void Elementalista(float dt)
        {
            var w = player.Weapon;
            // As magias saem do cristal do cajado: com outra arma na mão, só luta corpo a corpo.
            if (!Proficiency.CanCastSpells(S.Class.Id, w.Def.Kind))
            {
                CancelAims();
                if (I.TriggerPressed || I.APressed || I.BPressed) Hint("Magias precisam do cajado (troque a arma)");
                return;
            }
            // Bola de Fogo: segurar carrega, soltar arremessa
            if (I.TriggerPressed && fireCharge <= 0f)
            {
                if (S.CanCast(SkillIds.BolaFogo) == SkillCastResult.Ok)
                {
                    fireCharge = 0.001f;
                    fireOrb = ArenaUtil.Prim(PrimitiveType.Sphere, null, w.CrystalPoint, Vector3.one * 0.24f, ArenaUtil.Unlit(new Color(1f, 0.48f, 0.13f))).transform;
                }
                else Cast(SkillIds.BolaFogo);
            }
            if (fireCharge > 0f)
            {
                fireCharge += dt;
                if (fireOrb) { fireOrb.position = w.CrystalPoint; fireOrb.localScale = Vector3.one * 0.24f * (1f + Mathf.Min(1f, fireCharge / 0.8f)); }
                if (I.TriggerReleased || !I.TriggerHeld) Fireball();
            }
            if (I.APressed) IceLance();
            if (I.BPressed && S.CanCast(SkillIds.Raizes) == SkillCastResult.Ok) { rootAim = true; EnsureRootMarker(); }
            else if (I.BPressed) Cast(SkillIds.Raizes);
            if (rootAim)
            {
                rootMarker.position = RootTarget() + Vector3.up * 0.03f;
                if (I.BReleased || !I.BHeld) { rootAim = false; rootMarker.gameObject.SetActive(false); CastRoots(RootTarget()); }
            }
        }

        void Fireball()
        {
            float power = Mathf.Min(1f, fireCharge / 0.8f);
            fireCharge = 0f;
            if (fireOrb) Destroy(fireOrb.gameObject);
            if (!Cast(SkillIds.BolaFogo)) return;
            var w = player.Weapon;
            var start = w.CrystalPoint;
            Vector3 vel = player.IsVr && handVel.magnitude > 2.5f
                ? handVel.normalized * Mathf.Max(12f, handVel.magnitude * 2f)
                : (player.IsVr ? w.BladeDir : player.Head.forward) * 13f;
            float size = 0.12f + 0.1f * power;
            var vis = ArenaUtil.Prim(PrimitiveType.Sphere, null, start, Vector3.one * size * 2f, ArenaUtil.Unlit(new Color(1f, 0.48f, 0.13f)));
            var p = Projectile.Spawn(vis, start, vel);
            p.Gravity = 3f; p.Life = 2.5f; p.Radius = size; p.Pierce = 1;
            float radius = Combat.FireballRadius(power, S.Has(SkillIds.Combustao));
            float dmg = Combat.FireballDamage(power);
            p.OnEnd = pr => Explode(new Vector3(pr.transform.position.x, Mathf.Max(0.3f, pr.transform.position.y), pr.transform.position.z), radius, dmg);
            ArenaInput.Haptic(0.6f, 0.08f);
            ArenaAudio.Play(Sfx.FireCast, start, 0.8f);
        }

        static void Explode(Vector3 pos, float radius, float dmg)
        {
            ArenaAudio.Play(Sfx.Explosion, pos, 1f);
            ArenaFx.Ring(pos, new Color(1f, 0.48f, 0.13f), radius);
            ArenaFx.Burst(pos, new Color(1f, 0.6f, 0.19f), 24, 5f, 2f);
            foreach (var e in ArenaEnemy.All.ToArray())
            {
                if (e == null || e.Dead) continue;
                float d = Vector3.Distance(e.Center, pos);
                if (d >= radius + e.Radius) continue;
                bool rooted = e.State.Rooted > 0f;
                e.ApplyDamage(dmg * Combat.ExplosionFalloff(d, radius), 0.5f, e.transform.position - pos, rooted ? 0f : 4f,
                              rooted ? new Color(1f, 0.35f, 0.13f) : new Color(1f, 0.69f, 0.38f), burn: true);
            }
        }

        void IceLance()
        {
            if (!Cast(SkillIds.LancaGelo)) return;
            var w = player.Weapon;
            var dir = player.IsVr ? w.BladeDir : player.Head.forward;
            if (!player.IsVr) w.DesktopPoint(0.25f);
            var vis = new GameObject("LancaDeGelo");
            var cone = ArenaUtil.Prim(PrimitiveType.Capsule, vis.transform, Vector3.zero, new Vector3(0.1f, 0.3f, 0.1f), ArenaUtil.Lit(new Color(0.75f, 0.92f, 1f), new Color(0.3f, 0.5f, 0.6f))).transform;
            cone.localRotation = Quaternion.Euler(90, 0, 0);
            var p = Projectile.Spawn(vis, w.CrystalPoint, dir * 20f);
            p.Life = 1.2f; p.Radius = 0.12f; p.Pierce = Combat.IcePierce;
            bool deep = S.Has(SkillIds.GeloProfundo);
            p.OnHitEnemy = (e, _) =>
            {
                e.ApplyDamage(Combat.IceDamage, Combat.IcePosture, dir, 1f, new Color(0.75f, 0.92f, 1f));
                if (deep) e.State.ApplyFreeze(Combat.IceFreezeSeconds); else e.State.ApplySlow(Combat.IceSlowSeconds);
                ArenaFx.Burst(e.Center, new Color(0.75f, 0.92f, 1f), 10, 2f);
            };
            ArenaInput.Haptic(0.5f, 0.06f);
            ArenaAudio.Play(Sfx.Ice, w.CrystalPoint, 0.8f);
        }

        Vector3 RootTarget()
        {
            Vector3 o, d;
            if (player.IsVr) { o = player.Weapon.CrystalPoint; d = player.Weapon.BladeDir; }
            else { o = player.Head.position; d = player.Head.forward; }
            var flat = d.Flat();
            if (flat.sqrMagnitude < 1e-4f) flat = player.HeadForward;
            float reach = Combat.GroundAimReach(o.y - player.FeetPosition.y, d.normalized.y, flat.magnitude / Mathf.Max(d.magnitude, 1e-4f), Combat.RootMaxRange);
            flat.Normalize();
            // Não passa da parede: encurta ao longo da mesma direção (em vez de "dobrar" no canto).
            var start = new Vector3(o.x, 0f, o.z);
            reach = ArenaUtil.RayReach(start, flat, reach);
            return start + flat * Mathf.Max(0f, reach);
        }

        void EnsureRootMarker()
        {
            if (!rootMarker)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = ArenaUtil.Unlit(new Color(0.5f, 0.9f, 0.4f, 0.45f));
                rootMarker = go.transform;
                rootMarker.localScale = new Vector3(Combat.RootRadius * 2f, 0.005f, Combat.RootRadius * 2f);
            }
            rootMarker.gameObject.SetActive(true);
        }

        void CastRoots(Vector3 pos)
        {
            if (!Cast(SkillIds.Raizes)) return;
            ArenaFx.Ring(pos, new Color(0.5f, 0.9f, 0.4f), Combat.RootRadius, 0.5f);
            ArenaAudio.Play(Sfx.Impact, pos, 0.6f, 1.3f); ArenaAudio.Play(Sfx.Wind, pos, 0.4f, 0.6f);
            ArenaFx.Burst(pos + Vector3.up * 0.1f, new Color(0.35f, 0.23f, 0.1f), 24, 3f, 1.5f);
            int n = 0;
            foreach (var e in ArenaEnemy.All)
            {
                if (e == null || e.Dead) continue;
                if (Vector3.Distance(e.transform.position.Flat(), pos.Flat()) < Combat.RootRadius + e.Radius)
                { e.State.ApplyRoot(Combat.RootSeconds); n++; }
            }
            if (n > 0) ArenaGame.Instance?.Message($"Enraizou {n}!", 0.8f);
        }

        void CancelAims()
        {
            if (fireOrb) Destroy(fireOrb.gameObject);
            fireCharge = 0f;
            rootAim = false;
            if (rootMarker) rootMarker.gameObject.SetActive(false);
        }
    }
}
