using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Mímico: o baú que morde. Anda aos pulinhos; perto, abre a tampa (brilha vermelho = aviso) e dá um bote.
    // O bote pode ser aparado com a arma rápida (ou escudo empurrado) no caminho da boca: atordoa e quebra postura.
    public sealed class MimicEnemy : ArenaEnemy
    {
        enum Ai { Chase, Windup, Lunge, Recover }

        Ai ai = Ai.Chase;
        float t, hopT;
        bool bit;
        Transform model, lid;
        Material lidMat, tongueMat;
        Vector3 lungeDir, mouthPrev;
        static readonly Color Wood = new Color(0.4f, 0.24f, 0.1f);

        Vector3 Mouth => transform.position + Vector3.up * 0.5f + transform.forward * 0.42f;

        protected override void BuildModel()
        {
            bodyBottom = 0.1f; bodyTop = 0.75f;
            bloodColor = new Color(0.55f, 0.1f, 0.2f);
            model = new GameObject("Modelo").transform;
            model.SetParent(transform, false);
            var gold = ArenaUtil.Lit(new Color(0.85f, 0.65f, 0.2f), new Color(0.25f, 0.18f, 0.04f));
            ArenaUtil.Prim(PrimitiveType.Cube, model, new Vector3(0, 0.25f, 0), new Vector3(0.85f, 0.46f, 0.55f), ArenaUtil.Lit(Wood));
            ArenaUtil.Prim(PrimitiveType.Cube, model, new Vector3(0, 0.25f, 0.28f), new Vector3(0.12f, 0.14f, 0.03f), gold);
            // Dentes de baixo e língua.
            var tooth = ArenaUtil.Lit(new Color(0.95f, 0.93f, 0.85f));
            for (int i = 0; i < 6; i++)
                ArenaUtil.Prim(PrimitiveType.Cube, model, new Vector3(-0.33f + i * 0.13f, 0.5f, 0.22f), new Vector3(0.05f, 0.1f, 0.05f), tooth);
            tongueMat = ArenaUtil.Lit(new Color(0.75f, 0.15f, 0.25f));
            ArenaUtil.Prim(PrimitiveType.Cube, model, new Vector3(0, 0.49f, 0.05f), new Vector3(0.3f, 0.04f, 0.4f), tongueMat);
            // Tampa com dentes de cima e olhos.
            lid = new GameObject("Tampa").transform;
            lid.SetParent(model, false);
            lid.localPosition = new Vector3(0, 0.48f, -0.27f);
            lidMat = ArenaUtil.Lit(Wood);
            ArenaUtil.Prim(PrimitiveType.Cube, lid, new Vector3(0, 0.07f, 0.27f), new Vector3(0.87f, 0.14f, 0.57f), lidMat);
            ArenaUtil.Prim(PrimitiveType.Cube, lid, new Vector3(0, 0.07f, 0.27f), new Vector3(0.89f, 0.04f, 0.1f), gold);
            for (int i = 0; i < 6; i++)
                ArenaUtil.Prim(PrimitiveType.Cube, lid, new Vector3(-0.33f + i * 0.13f, -0.03f, 0.5f), new Vector3(0.05f, 0.1f, 0.05f), tooth);
            var eye = ArenaUtil.Lit(new Color(1f, 0.85f, 0.2f), new Color(0.8f, 0.6f, 0.1f));
            foreach (int s in new[] { -1, 1 })
                ArenaUtil.Prim(PrimitiveType.Sphere, lid, new Vector3(s * 0.18f, 0.16f, 0.5f), Vector3.one * 0.08f, eye);
            hopT = 0.6f;
        }

        void SetAi(Ai s)
        {
            ai = s; t = 0f; bit = false;
            if (s != Ai.Windup && s != Ai.Lunge) ArenaGame.Tokens.Release(GetInstanceID());   // devolve a ficha de ataque
            lidMat.color = s == Ai.Windup ? new Color(0.9f, 0.2f, 0.08f) : Wood;
            if (s == Ai.Windup) ArenaAudio.Play(Sfx.Windup, Center, 0.8f, 0.7f);
            if (s == Ai.Lunge) mouthPrev = Mouth;
        }

        void SetLid(float deg, float speed, float dt) =>
            lid.localRotation = Quaternion.Slerp(lid.localRotation, Quaternion.Euler(-deg, 0, 0), Mathf.Min(1f, dt * speed));

        protected override void Think(float dt, Vector3 toPlayer, float d)
        {
            t += dt;
            if (State.Stun > 0f)
            {
                SetLid(70f, 4f, dt);   // atordoado: boca escancarada (janela de crítico)
                model.localPosition = Vector3.zero;
                if (ai != Ai.Recover) SetAi(Ai.Recover);
                return;
            }
            var dir = d > 1e-3f ? toPlayer / d : transform.forward;
            switch (ai)
            {
                case Ai.Chase:
                {
                    FacePlayer(toPlayer, dt, 6f);
                    SetLid(8f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 10f, 6f, dt);   // bate os dentes
                    hopT -= dt;
                    // Pulinhos: anda em surtos.
                    float hop = Mathf.Max(0f, Mathf.Sin(Time.time * 7f));
                    model.localPosition = new Vector3(0, hop * 0.12f, 0);
                    if (d > 1.1f) Move(dir * Def.MoveSpeed * (0.4f + hop), dt);
                    if (d < MimicRules.BiteRange && t > 0.8f && State.CanAct && ArenaGame.Tokens.TryTake(GetInstanceID())) SetAi(Ai.Windup);
                    break;
                }
                case Ai.Windup:
                    FacePlayer(toPlayer, dt, 8f);
                    model.localPosition = Vector3.zero;
                    SetLid(85f, 8f, dt);
                    if (t > Def.Windup) { lungeDir = dir; SetAi(Ai.Lunge); }
                    break;
                case Ai.Lunge:
                {
                    Move(lungeDir * MimicRules.LungeSpeed, dt);
                    SetLid(t < MimicRules.LungeSeconds * 0.6f ? 85f : 0f, 18f, dt);
                    if (!bit) CheckBite();
                    if (t > MimicRules.LungeSeconds) SetAi(Ai.Recover);
                    break;
                }
                case Ai.Recover:
                    SetLid(10f, 4f, dt);
                    Move(-dir * 0.5f, dt);
                    if (t > MimicRules.RecoverSeconds) SetAi(Ai.Chase);
                    break;
            }
        }

        // Caminho da boca neste quadro contra escudo, arma e corpo do jogador.
        void CheckBite()
        {
            var mouth = Mouth;
            var pl = Player;
            var shield = pl.Shield;
            var w = pl.Weapon;
            bool shieldHit = shield && shield.Equipped && shield.Intercepts(mouthPrev, mouthPrev + Vector3.up * 0.15f, mouth, mouth + Vector3.up * 0.15f, 0.08f);
            bool weaponHit = w != null && w.Held && Geometry.SweptSegmentsHit(w.BasePrev.ToN(), w.TipPrev.ToN(), w.BasePoint.ToN(), w.TipPoint.ToN(),
                                                                                mouthPrev.ToN(), (mouthPrev + Vector3.up * 0.15f).ToN(), mouth.ToN(), (mouth + Vector3.up * 0.15f).ToN(), 0.12f);
            bool desktopGuard = !pl.IsVr && pl.Input.DesktopParry && Vector3.Dot(pl.HeadForward, (transform.position - pl.FeetPosition).Flat().normalized) > 0.5f;
            if (shieldHit || weaponHit || desktopGuard)
            {
                bit = true;
                float speed = shieldHit ? shield.GuardSpeed : desktopGuard ? Combat.ParrySpeed + 1f : w.TipSpeed;
                bool parry = shieldHit ? speed > Guard.ShieldParrySpeed : speed > Combat.ParrySpeed;
                if (parry)
                {
                    ArenaAudio.Play(Sfx.Parry, mouth);
                    ArenaFx.FloatText("APARO!", mouth, new Color(0.62f, 0.88f, 1f), 1.1f);
                    ArenaFx.Burst(mouth, new Color(1f, 0.91f, 0.63f), 16, 4f);
                    ArenaInput.Haptic(1f, 0.09f);
                    State.AddPosture(Guard.ParryPosture);
                    State.ApplyStun(1.3f);
                    knock += (transform.position - pl.FeetPosition).Flat().normalized * 2.5f;
                    SetAi(Ai.Recover);
                }
                else
                {
                    ArenaAudio.Play(Sfx.Block, mouth);
                    ArenaFx.FloatText("BLOQUEIO", mouth, new Color(0.85f, 0.85f, 0.85f), 0.9f);
                    pl.State.Stamina.TrySpend(Guard.ShieldBlockStaminaCost, 1f);
                    State.AddPosture(Guard.ShieldBlockPosture);
                    if (!shieldHit) pl.TakeDamage(Def.AttackDamage * 0.4f, transform.position, ignoreGuardCooldown: true);
                    SetAi(Ai.Recover);
                }
                return;
            }
            var head = pl.HeadPosition;
            var waist = new Vector3(head.x, pl.FeetPosition.y + 0.3f, head.z);
            if (Geometry.SweptSegmentsHit(waist.ToN(), head.ToN(), waist.ToN(), head.ToN(), mouthPrev.ToN(), mouthPrev.ToN(), mouth.ToN(), mouth.ToN(), 0.4f))
            {
                bit = true;
                pl.TakeDamage(Def.AttackDamage, transform.position);
                ArenaAudio.Play(Sfx.Bone, mouth, 1f, 0.6f);
            }
            mouthPrev = mouth;
        }

        protected override void OnDestroy() { ArenaGame.Tokens.Release(GetInstanceID()); base.OnDestroy(); }

        // Golpes seguidos sem quebrar a postura: fecha a tampa e pula para trás.
        protected override void ComboBreak()
        {
            base.ComboBreak();
            SetAi(Ai.Recover);
        }
    }
}
