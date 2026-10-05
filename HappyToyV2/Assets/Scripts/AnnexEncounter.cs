using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // An authored room encounter. The actor is visible but harmless during its warning.
    public sealed class AnnexEncounter : MonoBehaviour
    {
        public StalkerBrain monster;
        public Vector3 roomCenter;
        public float triggerRadius = 4.5f;
        public int activationStep = 1;
        bool chapterDriven;
        public void PrepareChapter()
        {
            chapterDriven=true; Cancelled=false; Triggered=Released=false; Phase="idle";
            activationStep=4; triggerRadius=7.5f; enabled=true;
            if(monster) monster.gameObject.SetActive(false);
        }
        public Light warningLight;
        public bool Triggered { get; private set; }
        public bool Released { get; private set; }
        public bool Cancelled { get; private set; }
        public string Phase { get; private set; } = "idle";
        public float RevealElapsed { get; private set; }
        public int PreparationContacts { get; private set; }
        EncounterRevealAudio revealAudio;
        AudioSource source;
        AudioClip cue;
        GameSession session;
        float originalIntensity;
        bool initialized;
        void Start()
        {
            session=GameSession.Current;
            if(warningLight)originalIntensity=warningLight.intensity;
            initialized=true;
            if(session)session.StoryChanged+=OnStory;
            if(monster)monster.gameObject.SetActive(false);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 1;
            source.minDistance = 2; source.maxDistance = 20;
            source.dopplerLevel = 0; source.ignoreListenerPause = false; source.ignoreListenerVolume = false;
            EnemyAcoustics.Bind(source, transform, .42f);
            cue = EncounterRevealAudio.CreateClip(EncounterRevealAudio.Cue.NurseryWhimper);

        }
        void Update()
        {
            if (!session) return;
            // Result screens pause time/input; cancellation must run before that gate.
            if(session.EncountersResolved){if(!Cancelled)CancelEncounter();return;}
            if(Cancelled||!monster||!session.InputAllowed||!session.player||session.player.Hidden)return;
            var delta = session.player.transform.position - roomCenter;
            if (Mathf.Abs(delta.y)>2) return; delta.y = 0;
            if (!Triggered && session.EncounterStep >= activationStep && delta.magnitude < triggerRadius)
                StartCoroutine(Reveal());
        }
        IEnumerator Reveal()
        {
            Triggered = true; Phase = "cry"; RevealElapsed = 0;
            GameSession.Current.Notify("지하의 물 너머에서 울음이 들립니다. 움직이기 전에 출구를 확인하세요.");
            source.PlayOneShot(cue);
            revealAudio = EncounterRevealAudio.Ensure(transform);
            session.WarnThreat("지하에서 울음이 들립니다 · 움직이기까지 5초, 출구를 확인하세요.", 4);
            var agent = monster.GetComponent<NavMeshAgent>();
            var motion = monster.GetComponent<V1MonsterMotion>();
            if(motion)motion.enabled = false;
            monster.enabled = false; agent.enabled = false;
            monster.gameObject.SetActive(true);
            var animation = monster.GetComponentInChildren<Animation>();
            if (animation && animation.GetClip("cry")) { animation["cry"].speed = .72f; animation.Play("cry"); }
            for (float t = 0; t < 5;)
            {
                if (!session || session.EncountersResolved || !monster)
                { CancelEncounter(); yield break; }
                // Settings pauses the reveal clock, but comfort changes must still
                // remove a frozen flicker frame immediately, as in the other reveals.
                bool soften = session.Shell && session.Shell.ReducedMotion;
                if (warningLight && (session.InputAllowed || soften))
                    warningLight.intensity = soften ? originalIntensity :
                        originalIntensity * (.62f + .38f * Mathf.SmoothStep(0, 1, t / 5));
                if (session.InputAllowed)
                {
                    if (t >= 2.7f && Phase == "cry")
                    {
                        Phase = "stillness"; source.Stop();
                        if (animation && animation.GetClip("cry")) animation["cry"].speed = 0;
                    }
                    if (t >= 4.1f && Phase == "stillness")
                    {
                        Phase = "crawlReady";
                        if (animation && animation.GetClip("patrol"))
                        { animation["patrol"].speed = .35f; animation.CrossFade("patrol", .25f); }
                        var contact = monster.transform.position + monster.transform.forward * .2f;
                        revealAudio.Play(EncounterRevealAudio.Cue.NurseryContact, contact, "[물이 철벅이며 울음이 멎는다]");
                        WaterSurfaceFeedback.ReportEventContact(contact, .55f); PreparationContacts++;
                    }
                    t += Time.deltaTime; RevealElapsed = Mathf.Min(5, t);
                }
                yield return null;
            }
            RestoreLight();
            if (!NavMesh.SamplePosition(monster.transform.position, out var hit, 1.5f, agent.areaMask) ||
                !EnemyNavigation.SameFloor(hit.position, roomCenter.y))
            { Debug.LogError("Nursery actor has no reachable floor"); CancelEncounter(); yield break; }
            monster.transform.position = hit.position; agent.enabled = true; monster.enabled = true; if(motion)motion.enabled = true;
            Phase = "released"; Released = true;
        }
        void RestoreLight(){if(initialized&&warningLight)warningLight.intensity=originalIntensity;}
        void OnStory(int step){if(!chapterDriven&&step>=4)CancelEncounter();}
        void CancelEncounter()
        {
            Cancelled=true;Phase="resolved";StopAllCoroutines();
            if(revealAudio)revealAudio.Stop();
            if(monster)monster.gameObject.SetActive(false);
            if(source)source.Stop();RestoreLight();
        }
        void OnDisable(){if(initialized)CancelEncounter();}
        void OnDestroy()
        {
            if(session)session.StoryChanged-=OnStory;
            RestoreLight();if(cue)Destroy(cue);
        }
    }
}
