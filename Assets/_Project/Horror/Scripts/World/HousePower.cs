using FantaLandia.Horror.Interaction;
using UnityEngine;

namespace FantaLandia.Horror.World
{
    /// <summary>
    /// Il contatore della casa: accende o spegne tutto insieme (luci, interruttori, TV, lampadine accese).
    /// Serve per il blackout del quartiere e per il generatore.
    /// </summary>
    public class HousePower : MonoBehaviour
    {
        public Light[] lights;
        public LightSwitch[] switches;
        public CrtTv[] tvs;
        [Tooltip("Lampadine e paralumi luminosi: si spengono senza corrente")]
        public Renderer[] glowRenderers;
        public bool powered = true;

        public event System.Action<bool> PowerChanged;

        void Start() => Apply();

        public void SetPower(bool on)
        {
            if (powered == on) return;
            powered = on;
            Apply();
            PowerChanged?.Invoke(on);
        }

        void Apply()
        {
            foreach (var l in lights) if (l != null) l.enabled = powered;
            foreach (var s in switches) if (s != null) s.SetPowered(powered);
            foreach (var t in tvs) if (t != null) t.powered = powered;
            foreach (var r in glowRenderers) if (r != null) r.enabled = powered;
        }
    }
}
