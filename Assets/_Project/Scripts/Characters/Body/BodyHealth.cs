using System;
using UnityEngine;

namespace FantaLandia.Characters.Body
{
    public enum DamageType { Blunt, Cut, Fall }

    public enum PartState { Healthy, Injured, Disabled, Severed, Prosthetic }

    /// <summary>
    /// Salute per parte del corpo. Ogni segmento ha i suoi punti vita:
    ///  - sotto il 50%: ferito; a 0 o meno: inutilizzabile; colpi da taglio molto forti staccano gli arti
    ///  - testa, collo o torace a 0: svenimento (ci si risveglia quando si sono ripresi)
    ///  - gambe ferite: zoppia e rallentamento; braccio inutilizzabile: pende inerte
    ///  - magia: cura veloce (non ricrea gli arti); protesi meccaniche: rimpiazzano gli arti staccati
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public class BodyHealth : MonoBehaviour
    {
        [Serializable]
        public class PartHealth
        {
            public BodyPart part;
            public float max;
            public float hp;
            public bool severed;
            public bool prosthetic;
        }

        [Header("Rigenerazione naturale (punti al secondo)")]
        public float regen = 0.4f;
        [Tooltip("Rigenerazione di testa, collo e torace mentre si è svenuti")]
        public float unconsciousRegen = 1.5f;
        public float minUnconsciousTime = 5f;
        [Tooltip("Frazione di salute di testa e torace necessaria per risvegliarsi")]
        public float wakeThreshold = 0.25f;

        [Header("Cadute")]
        public float safeFallHeight = 2.5f;
        public float fallDamagePerMeter = 16f;

        [Header("Protesi")]
        [Tooltip("Efficienza di un arto meccanico rispetto a uno sano")]
        public float prostheticEfficiency = 0.85f;

        public PartHealth[] parts;
        public bool IsUnconscious { get; private set; }

        public event Action<BodyPart, float, DamageType> Damaged;
        public event Action<BodyPart> PartSevered;
        public event Action KnockedOut;
        public event Action WokeUp;

        BodyRig rig;
        CharacterMotor motor;
        float unconsciousTime;

        public static float DefaultMax(BodyPart p)
        {
            switch (p)
            {
                case BodyPart.Head: return 50f;
                case BodyPart.Neck: return 30f;
                case BodyPart.Chest: return 100f;
                case BodyPart.Abdomen: return 80f;
                case BodyPart.Pelvis: return 70f;
                case BodyPart.UpperArmL: case BodyPart.UpperArmR: return 50f;
                case BodyPart.ForearmL: case BodyPart.ForearmR: return 40f;
                case BodyPart.HandL: case BodyPart.HandR: return 25f;
                case BodyPart.ThighL: case BodyPart.ThighR: return 60f;
                case BodyPart.ShinL: case BodyPart.ShinR: return 50f;
                default: return 30f; // piedi
            }
        }

        public static bool IsLimb(BodyPart p) => p >= BodyPart.UpperArmL;
        public static bool IsVital(BodyPart p) => p == BodyPart.Head || p == BodyPart.Neck || p == BodyPart.Chest;

        void Awake()
        {
            rig = GetComponentInChildren<BodyRig>();
            motor = GetComponent<CharacterMotor>();
            parts = new PartHealth[BodyRig.PartCount];
            for (int i = 0; i < parts.Length; i++)
            {
                var p = (BodyPart)i;
                parts[i] = new PartHealth { part = p, max = DefaultMax(p), hp = DefaultMax(p) };
            }
        }

        void OnEnable() { if (motor != null) motor.Landed += OnLanded; }
        void OnDisable() { if (motor != null) motor.Landed -= OnLanded; }

        public PartHealth Get(BodyPart p) => parts[(int)p];

        public PartState State(BodyPart p)
        {
            var h = Get(p);
            if (h.severed) return PartState.Severed;
            if (h.prosthetic) return PartState.Prosthetic;
            if (h.hp <= 0f) return PartState.Disabled;
            if (h.hp < h.max * 0.5f) return PartState.Injured;
            return PartState.Healthy;
        }

        /// <summary>Funzionalità della parte, 0..1 (staccata = 0, protesi = efficienza della protesi).</summary>
        public float Condition(BodyPart p)
        {
            var h = Get(p);
            if (h.severed) return 0f;
            float c = Mathf.Clamp01(h.hp / h.max);
            return h.prosthetic ? c * prostheticEfficiency : c;
        }

        // ------------------------------------------------------------------ danni

        public void Damage(BodyPart part, float amount, DamageType type)
        {
            var h = Get(part);
            if (h.severed || amount <= 0f) return;

            h.hp = Mathf.Max(-h.max, h.hp - amount);
            Damaged?.Invoke(part, amount, type);

            // Un colpo da taglio molto forte (o un colpo enorme) stacca l'arto.
            bool heavy = type == DamageType.Cut || amount >= h.max * 0.8f;
            if (IsLimb(part) && heavy && h.hp <= -h.max * 0.5f)
                Sever(part, (UnityEngine.Random.insideUnitSphere + Vector3.up) * 1.5f);
        }

        /// <summary>Stacca una parte e tutte quelle sotto di lei (es. avambraccio + mano).</summary>
        public void Sever(BodyPart part, Vector3 impulse)
        {
            if (Get(part).severed) return;
            if (rig != null) rig.Sever(part, impulse);
            foreach (BodyPart child in PartsBelow(part))
            {
                var h = Get(child);
                h.severed = true;
                h.prosthetic = false;
                h.hp = -h.max;
            }
            PartSevered?.Invoke(part);
        }

        void OnLanded(float height)
        {
            if (height <= safeFallHeight) return;
            float dmg = (height - safeFallHeight) * fallDamagePerMeter;
            // 40% piedi, 40% stinchi, 20% cosce, divisi tra le due gambe
            Damage(BodyPart.FootL, dmg * 0.2f, DamageType.Fall);
            Damage(BodyPart.FootR, dmg * 0.2f, DamageType.Fall);
            Damage(BodyPart.ShinL, dmg * 0.2f, DamageType.Fall);
            Damage(BodyPart.ShinR, dmg * 0.2f, DamageType.Fall);
            Damage(BodyPart.ThighL, dmg * 0.1f, DamageType.Fall);
            Damage(BodyPart.ThighR, dmg * 0.1f, DamageType.Fall);
        }

        // ------------------------------------------------------------------ cure

        /// <summary>Cura magica: distribuisce i punti partendo dalle parti messe peggio. Non ricrea gli arti staccati.</summary>
        public void HealMagic(float amount)
        {
            while (amount > 0.01f)
            {
                PartHealth worst = null;
                float worstRatio = 1f;
                foreach (var h in parts)
                {
                    if (h.severed || h.hp >= h.max) continue;
                    float r = h.hp / h.max;
                    if (r < worstRatio) { worstRatio = r; worst = h; }
                }
                if (worst == null) return;
                float add = Mathf.Min(amount, worst.max - worst.hp, 10f);
                worst.hp += add;
                amount -= add;
            }
        }

        /// <summary>Monta una protesi meccanica al posto di una parte staccata (e di quelle sotto).</summary>
        public bool AttachProsthetic(BodyPart part)
        {
            if (!Get(part).severed) return false;
            foreach (BodyPart child in PartsBelow(part))
            {
                var h = Get(child);
                if (!h.severed) continue;
                h.severed = false;
                h.prosthetic = true;
                h.hp = h.max;
            }
            if (rig != null) rig.AttachProsthetic(part);
            return true;
        }

        /// <summary>Monta protesi su tutti gli arti staccati.</summary>
        public void AttachAllProsthetics()
        {
            for (int i = 0; i < parts.Length; i++)
            {
                var p = (BodyPart)i;
                if (!parts[i].severed) continue;
                BodyPart? parent = ParentPart(p);
                if (parent == null || !Get(parent.Value).severed) AttachProsthetic(p);
            }
        }

        /// <summary>Debug: tutto come nuovo (arti compresi, protesi rimosse).</summary>
        public void RestoreAll()
        {
            foreach (var h in parts) { h.hp = h.max; h.severed = false; h.prosthetic = false; }
            if (rig != null) rig.RestoreAll();
        }

        // ------------------------------------------------------------------ ciclo

        void Update()
        {
            float dt = Time.deltaTime;

            // rigenerazione naturale (gli arti staccati non ricrescono)
            foreach (var h in parts)
            {
                if (h.severed || h.hp >= h.max) continue;
                float r = IsUnconscious && IsVital(h.part) ? unconsciousRegen : regen;
                h.hp = Mathf.Min(h.max, h.hp + r * dt);
            }

            // svenimento / risveglio
            bool vitalDown = Get(BodyPart.Head).hp <= 0f || Get(BodyPart.Neck).hp <= 0f || Get(BodyPart.Chest).hp <= 0f;
            if (!IsUnconscious && vitalDown)
            {
                IsUnconscious = true;
                unconsciousTime = 0f;
                motor.SetIncapacitated(true);
                KnockedOut?.Invoke();
            }
            else if (IsUnconscious)
            {
                unconsciousTime += dt;
                bool recovered = Condition(BodyPart.Head) >= wakeThreshold && Get(BodyPart.Neck).hp > 0f &&
                                 Condition(BodyPart.Chest) >= wakeThreshold;
                if (unconsciousTime >= minUnconsciousTime && recovered)
                {
                    IsUnconscious = false;
                    motor.SetIncapacitated(false);
                    WokeUp?.Invoke();
                }
            }

            // limiti di movimento dovuti alle ferite
            float legL = LegCondition(-1), legR = LegCondition(1);
            float worst = Mathf.Min(legL, legR), best = Mathf.Max(legL, legR);
            float mult = worst > 0f ? Mathf.Lerp(0.35f, 1f, worst) : Mathf.Max(0.15f, 0.3f * best);
            float core = Mathf.Min(Condition(BodyPart.Abdomen), Condition(BodyPart.Pelvis));
            mult = core > 0f ? mult * Mathf.Lerp(0.6f, 1f, core) : Mathf.Min(mult, 0.2f);
            bool canSprint = worst > 0.5f && core > 0.3f;
            motor.SetHealthLimits(mult, canSprint);
        }

        // ------------------------------------------------------------------ letture per animazione e UI

        public float LegCondition(int side)
        {
            BodyPart a = side < 0 ? BodyPart.ThighL : BodyPart.ThighR;
            return Mathf.Min(Condition(a), Mathf.Min(Condition(a + 1), Condition(a + 2)));
        }

        public float ArmCondition(int side)
        {
            BodyPart a = side < 0 ? BodyPart.UpperArmL : BodyPart.UpperArmR;
            return Mathf.Min(Condition(a), Mathf.Min(Condition(a + 1), Condition(a + 2)));
        }

        /// <summary>Braccio inutilizzabile: braccio o avambraccio a 0 (o staccati senza protesi).</summary>
        public bool ArmDisabled(int side)
        {
            BodyPart a = side < 0 ? BodyPart.UpperArmL : BodyPart.UpperArmR;
            return Condition(a) <= 0f || Condition(a + 1) <= 0f;
        }

        /// <summary>Lato della gamba che zoppica (-1, +1) o 0 se si cammina bene.</summary>
        public int LimpSide
        {
            get
            {
                float l = LegCondition(-1), r = LegCondition(1);
                if (Mathf.Abs(l - r) < 0.15f && Mathf.Min(l, r) > 0.6f) return 0;
                return l < r ? -1 : 1;
            }
        }

        public float LimpAmount => LimpSide == 0 ? 0f : 1f - Mathf.Min(LegCondition(-1), LegCondition(1));

        // ------------------------------------------------------------------ gerarchia delle parti

        /// <summary>La parte stessa e tutte quelle attaccate sotto di lei.</summary>
        public System.Collections.Generic.IEnumerable<BodyPart> PartsBelow(BodyPart part)
        {
            yield return part;
            if (!IsLimb(part)) yield break;
            // braccio: UpperArm -> Forearm -> Hand; gamba: Thigh -> Shin -> Foot
            int chainStart = part <= BodyPart.HandR ? (part <= BodyPart.HandL ? (int)BodyPart.UpperArmL : (int)BodyPart.UpperArmR)
                                                    : (part <= BodyPart.FootL ? (int)BodyPart.ThighL : (int)BodyPart.ThighR);
            for (int i = (int)part + 1; i < chainStart + 3; i++) yield return (BodyPart)i;
        }

        static BodyPart? ParentPart(BodyPart p)
        {
            switch (p)
            {
                case BodyPart.ForearmL: return BodyPart.UpperArmL;
                case BodyPart.HandL: return BodyPart.ForearmL;
                case BodyPart.ForearmR: return BodyPart.UpperArmR;
                case BodyPart.HandR: return BodyPart.ForearmR;
                case BodyPart.ShinL: return BodyPart.ThighL;
                case BodyPart.FootL: return BodyPart.ShinL;
                case BodyPart.ShinR: return BodyPart.ThighR;
                case BodyPart.FootR: return BodyPart.ShinR;
                default: return null;
            }
        }
    }
}
