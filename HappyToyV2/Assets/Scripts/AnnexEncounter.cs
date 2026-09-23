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
        public Light warningLight;
        public bool Triggered { get; private set; }
        public bool Released { get; private set; }
        public bool Cancelled { get; private set; }
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
            monster.gameObject.SetActive(false);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 1;
            source.minDistance = 2; source.maxDistance = 20;
            const int rate = 24000;
            var samples = new float[rate * 3];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Sin(Mathf.PI * t / 3) * (.55f + .45f * Mathf.Sin(t * 7));
                samples[i] = .15f * envelope * (Mathf.Sin(t * 2400 + 65 * Mathf.Sin(t * 4)) + .3f * Mathf.Sin(t * 3600));
            }
            cue = AudioClip.Create("Nursery warning", samples.Length, 1, rate, false); cue.SetData(samples, 0);
        }
        void Update()
        {
            if (!session) return;
            // Result screens pause time/input; cancellation must run before that gate.
            if(session.Finished||session.StoryStep>=4){if(!Cancelled)CancelEncounter();return;}
            if(Cancelled||!session.InputAllowed||session.player.Hidden)return;
            var delta = session.player.transform.position - roomCenter;
            if (Mathf.Abs(delta.y)>2) return; delta.y = 0;
            if (!Triggered && session.StoryStep >= 1 && delta.magnitude < triggerRadius)
                StartCoroutine(Reveal());
        }
        IEnumerator Reveal()
        {
            Triggered = true;
            GameSession.Current.Notify("지하의 물 너머에서 울음이 들립니다. 움직이기 전에 출구를 확인하세요.");
            source.PlayOneShot(cue);
            var agent = monster.GetComponent<NavMeshAgent>();
            var motion = monster.GetComponent<V1MonsterMotion>();
            motion.enabled = false;
            monster.enabled = false; agent.enabled = false;
            monster.gameObject.SetActive(true);
            var animation = monster.GetComponentInChildren<Animation>();
            if (animation && animation.GetClip("cry")) animation.Play("cry");
            for (float t = 0; t < 5; t += Time.deltaTime)
            {
                if (GameSession.Current.Finished || GameSession.Current.StoryStep >= 4)
                { CancelEncounter(); yield break; }
                if (warningLight) warningLight.intensity = originalIntensity * (.45f + .55f * Mathf.Abs(Mathf.Sin(t * 9)));
                yield return null;
            }
            RestoreLight();
            if (!NavMesh.SamplePosition(monster.transform.position, out var hit, 2, NavMesh.AllAreas))
            { Debug.LogError("Nursery actor has no reachable floor"); CancelEncounter(); yield break; }
            monster.transform.position = hit.position; agent.enabled = true; monster.enabled = true; motion.enabled = true;
            Released = true;
        }
        void RestoreLight(){if(initialized&&warningLight)warningLight.intensity=originalIntensity;}
        void OnStory(int step){if(step>=4)CancelEncounter();}
        void CancelEncounter()
        {
            Cancelled=true;StopAllCoroutines();
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
