using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace HappyToy.V2
{
    // Samples both full V1 clips against the authored opening envelope, plus actual leaf travel.
    // This does not replace arbitrary-path body/wall intersection testing.
    public sealed class DoorClearanceAudit:MonoBehaviour
    {
        string output;
        [Serializable]class Result{public float left,right,height,opening;public int samples,doors;public bool bothLeavesMoved,meshFits,edgeCloseProtected;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-doorclearance-output");if(i<0||i+1>=args.Length)return;Application.runInBackground=true;new GameObject("Door clearance audit").AddComponent<DoorClearanceAudit>().output=args[i+1];}
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1);
            var doors=FindObjectsByType<Interactable>(FindObjectsSortMode.None).Where(d=>d.kind==Interactable.Kind.Door).ToArray();
            var start=doors.Select(d=>new[]{d.movingLeaf.localPosition,d.secondaryLeaf.localPosition}).ToArray();
            foreach(var door in doors)door.Use(GameSession.Current.player);yield return new WaitForSecondsRealtime(1);
            var result=new Result{opening=doors.Min(d=>d.obstacle.size.x),doors=doors.Length,bothLeavesMoved=true};
            for(int i=0;i<doors.Length;i++)result.bothLeavesMoved&=Vector3.Distance(start[i][0],doors[i].movingLeaf.localPosition)>1.2f&&Vector3.Distance(start[i][1],doors[i].secondaryLeaf.localPosition)>1.2f;
            var player=GameSession.Current.player;var cc=player.GetComponent<CharacterController>();var saved=player.transform.position;
            cc.enabled=false;player.transform.position=doors[0].transform.position+Vector3.right;
            var openedPosition=doors[0].movingLeaf.localPosition;doors[0].Use(player);yield return new WaitForSecondsRealtime(.15f);
            result.edgeCloseProtected=Vector3.Distance(openedPosition,doors[0].movingLeaf.localPosition)<.01f;
            player.transform.position=saved;cc.enabled=true;
            var brain=FindFirstObjectByType<StoryDirector>().stalker;brain.gameObject.SetActive(true);brain.enabled=false;
            var motion=brain.GetComponent<V1MonsterMotion>();motion.enabled=false;var anim=motion.animationPlayer;anim.Stop();
            var skins=motion.model.GetComponentsInChildren<SkinnedMeshRenderer>();var baked=new Mesh();
            foreach(var name in new[]{"patrol","chase"})for(int frame=0;frame<64;frame++)
            {
                anim[name].clip.SampleAnimation(motion.model.gameObject,anim[name].length*frame/64);
                float low=float.MaxValue,high=float.MinValue;
                foreach(var skin in skins)
                {
                    skin.BakeMesh(baked,true);foreach(var v in baked.vertices)
                    {
                        var p=brain.transform.InverseTransformPoint(skin.transform.TransformPoint(v));
                        result.left=Mathf.Min(result.left,p.x);result.right=Mathf.Max(result.right,p.x);low=Mathf.Min(low,p.y);high=Mathf.Max(high,p.y);
                    }
                }
                result.height=Mathf.Max(result.height,high-low);result.samples++;
            }
            result.meshFits=result.left>-result.opening/2+.015f&&result.right<result.opening/2-.015f&&result.height<2.48f;
            File.WriteAllText(Path.Combine(output,"clearance.json"),JsonUtility.ToJson(result,true));Destroy(baked);
            Application.Quit(result.meshFits&&result.bothLeavesMoved&&result.edgeCloseProtected&&result.doors==3?0:1);
        }
    }
}
