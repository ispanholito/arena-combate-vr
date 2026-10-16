using System.Collections.Generic;
using Arena.Core;
using UnityEngine;

namespace Arena
{
    // Base dos inimigos: vida/postura (Core), barras, empurrão, eixo do corpo para colisão com a arma.
    public abstract class ArenaEnemy : MonoBehaviour
    {
        public static readonly List<ArenaEnemy> All = new List<ArenaEnemy>();

        public EnemyState State { get; private set; }
        public EnemyDef Def => State.Def;
        public bool Dead => State == null || State.Dead;
        public float SpawnGrace;   // segundos sem levar dano logo ao nascer (filhos do slime)

        protected ArenaPlayer Player => ArenaGame.Instance ? ArenaGame.Instance.Player : null;
        protected Vector3 knock;
        protected float bodyBottom, bodyTop;
        protected Color bloodColor = new Color(0.7f, 0.12f, 0.16f);

        Transform barRoot, hpFill, postureFill;
        Transform visualPivot;                 // todo o modelo fica aqui dentro: inclina no impacto e vira ragdoll ao morrer
        Vector2 lean, leanVel;                 // inclinação (x = para trás/frente, y = lado) em rad
        Vector3 lastHitDir = Vector3.forward;
        float lastHitSpeed, staggerCd, impactCd;
        protected virtual bool LeavesCorpse => true;

        // ---------- Fase 3: empalado pela espada ----------
        public bool Impaled { get; private set; }
        public Rigidbody Body { get; private set; }
        public Collider BodyCollider { get; private set; }
        public void SetImpaled(bool v) { Impaled = v; if (v) knock = Vector3.zero; }
        // Arrastado pela mão do jogador enquanto empalado (raízes e gelo seguram).
        public void Drag(Vector3 velocity, float dt)
        {
            if (Dead || State.Rooted > 0f || State.Frozen > 0f) return;
            transform.position = ArenaUtil.ClampToArena(transform.position + velocity.Flat() * dt);
        }

        public void TakeStab(float tipSpeed, Vector3 dir, WeaponDef weapon, PlayerState ps)
        {
            if (Dead || SpawnGrace > 0f) return;
            bool hasSta = ps.SpendSwingStamina(weapon);
            hitCooldown = Combat.HitCooldown;
            ArenaFx.FloatText("CRAVOU!", Center + Vector3.up * (0.6f + bodyTop * 0.3f), new Color(1f, 0.55f, 0.45f), 1.1f);
            ApplyDamage(StabRules.StabDamage(tipSpeed, weapon, ps.Has(SkillIds.FioAfiado), hasSta), 0.9f, dir, 0f);
        }
        float hitCooldown, hitFlash;
        Renderer[] renderers;
        readonly List<Color> baseColors = new List<Color>();

        public void Init(EnemyDef def)
        {
            State = new EnemyState(def);
            State.Damaged += OnDamaged;
            BuildModel();
            BuildVisualPivot();
            AddBodyCollider();
            BuildBars();
            renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) baseColors.Add(r.sharedMaterial.color);
            All.Add(this);
        }

        void BuildVisualPivot()
        {
            visualPivot = new GameObject("Visual").transform;
            visualPivot.SetParent(transform, false);
            var kids = new List<Transform>();
            foreach (Transform c in transform) if (c != visualPivot) kids.Add(c);
            foreach (var c in kids) c.SetParent(visualPivot, false);
        }

        // ---------- Fase 2: reação física ao impacto da arma ----------
        // impulse: impulso da colisão (N·s); dir: direção em que a arma empurrou.
        public void PhysicalImpact(float impulse, Vector3 dir)
        {
            if (Dead || impulse <= 0f || impactCd > 0f) return;   // a lâmina deslizando gera vários eventos seguidos
            impactCd = 0.15f;
            var flat = dir.Flat();
            if (flat.sqrMagnitude < 1e-4f) return;
            flat.Normalize();
            float mass = Def.Mass;
            knock = Vector3.ClampMagnitude(knock + flat * ImpactReaction.KnockSpeed(impulse, mass), ImpactReaction.MaxKnockSpeed);
            // Inclina o corpo para longe do golpe (em coordenadas locais do inimigo).
            var local = transform.InverseTransformDirection(flat);
            float kick = ImpactReaction.LeanKick(impulse, mass) * 12f;
            leanVel += new Vector2(local.z, -local.x) * kick;
            lastHitDir = flat; lastHitSpeed = Mathf.Max(lastHitSpeed, ImpactReaction.ImpactSpeed(impulse, mass));
            if (staggerCd <= 0f && ImpactReaction.Staggers(impulse, mass) && State.Posture.Enabled)
            {
                staggerCd = ImpactReaction.StaggerCooldown;
                State.ApplyStun(ImpactReaction.StaggerSeconds);
                OnStagger();
            }
        }

