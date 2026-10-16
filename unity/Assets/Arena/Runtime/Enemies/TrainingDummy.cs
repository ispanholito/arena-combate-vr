using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Boneco de treino (modo Treino): parado, não ataca, mostra DPS e dano total; volta com a vida cheia
    // depois de 3 s sem apanhar. Postura, raízes, lentidão e congelamento funcionam normalmente.
    public sealed class TrainingDummy : ArenaEnemy
    {
        public const float ResetAfterIdle = 3f;

        readonly DpsMeter meter = new DpsMeter(5f);
        TextMesh label;

        protected override void BuildModel()
        {
            bodyBottom = 0.2f; bodyTop = 1.6f;
            bloodColor = new Color(0.85f, 0.75f, 0.4f);
            var wood = ArenaUtil.Lit(new Color(0.35f, 0.23f, 0.12f));
            var straw = ArenaUtil.Lit(new Color(0.78f, 0.66f, 0.35f));
            var cloth = ArenaUtil.Lit(new Color(0.45f, 0.18f, 0.15f));
            ArenaUtil.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.9f, 0), new Vector3(0.08f, 0.9f, 0.08f), wood);
            ArenaUtil.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 0.02f, 0), new Vector3(0.6f, 0.02f, 0.6f), wood);
            ArenaUtil.Prim(PrimitiveType.Capsule, transform, new Vector3(0, 1.05f, 0), new Vector3(0.5f, 0.42f, 0.4f), straw);
            ArenaUtil.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 1.1f, 0), new Vector3(0.52f, 0.18f, 0.42f), cloth);
            ArenaUtil.Prim(PrimitiveType.Sphere, transform, new Vector3(0, 1.62f, 0), Vector3.one * 0.3f, straw);
            var arms = ArenaUtil.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 1.3f, 0), new Vector3(0.06f, 0.45f, 0.06f), wood).transform;
            arms.localRotation = Quaternion.Euler(0, 0, 90);

            label = ArenaFx.MakeText(transform, "", 0.006f, Color.white);
            label.transform.localPosition = new Vector3(0, 2.25f, 0);
            State.Damaged += ev => meter.Add(ev.Amount);
        }

        protected override void Think(float dt, Vector3 toPlayer, float d)
        {
            FacePlayer(toPlayer, dt, 3f);
            meter.Tick(dt);
            if (meter.IdleTime > ResetAfterIdle && State.Hp < Def.MaxHp) State.HealFull();
            var cam = Camera.main;
            if (cam) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
            label.text = meter.IdleTime > 6f
                ? "<color=#bbb>Boneco de treino\nbata para medir o dano</color>"
                : $"DPS (5 s): <b>{meter.Dps:0}</b>\nTotal: {meter.Total:0}   Último: {meter.LastHit:0}"
                  + (State.Posture.IsBroken ? "\n<color=#ffd36b>POSTURA QUEBRADA</color>" : "");
        }

        protected override void Die() { State.HealFull(); }   // nunca morre de verdade
    }
}
