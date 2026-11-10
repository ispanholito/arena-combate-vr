using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Pistola e bacamarte: gatilho atira pelo cano (tiro instantâneo; o bacamarte espalha bagos), coice físico na arma,
    // fumaça e rastro. Recarregar: cano para cima + tranco rápido (VR) ou espera (desktop). Munição em bolinhas na arma.
    public sealed class GunController : MonoBehaviour
    {
        ArenaPlayer player;
        public bool Left;   // instância da mão esquerda: atira com o gatilho esquerdo
        PlayerWeapon W => player ? (Left ? player.WeaponL : player.Weapon) : null;
        int ammo = -1;
        float cd, reloadT = -1f, desktopReloadT;
        WeaponLook lastLook;
        Transform ammoRoot;
        Transform[] ammoDots;
        readonly RaycastHit[] hits = new RaycastHit[24];

        // ---------- Bacamarte de pump (Pablo, 2026-11-10) ----------
        // A outra mão (grip do controle ou mão livre fechada) perto do fuste puxa para trás ao longo do cano e empurra
        // de volta; o fuste visual desliza junto. A cadência é a velocidade da bombeada (PumpRules).
        bool pumped = true;          // pronto para atirar (começa pronta)
        bool pumpHeld;               // a outra mão está no fuste
        float pumpOffset, pumpPeak, pumpReadyT = -1f, pumpLoseT, pumpHandZ0;
        PumpRules.Phase pumpPhase = PumpRules.Phase.Closed;
        Transform foreGrip;          // fuste visual (desliza)
        Vector3 foreGripRest;
        const float ForeGripZ = 0.36f, PumpTravel = 0.11f;

        bool IsPump(GunSpec spec) => spec != null && spec.Pellets > 1;   // só o bacamarte

        // Mão oposta à que segura a arma: posição e se está "pegando" (grip do controle / mão livre fechada).
        bool OtherHand(out Vector3 pos)
        {
            var I = player.Input;
            bool otherIsLeft = !Left;
            pos = otherIsLeft ? player.leftHand.position : player.rightHand.position;
            if (otherIsLeft) return I.LeftHandFree ? HandPoses.Closed(player, 0) : I.LeftGripHeld;
            return I.RightHandFree ? HandPoses.Closed(player, 1) : I.GripHeld;
        }

        void UpdatePump(PlayerWeapon w, float dt)
        {
            if (!foreGrip || foreGrip.parent != w.Visual) { foreGrip = w.Visual ? w.Visual.Find("Fuste") : null; if (foreGrip) foreGripRest = foreGrip.localPosition; }
            if (pumpReadyT >= 0f) { pumpReadyT -= dt; if (pumpReadyT < 0f) { pumped = true; ArenaAudio.Play(Sfx.Click, w.TipPoint, 0.9f, 1.1f); } }
            if (!player.IsVr) { pumped = true; return; }   // desktop: sem bombear
            bool grab = OtherHand(out var hand);
            // Posição da mão ao longo do cano (z local da arma).
            var local = w.Visual ? w.Visual.InverseTransformPoint(hand) : Vector3.zero;
            float radial = new Vector2(local.x, local.y).magnitude;
            if (!pumpHeld)
            {
                if (grab && w.Visual && radial < PumpRules.GrabRadius && Mathf.Abs(local.z - (ForeGripZ - pumpOffset)) < PumpRules.GrabRadius)
                { pumpHeld = true; pumpHandZ0 = local.z + pumpOffset; pumpPeak = 0f; pumpLoseT = 0f; ArenaInput.Haptic(0.3f, 0.03f, !Left); }
            }
            else
            {
                bool near = w.Visual && radial < PumpRules.GrabRadius * 1.6f;
                if (!grab || !near) { pumpLoseT += dt; if (!grab || pumpLoseT > PumpRules.ReleaseSeconds) { pumpHeld = false; pumpLoseT = 0f; } }
                else pumpLoseT = 0f;
                if (pumpHeld)
                {
                    float prev = pumpOffset;
                    pumpOffset = Mathf.Clamp(pumpHandZ0 - local.z, 0f, PumpTravel);
                    float v = Mathf.Abs(pumpOffset - prev) / Mathf.Max(dt, 1e-4f);
                    pumpPeak = Mathf.Max(pumpPeak, v);
                    var before = pumpPhase;
                    if (PumpRules.Step(ref pumpPhase, pumpOffset))
                    {
                        float delay = PumpRules.ReadyDelay(pumpPeak);
                        if (delay <= 0f) { pumped = true; ArenaAudio.Play(Sfx.Click, w.TipPoint, 1f, 1.2f); }
                        else { pumpReadyT = delay; ArenaAudio.Play(Sfx.Rattle, w.TipPoint, 0.6f, 1.4f); }
                        ArenaInput.Haptic(0.6f, 0.06f, !Left);
                        pumpPeak = 0f;
                    }
                    else if (before != pumpPhase && pumpPhase == PumpRules.Phase.Open) { ArenaAudio.Play(Sfx.Rattle, w.TipPoint, 0.8f, 1.1f); ArenaInput.Haptic(0.5f, 0.05f, !Left); }
                }
            }
            if (!pumpHeld && pumpOffset > 0f) pumpOffset = Mathf.MoveTowards(pumpOffset, 0f, dt * 0.5f);   // soltou: o fuste volta
            if (foreGrip) foreGrip.localPosition = foreGripRest - new Vector3(0f, 0f, pumpOffset);
        }

        void Awake() => player = GetComponent<ArenaPlayer>();

        GunSpec Spec => W && W.Def != null && W.Held ? GunRules.For(W.Def.Look) : null;

        void Update()
        {
            var w = W;
            var spec = Spec;
            if (spec == null) { HideDots(); lastLook = WeaponLook.Padrao; return; }
            if (lastLook != w.Def.Look || ammo < 0) { ammo = spec.Shots; lastLook = w.Def.Look; reloadT = -1f; }
            if (!GunRules.NeedsReload) { ammo = spec.Shots; reloadT = -1f; HideDots(); }
            if (ArenaGame.Blocked || !w.Held) { if (GunRules.NeedsReload) UpdateDots(spec); return; }
            float dt = Time.deltaTime;
            cd -= dt;

            // Recarga
            if (reloadT >= 0f)
            {
                reloadT += dt;
                if (reloadT >= GunRules.ReloadSeconds)
                {
                    reloadT = -1f; ammo = spec.Shots;
                    ArenaAudio.Play(Sfx.Click, w.TipPoint, 1f, 0.8f);
                    ArenaInput.Haptic(0.4f, 0.05f, Left);
                }
            }
            else if (GunRules.NeedsReload && ammo < spec.Shots)
            {
                if (player.IsVr)
                {
                    float up = Vector3.Dot(w.TipVelocity, Vector3.up);   // já é relativa ao jogador
                    if (GunRules.IsReloadFlick(up, Vector3.Dot(w.BladeDir, Vector3.up))) StartReload(w);
                }
                else if (ammo <= 0 && (desktopReloadT += dt) >= GunRules.DesktopReload) { desktopReloadT = 0f; StartReload(w); }
            }

            if (IsPump(spec)) UpdatePump(w, dt);
            bool fire = Left ? player.IsVr && player.Input.ShieldToggle : player.IsVr ? player.Input.TriggerPressed : player.Input.DesktopSwing;
            if (fire && !player.TreeOpen && cd <= 0f)
            {
                if (IsPump(spec) && !pumped)
                {
                    ArenaAudio.Play(Sfx.Click, w.TipPoint, 0.8f, 1.5f);
                    ArenaInput.Haptic(0.3f, 0.04f, Left);
                    if (pumpReadyT < 0f) ArenaGame.Instance?.Message("Bombeie o fuste com a outra mão", 1f);
                }
                else if (ammo <= 0 || reloadT >= 0f)
                {
                    ArenaAudio.Play(Sfx.Click, w.TipPoint, 0.8f, 1.5f);
                    if (ammo <= 0 && player.IsVr) ArenaGame.Instance?.Message("Sem munição: aponte para cima e dê um tranco", 1.2f);
                }
                else Fire(w, spec);
            }
            if (GunRules.NeedsReload) UpdateDots(spec);
        }

        void StartReload(PlayerWeapon w)
        {
            reloadT = 0f;
            ArenaAudio.Play(Sfx.Rattle, w.TipPoint, 0.7f, 1.6f);
            ArenaFx.Burst(w.TipPoint, new Color(0.7f, 0.7f, 0.7f), 4, 0.8f);
        }

        void Fire(PlayerWeapon w, GunSpec spec)
        {
            if (GunRules.NeedsReload) ammo--;
            cd = IsPump(spec) ? 0.1f : spec.Cooldown;   // pump: a cadência é a bombeada, não o relógio
            if (IsPump(spec)) { pumped = false; pumpPhase = PumpRules.Phase.Closed; }
            Vector3 dir;
            if (!player.IsVr) { w.DesktopPoint(0.3f); dir = player.HeadForward; }
            else dir = w.BladeDir;
            var muzzle = w.TipPoint;
            float dmg = GunRules.ShotDamage(spec, w.Def);
            for (int i = 0; i < spec.Pellets; i++)
            {
                var d = Quaternion.LookRotation(dir) * Quaternion.Euler(Random.Range(-spec.Spread, spec.Spread), Random.Range(-spec.Spread, spec.Spread), 0f) * Vector3.forward;
                Shoot(muzzle, d.normalized, spec, dmg);
            }
            // Coice: empurra a ponta para cima/trás (a arma é física).
            if (w.Body) w.Body.AddForceAtPosition((Vector3.up * 0.8f - dir) * (spec.Pellets > 1 ? 9f : 5f), muzzle, ForceMode.Impulse);
            ArenaAudio.Play(Sfx.Explosion, muzzle, 0.9f, spec.Pellets > 1 ? 1.3f : 1.8f);
            ArenaAudio.Play(Sfx.Shot, muzzle, 1f, 0.7f);
            ArenaFx.Burst(muzzle + dir * 0.05f, new Color(1f, 0.75f, 0.3f), 10, 4f, 0.8f);
            ArenaFx.Burst(muzzle + dir * 0.1f, new Color(0.75f, 0.75f, 0.75f), 8, 1.2f, 1.6f);
            ArenaInput.Haptic(1f, spec.Pellets > 1 ? 0.15f : 0.08f, Left);
        }

        void Shoot(Vector3 from, Vector3 dir, GunSpec spec, float dmg)
        {
            // Paredes e objetos fixos param o tiro (barris quebram).
            float maxD = spec.Range;
            int n = Physics.RaycastNonAlloc(from, dir, hits, spec.Range, ~0, QueryTriggerInteraction.Ignore);
            Breakable barrel = null;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider.attachedRigidbody) continue;   // arma, ossos de inimigos, destroços
                if (h.distance < maxD)
                {
                    maxD = h.distance;
                    barrel = h.collider.GetComponentInParent<Breakable>();
                }
            }
            // Inimigo mais perto no caminho.
            ArenaEnemy best = null; float bestT = maxD;
            var end = from + dir * maxD;
            foreach (var e in ArenaEnemy.All)
            {
                if (!e || e.Dead) continue;
                e.BodyAxis(out var a, out var b);
                if (Geometry.SegmentSegmentDistance(from.ToN(), end.ToN(), a.ToN(), b.ToN()) > e.Radius + 0.05f) continue;
                float t = Vector3.Dot(e.Center - from, dir);
                if (t > 0f && t < bestT) { bestT = t; best = e; }
            }
            var hitPoint = from + dir * (best ? bestT : maxD);
            if (best)
            {
                bool head = best.HeightFraction(hitPoint) > 0.7f;
                best.ApplyDamage(dmg * (head ? WeakPoints.HeadMultiplier : 1f), spec.Posture, dir, spec.Knock, head ? new Color(1f, 0.9f, 0.3f) : (Color?)null);
                if (head) ArenaFx.FloatText("CABEÇA!", hitPoint + Vector3.up * 0.2f, new Color(1f, 0.9f, 0.3f), 1f);
                if (best is PhysicalGoblinEnemy pg && pg.Body && spec.Knock >= 5f && !pg.Dead) pg.Body.OnThrown(dir.Flat().normalized * 3f + Vector3.up * 1.5f);
                ArenaFx.Burst(hitPoint, new Color(0.7f, 0.12f, 0.16f), 6, 2f);
            }
            else
            {
                if (barrel) barrel.Break();
                if (maxD < spec.Range) ArenaFx.Burst(hitPoint, new Color(0.6f, 0.55f, 0.5f), 4, 1.5f);
            }
            Tracer(from, hitPoint);
        }

        static void Tracer(Vector3 a, Vector3 b)
        {
            var go = new GameObject("Tiro");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, a); lr.SetPosition(1, b);
            lr.startWidth = 0.02f; lr.endWidth = 0.008f;
            lr.material = ArenaUtil.Unlit(new Color(1f, 0.85f, 0.5f, 0.8f));
            Destroy(go, 0.06f);
        }

        // Munição: bolinhas em cima da arma (cheia = dourada, gasta = escura).
        void UpdateDots(GunSpec spec)
        {
            var vis = W.Visual;
            if (!vis) return;
            if (!ammoRoot || ammoRoot.parent != vis || ammoDots == null || ammoDots.Length != spec.Shots)
            {
                if (ammoRoot) Destroy(ammoRoot.gameObject);
                ammoRoot = new GameObject("Municao").transform;
                ammoRoot.SetParent(vis, false);
                ammoRoot.localPosition = new Vector3(0f, 0.07f, 0f);
                ammoDots = new Transform[spec.Shots];
                for (int i = 0; i < spec.Shots; i++)
                    ammoDots[i] = ArenaUtil.Prim(PrimitiveType.Sphere, ammoRoot, new Vector3(0f, 0f, i * 0.035f), Vector3.one * 0.022f,
                                                 ArenaUtil.Unlit(new Color(1f, 0.8f, 0.3f))).transform;
            }
            ammoRoot.gameObject.SetActive(true);
            for (int i = 0; i < ammoDots.Length; i++)
                ammoDots[i].GetComponent<Renderer>().material.color = i < ammo && reloadT < 0f ? new Color(1f, 0.8f, 0.3f) : new Color(0.2f, 0.18f, 0.15f);
        }

        void HideDots() { if (ammoRoot) ammoRoot.gameObject.SetActive(false); }
    }
}