        protected virtual void OnStagger() { }

        void UpdateLean(float dt)
        {
            // Mola que endireita o corpo.
            leanVel += (-ImpactReaction.LeanStiffness * lean - ImpactReaction.LeanDamping * leanVel) * dt;
            lean += leanVel * dt;
            lean = Vector2.ClampMagnitude(lean, ImpactReaction.MaxLean);
            visualPivot.localRotation = Quaternion.Euler(lean.x * Mathf.Rad2Deg, 0f, lean.y * Mathf.Rad2Deg);
            lastHitSpeed = Mathf.MoveTowards(lastHitSpeed, 0f, dt * 4f);
        }

        // Corpo sólido para a arma física bater e parar (Fase 1). Cinemático: quem move o inimigo é a IA, não a física.
        void AddBodyCollider()
        {
            var col = gameObject.AddComponent<CapsuleCollider>();
            BodyCollider = col;
            col.radius = Def.Radius;
            col.height = Mathf.Max(bodyTop - bodyBottom + Def.Radius * 2f, Def.Radius * 2f);
            col.center = Vector3.up * (bodyBottom + bodyTop) * 0.5f;
            var rb = gameObject.AddComponent<Rigidbody>();
            Body = rb;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        protected abstract void BuildModel();
        protected abstract void Think(float dt, Vector3 toPlayer, float distance);

        protected virtual void OnDestroy() { ReleaseWeapon(); All.Remove(this); }

        void ReleaseWeapon()
        {
            var pl = Player;
            if (pl && pl.Weapon) pl.Weapon.OnEnemyRemoved(this);
        }

        public Vector3 Center => transform.position + Vector3.up * (bodyBottom + bodyTop) * 0.5f;
        public float Radius => Def.Radius;
        public void BodyAxis(out Vector3 a, out Vector3 b)
        {
            a = transform.position + Vector3.up * bodyBottom;
            b = transform.position + Vector3.up * bodyTop;
        }

        // ---------- Dano recebido ----------
        // Golpe da arma do jogador (chamado pela PlayerWeapon). Retorna true se contou.
        public bool TakeMeleeHit(float tipSpeed, float edge, Vector3 dir, WeaponDef weapon, PlayerState ps, float damageMultiplier = 1f)
        {
            if (Dead || hitCooldown > 0f || SpawnGrace > 0f) return false;
            bool hasSta = ps.SpendSwingStamina(weapon);
            float dmg = Combat.MeleeDamage(tipSpeed, weapon, ps.Has(SkillIds.FioAfiado), hasSta, edge) * damageMultiplier;
            if (dmg <= 0f) return false;
            hitCooldown = Combat.HitCooldown;
            ApplyDamage(dmg, 0.7f, dir, Combat.Knockback(tipSpeed, weapon));
            ArenaInput.Haptic(Mathf.Min(1f, 0.3f + tipSpeed / 8f) * (weapon.Weight > 5 ? 1f : 0.7f), 0.05f + weapon.Weight * 0.004f);
            return true;
        }

        public DamageEvent ApplyDamage(float amount, float postureFactor = 0.7f, Vector3? dir = null, float knockback = 0f,
                                       Color? color = null, bool burn = false, bool canCrit = true)
        {
            if (Dead || SpawnGrace > 0f) return default;
            var ev = State.TakeDamage(amount, postureFactor, canCrit, burn);
            if (dir.HasValue && knockback > 0f)
            {
                var d = dir.Value.Flat();
                if (d.sqrMagnitude > 1e-4f) { knock += d.normalized * knockback / Def.Mass; lastHitDir = d.normalized; lastHitSpeed = Mathf.Max(lastHitSpeed, knockback); }
            }
            var c = color ?? (ev.Crit ? new Color(1f, 0.83f, 0.42f) : Color.white);
            ArenaFx.FloatText(ev.Crit ? ev.Amount + "!" : ev.Amount.ToString(), Center + Vector3.up * (0.25f + bodyTop * 0.3f), c, ev.Crit ? 1.3f : 1f);
            if (ev.Burn) ArenaFx.FloatText("QUEIMA!", Center + Vector3.up * 0.6f, new Color(1f, 0.48f, 0.19f), 0.9f);
            ArenaFx.Burst(Center, bloodColor, ev.Crit ? 18 : 8, 3f);
            return ev;
        }

        void OnDamaged(DamageEvent ev)
        {
            hitFlash = 0.08f;
            if (ev.PostureBroke) ArenaFx.FloatText("POSTURA QUEBRADA", Center + Vector3.up * (0.5f + bodyTop * 0.3f), Color.white, 1.1f);
            OnHit(ev);
            if (ev.Killed) Die();
        }

        protected virtual void OnHit(DamageEvent ev) { }

        protected virtual void Die()
        {
            ArenaFx.Burst(Center, bloodColor, 20, 4f, 1.4f);
            if (ArenaGame.Instance) ArenaGame.Instance.OnEnemyKilled(this);
            ReleaseWeapon();   // solta a espada cravada antes de o corpo sumir
            if (LeavesCorpse && visualPivot)
            {
                visualPivot.SetParent(null, true);
                RagdollCorpse.Create(visualPivot, lastHitDir, lastHitSpeed);
            }
            Destroy(gameObject);
        }

        // ---------- Loop ----------
        protected virtual void Update()
        {
            if (Dead) return;
            float dt = Time.deltaTime;
            State.Tick(dt);
            hitCooldown -= dt;
            SpawnGrace -= dt;
            staggerCd -= dt; impactCd -= dt;
            if (State.Rooted > 0f || State.Frozen > 0f || Impaled) knock = Vector3.zero;   // preso: só inclina
            knock = Vector3.ClampMagnitude(knock, ImpactReaction.MaxKnockSpeed);
            UpdateLean(dt);
            knock *= Mathf.Exp(-4f * dt);
            transform.position = ArenaUtil.ClampToArena(transform.position + knock * dt);
            UpdateBars();
            UpdateFlash(dt);
            if (Player == null || State.Frozen > 0f) return;
            var to = (Player.FeetPosition - transform.position).Flat();
            Think(dt, to, to.magnitude);
        }

        protected void FacePlayer(Vector3 toPlayer, float dt, float speed = 10f)
        {
            if (toPlayer.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer), dt * speed);
        }

