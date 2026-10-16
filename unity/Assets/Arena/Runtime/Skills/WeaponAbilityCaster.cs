using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Habilidade própria da arma na mão (WeaponAbilities no Core), para qualquer classe.
    // VR: erga a arma acima da cabeça com a ponta para cima por ~0,5 s (vibra e a arma brilha) e dê um golpe rápido
    // em até 4 s para soltar. Desktop: H solta direto (com o golpe animado).
    [RequireComponent(typeof(ArenaPlayer))]
    public sealed class WeaponAbilityCaster : MonoBehaviour
    {
        ArenaPlayer player;
        SkillCaster skills;
        readonly WeaponCharge charge = new WeaponCharge();
        Transform glow;
        Material glowMat;
        float hintCd;

        public bool Charged => charge.Charged;
        public float RaiseProgress => charge.RaiseProgress;

        PlayerState S => player.State;

        void Awake()
        {
            player = GetComponent<ArenaPlayer>();
            skills = GetComponent<SkillCaster>();
        }

        void Update()
        {
            hintCd -= Time.deltaTime;
            var w = player.Weapon;
            if (ArenaGame.Blocked || player.TreeOpen || w == null || w.Def == null || !w.Held) { charge.Cancel(); UpdateGlow(); return; }
            bool aiming = skills && skills.Aiming;

            if (!player.IsVr)
            {
                if (player.Input.WeaponAbility && !aiming) TryRelease(player.HeadForward, desktop: true);
                UpdateGlow();
                return;
            }

            bool raised = w.TipPoint.y > player.Head.position.y + 0.1f && w.BladeDir.y > 0.6f;
            switch (charge.Tick(Time.deltaTime, raised, w.TipSpeed, !aiming && !w.TwoHanded))
            {
                case WeaponCharge.Event.Charged:
                    var r = S.CanUseWeaponAbility();
                    if (r != SkillCastResult.Ok) { charge.Cancel(); Hint(Reason(r)); break; }
                    ArenaInput.Haptic(0.7f, 0.12f);
                    ArenaAudio.Play(Sfx.Skill, w.TipPoint, 0.7f, 1.3f);
                    ArenaGame.Instance?.Message(S.ActiveItem.Ability.Name + ": golpeie!", 1f);
                    break;
                case WeaponCharge.Event.Released:
                    if (aiming) break;
                    var dir = w.TipVelocity.Flat();
                    TryRelease(dir.sqrMagnitude > 0.5f ? dir.normalized : player.HeadForward, desktop: false);
                    break;
            }
            UpdateGlow();
        }

        static string Reason(SkillCastResult r) => r switch
        {
            SkillCastResult.Cooldown => "Habilidade da arma recarregando",
            SkillCastResult.NoMana => "Sem mana",
            SkillCastResult.NoStamina => "Sem estamina",
            _ => "Sem habilidade",
        };

        void Hint(string msg)
        {
            if (hintCd > 0f) return;
            hintCd = 1f;
            ArenaGame.Instance?.Message(msg, 1f);
            ArenaAudio.Play2D(Sfx.Deny, 0.6f);
        }

        void TryRelease(Vector3 dir, bool desktop)
        {
            var r = S.TryWeaponAbility();
            if (r != SkillCastResult.Ok) { Hint(Reason(r)); return; }
            var item = S.ActiveItem;
            if (desktop) player.Weapon.DesktopSwing();
            Fire(item, dir.Flat().normalized);
        }

        // ---------- Efeitos ----------
        void Fire(ItemInstance item, Vector3 dir)
        {
            var w = player.Weapon;
            var def = w.Def;
            float power = WeaponAbilities.Power(item.Rarity);
            float dmg = WeaponAbilities.BaseDamage(item.Ability.Kind) * def.DamageMultiplier * power;
            var feet = player.FeetPosition;
            var color = WeaponModels.RarityColor(item.Rarity == Rarity.Comum ? Rarity.Raro : item.Rarity);
            ArenaGame.Instance?.Message(item.Ability.Name + "!", 0.9f);
            bool legend = item.Rarity == Rarity.Lendario;
            ArenaInput.Haptic(1f, 0.15f);
            switch (item.Ability.Kind)
            {
                case WeaponAbilityKind.CorteAmplo:
                {
                    float range = 2.8f * power;
                    foreach (var e in ArenaEnemy.All.ToArray())
                    {
                        if (!e || e.Dead) continue;
                        var to = (e.transform.position - feet).Flat();
                        if (to.magnitude > range + e.Radius || Vector3.Angle(dir, to) > 75f) continue;
                        Hit(e, dmg, 0.8f, to, 3f, item);
                    }
                    for (int i = -3; i <= 3; i++)   // arco de faíscas mostrando o alcance
                        ArenaFx.Burst(feet + Vector3.up * 1.1f + Quaternion.Euler(0, i * 22f, 0) * dir * range * 0.8f, color, 5, 2f);
                    ArenaAudio.Play(Sfx.Whoosh, w.TipPoint, 1f, 0.7f);
                    if (legend)   // Ceifadora: 3 cortes de vento em leque
                        for (int i = -1; i <= 1; i++)
                        {
                            var d = Quaternion.Euler(0, i * 25f, 0) * dir;
                            SkillCaster.LaunchSlash(w.TipPoint, d, Vector3.Cross(Vector3.up, d), dmg * WeaponAbilities.LegendarySlashFraction, color, 1.6f, 0.7f);
                        }
                    break;
                }
                case WeaponAbilityKind.FendaNoChao:
                {
                    float len = 6f * power;
                    foreach (var e in ArenaEnemy.All.ToArray())
                    {
                        if (!e || e.Dead) continue;
                        var to = (e.transform.position - feet).Flat();
                        float along = Vector3.Dot(to, dir);
                        float side = (to - dir * along).magnitude;
                        if (along < 0f || along > len || side > 0.8f + e.Radius) continue;
                        Hit(e, dmg, 2.2f, dir, 2f, item);
                        if (legend && e && !e.Dead) e.State.ApplyStun(WeaponAbilities.LegendaryStunSeconds);   // Racha-Montanhas
                    }
                    for (float d = 0.8f; d <= len; d += 0.8f)
                        ArenaFx.Burst(feet + dir * d + Vector3.up * 0.1f, new Color(0.55f, 0.45f, 0.35f), 6, 3f);
                    ArenaAudio.Play(Sfx.Slam, feet + dir * 2f);
                    break;
                }
                case WeaponAbilityKind.Terremoto:
                {
                    float radius = 3f * power;
                    var c = new Vector3(w.TipPoint.x, 0f, w.TipPoint.z);
                    foreach (var e in ArenaEnemy.All.ToArray())
                    {
                        if (!e || e.Dead) continue;
                        var to = (e.transform.position - c).Flat();
                        if (to.magnitude > radius + e.Radius) continue;
                        Hit(e, dmg, 3f, legend ? -to : to, legend ? 4f : 2f, item);   // Martelo do Trovão puxa para o centro
                        if (e && !e.Dead) e.State.ApplyStun(WeaponAbilities.StunSeconds * power);
                    }
                    ArenaFx.Ring(c + Vector3.up * 0.05f, new Color(1f, 0.85f, 0.4f), radius, 0.5f);
                    ArenaAudio.Play(Sfx.Slam, c);
                    break;
                }
                case WeaponAbilityKind.ArremessoPerfurante:
                    StartCoroutine(SpectralSpear(item, dmg, color, legend));   // meio segundo para mirar antes de soltar
                    break;
                case WeaponAbilityKind.PassoSombrio:
                {
                    ArenaEnemy best = null; float bestD = 6f * power;
                    foreach (var e in ArenaEnemy.All)
                    {
                        if (!e || e.Dead) continue;
                        float d = (e.transform.position - feet).Flat().magnitude;
                        if (d < bestD) { bestD = d; best = e; }
                    }
                    if (!best) { ArenaGame.Instance?.Message("Ninguém por perto", 0.8f); break; }
                    var back = best.transform.position - best.transform.forward * (best.Radius + 0.9f);
                    ArenaFx.Burst(feet + Vector3.up, new Color(0.4f, 0.2f, 0.6f), 16, 3f);
                    player.TeleportTo(ArenaUtil.ClampToArena(back), best.transform.position);
                    S.SetInvulnerable(0.4f);
                    S.ArmBackstab(legend ? WeaponAbilities.LegendaryBackstabMultiplier : WeaponAbilities.BackstabMultiplier);   // Sussurro da Noite: ×3
                    ArenaFx.Burst(back + Vector3.up, new Color(0.4f, 0.2f, 0.6f), 16, 3f);
                    ArenaAudio.Play2D(Sfx.Dash, 1f, 0.7f);
                    break;
                }
                default:   // Pulso Arcano
                {
                    float radius = 3f * power;
                    foreach (var e in ArenaEnemy.All.ToArray())
                    {
                        if (!e || e.Dead) continue;
                        var to = (e.transform.position - feet).Flat();
                        if (to.magnitude > radius + e.Radius) continue;
                        Hit(e, dmg, 0.6f, to.sqrMagnitude > 0.01f ? to : dir, 7f, item);
                        if (legend && e && !e.Dead) e.State.ApplyFreeze(WeaponAbilities.LegendaryFreezeSeconds);   // Cetro Estelar
                    }
                    ArenaFx.Ring(feet + Vector3.up * 0.8f, new Color(0.6f, 0.75f, 1f), radius, 0.4f);
                    ArenaAudio.Play(Sfx.Explosion, feet, 0.8f, 1.4f);
                    break;
                }
            }
        }

        // Lança Espectral: aparece na ponta da arma e fica 0,5 s apontando para onde você aponta a lança (VR) ou olha
        // (desktop), com uma linha de mira; depois dispara nessa direção (pedido do Pablo: antes saía na hora e não dava
        // para mirar num slime).
        public const float SpearAimSeconds = 0.5f;

        System.Collections.IEnumerator SpectralSpear(ItemInstance item, float dmg, Color color, bool legend)
        {
            var vis = new GameObject("LancaEspectral");
            ArenaUtil.Prim(PrimitiveType.Cube, vis.transform, Vector3.zero, new Vector3(0.06f, 0.06f, 1.2f), ArenaUtil.Unlit(new Color(color.r, color.g, color.b, 0.85f))).name = "Haste";
            var guide = ArenaUtil.Prim(PrimitiveType.Cube, vis.transform, new Vector3(0, 0, 4.6f), new Vector3(0.015f, 0.015f, 8f), ArenaUtil.Unlit(new Color(color.r, color.g, color.b, 0.25f)));
            ArenaAudio.Play(Sfx.Skill, player.Weapon.TipPoint, 0.6f, 0.8f);
            Vector3 start = player.Weapon.TipPoint, aim = player.HeadForward;
            for (float t = 0f; t < SpearAimSeconds; t += Time.deltaTime)
            {
                Aim(out start, out aim);
                vis.transform.SetPositionAndRotation(start, Quaternion.LookRotation(aim));
                yield return null;
            }
            Aim(out start, out aim);
            Destroy(guide);
            var p = Projectile.Spawn(vis, start, aim * 22f);
            vis.transform.rotation = Quaternion.LookRotation(aim);
            p.Life = 1.2f; p.Radius = 0.35f; p.Pierce = 5;
            p.OnHitEnemy = (e, _) => Hit(e, dmg, 1f, aim, 2.5f, item);
            if (legend)   // Presa da Serpente: explode no fim do voo
                p.OnEnd = pr =>
                {
                    var at = pr.transform.position;
                    foreach (var e in ArenaEnemy.All.ToArray())
                        if (e && !e.Dead && (e.transform.position - at).Flat().magnitude < WeaponAbilities.LegendaryExplosionRadius + e.Radius)
                            Hit(e, dmg * WeaponAbilities.LegendaryExplosionFraction, 1f, (e.transform.position - at).Flat(), 3f, item);
                    ArenaFx.Ring(new Vector3(at.x, 0.05f, at.z), color, WeaponAbilities.LegendaryExplosionRadius, 0.4f);
                    ArenaAudio.Play(Sfx.Explosion, at, 0.8f, 1.2f);
                };
            ArenaAudio.Play(Sfx.Ice, start, 0.8f, 0.6f);
            ArenaInput.Haptic(0.8f, 0.08f);
        }

        void Aim(out Vector3 start, out Vector3 aim)
        {
            var w = player.Weapon;
            if (player.IsVr && w && w.Held) { aim = w.BladeDir; start = w.TipPoint + aim * 0.3f; }
            else { aim = player.Head.forward; start = player.Head.position + aim * 0.6f - Vector3.up * 0.25f; }
        }

        void Hit(ArenaEnemy e, float dmg, float postureFactor, Vector3 dir, float knock, ItemInstance item)
        {
            if (!e || e.Dead) return;
            var ev = e.ApplyDamage(dmg, postureFactor, dir, knock, WeaponModels.RarityColor(item.Rarity == Rarity.Comum ? Rarity.Raro : item.Rarity));
            S.OnDealtDamage(ev.Amount, player.Weapon.Def);
        }

        // A arma brilha enquanto carrega/está carregada.
        void UpdateGlow()
        {
            var w = player.Weapon;
            bool show = (charge.Charged || charge.RaiseProgress > 0.05f) && w && w.Visual;
            if (!show) { if (glow) glow.gameObject.SetActive(false); return; }
            if (!glow)
            {
                glowMat = ArenaUtil.Unlit(new Color(1f, 0.9f, 0.5f, 0.5f));
                glow = ArenaUtil.Prim(PrimitiveType.Sphere, null, Vector3.zero, Vector3.one * 0.18f, glowMat).transform;
                glow.name = "BrilhoDaArma";
            }
            glow.gameObject.SetActive(true);
            glow.position = w.TipPoint;
            float s = charge.Charged ? 0.22f + Mathf.Sin(Time.time * 12f) * 0.04f : 0.08f + 0.12f * charge.RaiseProgress;
            glow.localScale = Vector3.one * s;
        }

        void OnDestroy() { if (glow) Destroy(glow.gameObject); }
    }
}
