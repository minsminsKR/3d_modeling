using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HappyToy.V2
{
    // Real CharacterController movement and E-key selection; never teleports or calls Use/Collect.
    public sealed class WalkAudit : MonoBehaviour
    {
        string output, failure="";PlayerMotor player;Keyboard keyboard;Mouse mouse;
        int maximumActiveEnemies;
        void Update(){maximumActiveEnemies=Mathf.Max(maximumActiveEnemies,FindObjectsByType<StalkerBrain>(FindObjectsSortMode.None).Count(e=>e.enabled));}
        readonly List<string> actions=new List<string>();
        [Serializable] class Result {public string failure;public string[] actions;public int storyStep;public bool escaped,hwacatCompleted;public Vector3 position;public int movementUpdates,attacks,maximumActiveEnemies;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-walk-output");if(i<0||i+1>=args.Length)return;
            Application.runInBackground=true;new GameObject("Walking audit").AddComponent<WalkAudit>().output=args[i+1];
        }
        IEnumerator Start()
        {
            // The audit runs in a hidden window: keep its synthetic devices enabled without focus.
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            Directory.CreateDirectory(output);keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
            yield return new WaitForSecondsRealtime(1);player=GameSession.Current.player;
            var items=FindObjectsByType<Interactable>(FindObjectsSortMode.None);
            var roomNames=new[]{"CLASSROOM","WASHROOM","INFIRMARY","CLASSROOM"};
            var ids=new[]{"register","ribbon","record","restore"};
            var centers=new[]{new Vector3(-4.5f,0,0),new Vector3(-4.5f,0,0),new Vector3(5.5f,0,0),new Vector3(-4.5f,0,0)};
            var opened=new HashSet<string>();
            // Prepare the retreat route before disturbing the ribbon; this uses the same E input.
            yield return Walk(new Vector3(5.5f,0,0));
            if(failure=="")
            {
                var prepared=items.First(i=>i.kind==Interactable.Kind.Door&&i.name.StartsWith("INFIRMARY"));
                yield return PressE(prepared,prepared.movingLeaf.GetComponent<Collider>().bounds.center);
                yield return new WaitForSecondsRealtime(1.4f);opened.Add("INFIRMARY");
            }
            for(int stage=0;stage<4&&failure=="";stage++)
            {
                yield return Walk(centers[stage]);if(failure!="")break;
                var door=items.First(i=>i.kind==Interactable.Kind.Door&&i.name.StartsWith(roomNames[stage]));
                if(opened.Add(roomNames[stage]))
                {yield return PressE(door,door.movingLeaf.GetComponent<Collider>().bounds.center);yield return new WaitForSecondsRealtime(1.4f);}
                if(failure!="")break;
                if(stage==2)
                {
                    yield return Walk(new Vector3(5.5f,0,2.9f));
                    var latch=door.GetComponentsInChildren<Collider>().First(c=>c.name.EndsWith("door latch"));
                    if(failure=="")yield return PressE(door,latch.bounds.center+Vector3.forward*.16f);
                    yield return new WaitForSecondsRealtime(1.3f);
                    if(failure!="")break;
                }
                var item=items.First(i=>i.stableId==ids[stage]);
                if(!Approach(item,out var approach,out var aim)){failure="No approach: "+ids[stage];break;}
                yield return Walk(approach);if(failure!="")break;
                // Rest before waking the room's second threat, not while it transforms behind us.
                if(stage==2){while(player.Stamina<.98f&&!GameSession.Current.Finished)yield return null;actions.Add("Recovered stamina before medical record");}
                yield return PressE(item,aim);
                if(failure!="")break;
                if(GameSession.Current.StoryStep!=stage+1){failure="Story did not advance: "+ids[stage];break;}
                actions.Add("Investigated "+ids[stage]);Save();
                if(stage==2)
                {
                    yield return Walk(new Vector3(5.5f,0,3.3f));
                    var latch=door.GetComponentsInChildren<Collider>().First(c=>c.name.EndsWith("door latch"));
                    var enemy=FindFirstObjectByType<StoryDirector>().stalker;int attacksBefore=enemy.AttacksStarted;
                    if(failure=="")yield return PressE(door,latch.bounds.center+Vector3.forward*.16f);
                    yield return new WaitForSecondsRealtime(.7f);
                    float waitUntil=Time.realtimeSinceStartup+1.25f;
                    while(enemy.AttacksStarted==attacksBefore&&!GameSession.Current.Finished&&Time.realtimeSinceStartup<waitUntil)yield return null;
                    if(enemy.AttacksStarted>attacksBefore&&!GameSession.Current.Finished)
                    {yield return Walk(new Vector3(6.9f,0,3.5f));yield return new WaitForSecondsRealtime(.55f);actions.Add("Sidestepped and waited for strike to miss");}
                }
                yield return Walk(centers[stage]);
            }
            if(failure=="")
            {
                var exit=items.First(i=>i.kind==Interactable.Kind.Exit);
                if(Approach(exit,out var p,out var aim)){yield return Walk(p);if(failure=="")yield return PressE(exit,aim);}
                else failure="No exit approach";
            }
            Save();Application.Quit(failure==""&&GameSession.Current.Escaped?0:1);
        }
        void Save()=>File.WriteAllText(Path.Combine(output,"walk.json"),JsonUtility.ToJson(new Result{failure=failure,actions=actions.ToArray(),storyStep=GameSession.Current.StoryStep,escaped=GameSession.Current.Escaped,position=player.transform.position,movementUpdates=player.MovementUpdates,attacks=FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include,FindObjectsSortMode.None).Sum(e=>e.AttacksStarted),maximumActiveEnemies=maximumActiveEnemies,hwacatCompleted=FindFirstObjectByType<V1HwacatEvent>()?.Completed??false},true));
        IEnumerator Walk(Vector3 destination)
        {
            if(GameSession.Current.Finished){failure="Caught during movement";yield break;}
            var path=new NavMeshPath();
            if(!NavMesh.CalculatePath(player.transform.position,destination,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
            {failure="No complete route to "+destination;yield break;}
            foreach(var corner in path.corners.Skip(1))
            {
                float until=Time.realtimeSinceStartup+25;
                while(true)
                {
                    var delta=corner-player.transform.position;delta.y=0;
                    if(delta.magnitude<.13f)break;
                    if(GameSession.Current.Finished){failure="Caught during movement";break;}
                    if(Time.realtimeSinceStartup>until){failure="Movement blocked at "+player.transform.position+" toward "+corner;break;}
                    player.transform.rotation=Quaternion.LookRotation(delta);
                    bool sprint=GameSession.Current.StoryStep>=2&&GameSession.Current.StoryStep<4;
                    InputSystem.QueueStateEvent(keyboard,sprint?new KeyboardState(Key.W,Key.LeftShift):new KeyboardState(Key.W));
                    yield return null;
                }
                if(failure!="")break;
            }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.08f);
        }
        IEnumerator PressE(Interactable item,Vector3 aim)
        {
            var delta=aim-player.eyes.transform.position;var flat=new Vector3(delta.x,0,delta.z);
            player.transform.rotation=Quaternion.LookRotation(flat);
            float desired=-Mathf.Atan2(delta.y,flat.magnitude)*Mathf.Rad2Deg;
            float current=Mathf.DeltaAngle(0,player.eyes.transform.localEulerAngles.x);
            InputSystem.QueueDeltaStateEvent(mouse.delta,new Vector2(0,-Mathf.DeltaAngle(current,desired)/player.sensitivity));
            yield return new WaitForSecondsRealtime(.1f);
            if(player.Focus!=item){Physics.Raycast(player.eyes.transform.position,player.eyes.transform.forward,out var obstruction,2.2f);failure="Actual interaction ray missed "+item.name+"; hit="+(obstruction.collider?obstruction.collider.name:"none")+"; angle="+Vector3.Angle(player.eyes.transform.forward,aim-player.eyes.transform.position)+"; aim="+aim+"; camera="+player.eyes.transform.position;yield break;}
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));yield return new WaitForSecondsRealtime(.06f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.06f);
            actions.Add("E "+item.name);
        }
        bool Approach(Interactable item,out Vector3 approach,out Vector3 aim)
        {
            foreach(var collider in item.GetComponentsInChildren<Collider>())
            {
                aim=collider.bounds.center;
                for(int i=0;i<96;i++)
                {
                    float a=i%32*Mathf.PI/16,r=.65f+i/32*.45f;
                    var test=new Vector3(aim.x+Mathf.Cos(a)*r,.1f,aim.z+Mathf.Sin(a)*r);
                    if(!NavMesh.SamplePosition(test,out var sample,.3f,NavMesh.AllAreas))continue;
                    var q=sample.position;
                    if(Physics.CheckCapsule(q+Vector3.up*.4f,q+Vector3.up*1.4f,.3f,~0,QueryTriggerInteraction.Ignore))continue;
                    var delta=aim-(q+Vector3.up*1.6f);
                    if(delta.magnitude>2.2f||!Physics.Raycast(q+Vector3.up*1.6f,delta.normalized,out var hit,2.2f)||hit.collider.GetComponentInParent<Interactable>()!=item)continue;
                    var path=new NavMeshPath();if(!NavMesh.CalculatePath(player.transform.position,q,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                    approach=q;return true;
                }
            }
            approach=aim=Vector3.zero;return false;
        }
    }
}