        protected void Move(Vector3 velocity, float dt)
        {
            if (!State.CanMove || Impaled) return;
            transform.position = ArenaUtil.ClampToArena(transform.position + velocity * State.SpeedMultiplier * dt);
        }

        void UpdateFlash(float dt)
        {
            hitFlash -= dt;
            Color tint = hitFlash > 0f ? Color.white : State.Frozen > 0f ? new Color(0.6f, 0.85f, 1f) : State.Rooted > 0f ? new Color(0.55f, 0.75f, 0.35f) : Color.clear;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].transform.IsChildOf(barRoot)) continue;
                renderers[i].material.color = tint == Color.clear ? baseColors[i] : Color.Lerp(baseColors[i], tint, 0.6f);
            }
        }

        // ---------- Barras de vida e postura ----------
        void BuildBars()
        {
            barRoot = new GameObject("Barras").transform;
            barRoot.SetParent(transform, false);
            barRoot.localPosition = Vector3.up * (bodyTop + 0.3f);
            float w = Mathf.Clamp(Def.Radius * 2f, 0.5f, 1.1f);
            ArenaUtil.Prim(PrimitiveType.Cube, barRoot, Vector3.zero, new Vector3(w + 0.04f, 0.12f, 0.01f), ArenaUtil.Unlit(new Color(0, 0, 0, 0.6f)));
            hpFill = MakeFill(barRoot, w, 0.03f, new Color(0.8f, 0.13f, 0.13f));
            if (State.Posture.Enabled) postureFill = MakeFill(barRoot, w, -0.03f, new Color(0.9f, 0.7f, 0.12f));
        }

        static Transform MakeFill(Transform parent, float w, float y, Color c)
        {
            var pivot = new GameObject("Fill").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(-w / 2f, y, -0.01f);
            ArenaUtil.Prim(PrimitiveType.Cube, pivot, new Vector3(w / 2f, 0, 0), new Vector3(w, 0.045f, 0.01f), ArenaUtil.Unlit(c));
            return pivot;
        }

        void UpdateBars()
        {
            var cam = Camera.main;
            if (cam) barRoot.rotation = Quaternion.LookRotation(barRoot.position - cam.transform.position);
            hpFill.localScale = new Vector3(State.HpFraction, 1, 1);
            if (postureFill)
            {
                postureFill.localScale = new Vector3(Mathf.Max(0.001f, State.Posture.Fraction), 1, 1);
                postureFill.GetComponentInChildren<Renderer>().material.color = State.Posture.IsBroken ? Color.white : new Color(0.9f, 0.7f, 0.12f);
            }
        }
    }
}
