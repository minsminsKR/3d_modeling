using UnityEngine;

namespace HappyToy.V2
{
    // Inventory and effect lifetime; presentation and enemy clocks have separate owners.
    [DefaultExecutionOrder(-180), DisallowMultipleComponent, RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerRitualInventory : MonoBehaviour
    {
        public int Incense { get; private set; } = 1;
        public int Fragments { get; private set; }
        public int Talismans { get; private set; }
        public bool Burning => burn.Burning;
        public float BurnedFraction => burn.BurnedFraction;
        public float Remaining => burn.Remaining;
        public Transform WorldRoot { get; private set; }
        public IncenseGuidance Smoke { get; private set; }
        readonly TalismanBurnClock burn = new TalismanBurnClock();
        PlayerMotor player;
        EnemyTimeStop stop;
        void Awake()
        {
            player = GetComponent<PlayerMotor>(); stop = GetComponent<EnemyTimeStop>();
            if (!stop) stop = gameObject.AddComponent<EnemyTimeStop>();
        }
        public void ConfigureWorld(Transform root)
        { EndEffect(); WorldRoot = root; Incense = 1; Fragments = Talismans = 0; if (Smoke) Destroy(Smoke.gameObject); }
        bool CanUse => isActiveAndEnabled && player && !player.Hidden && !player.Paused && GameSession.Current &&
            !GameSession.Current.Finished && !GameSession.Current.Shell.ItemWheelOpen;
        public bool CollectIncense()
        {
            if (!GameSession.Current || !GameSession.Current.InputAllowed || Incense == int.MaxValue) return false;
            Incense++; GameSession.Current.Notify("향을 주웠습니다. 연기가 남아 있는 기억을 가리킵니다."); return true;
        }
        public bool CollectFragment()
        {
            if (!GameSession.Current || !GameSession.Current.InputAllowed ||
                !TalismanStockRules.TryCollectFragment(Fragments,Talismans,out int fragments,out int talismans)) return false;
            bool completed = talismans > Talismans; Fragments = fragments; Talismans = talismans;
            GameSession.Current.Notify(completed ? "다섯 조각이 하나의 부적이 되었습니다." : "부적의 조각 "+Fragments+" / 5"); return true;
        }
        public bool UseIncense()
        {
            if (!CanUse || Incense <= 0 || Smoke) return false;
            Incense--; var root = new GameObject("Memory guiding incense");
            root.transform.SetParent(WorldRoot ? WorldRoot : GameSession.Current.transform,false);
            Smoke = root.AddComponent<IncenseGuidance>(); Smoke.Prepare(player);
            player.Hands?.PlayAction(PlayerItem.Incense); return true;
        }
        public bool UseTalisman()
        {
            if (!CanUse || GameSession.Current.EncountersResolved || Talismans <= 0 || !burn.Begin()) return false;
            Talismans--; stop.Begin(); player.Hands?.PlayAction(PlayerItem.Talisman);
            GameSession.Current.Notify("부적이 타는 동안 모든 괴물의 시간이 멈춥니다."); return true;
        }
        public void RestoreSmoke(float remaining)
        {
            if (Smoke) Destroy(Smoke.gameObject);
            var root=new GameObject("Restored memory guiding incense"); root.transform.SetParent(WorldRoot,false);
            Smoke=root.AddComponent<IncenseGuidance>(); Smoke.Prepare(player,remaining);
        }
        void Update()
        {
            var session = GameSession.Current;
            if (!session || session.Finished) { if (Burning) EndEffect(); return; }
            if (session.InputAllowed && burn.Tick(Time.deltaTime)) stop.End();
        }
        public void Restore(int incense,int fragments,int talismans)
        {
            if (incense < 0) throw new System.ArgumentOutOfRangeException(nameof(incense));
            TalismanStockRules.Validate(fragments,talismans);
            EndEffect(); Incense = incense; Fragments = fragments; Talismans = talismans;
        }
        public void EndEffect() { burn.Reset(); if (stop) stop.End(); }
        void OnDisable() { EndEffect(); }
    }
}
