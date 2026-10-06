using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    public sealed partial class MemoryChapter
    {
        Interactable[] ChapterItems(Interactable.Kind kind) => FindObjectsByType<Interactable>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(x=>x.gameObject.scene==gameObject.scene && x.kind==kind)
            .OrderBy(x=>HierarchyKey(x.transform),StringComparer.Ordinal).ToArray();
        static string HierarchyKey(Transform node) => node.parent ? HierarchyKey(node.parent)+"/"+node.GetSiblingIndex().ToString("D5") : node.GetSiblingIndex().ToString("D5");
        // Authoring leaves many door IDs empty. Deterministic hierarchy IDs exist only at runtime.
        void EnsureCheckpointIdentities()
        {
            var doors=ChapterItems(Interactable.Kind.Door);
            for(int i=0;i<doors.Length;i++) doors[i].stableId="school-door-"+i;
            var packs=ChapterItems(Interactable.Kind.FirecrackerSupply);
            for(int i=0;i<packs.Length;i++) packs[i].stableId="school-supply-"+i;
        }
        public string SuspendBlockReason
        {
            get
            {
                if(!Ready || session.Finished || session.Shell.Screen!=GameShell.Page.Pause) return "일시정지 메뉴에서 학교 탐색을 저장하세요.";
                if(session.player.Hidden) return "은신처에서 나온 뒤 학교 탐색을 저장할 수 있습니다.";
                if(Portrait.Triggered && !Portrait.Completed || Nursery.Triggered && !Nursery.Released ||
                    Mannequin.Triggered && !Mannequin.Released || Mask.IntroStarted && !Mask.IntroCompleted ||
                    Mask.State==LanternMaskEncounter.Phase.Transforming)
                    return "등장이나 변신이 끝난 뒤 다시 일시정지해 저장하세요.";
                if(Cyclopse.AttackActive || Portrait.angry.AttackActive || Nursery.monster.AttackActive || Mannequin.AttackActive || Mask.AttackActive)
                    return "공격 동작이 끝난 뒤 다시 일시정지해 저장하세요.";
                if(FindObjectsByType<FirecrackerProjectile>(FindObjectsSortMode.None).Any(x=>x.gameObject.scene==gameObject.scene))
                    return "던진 폭죽의 효과가 끝난 뒤 저장하세요.";
                var cue=session.player.GetComponent<DetectionFeedback>();
                return cue && cue.Active?"인식 연출이 끝난 뒤 다시 일시정지해 저장하세요.":"";
            }
        }
        public ChapterCheckpoint CaptureCheckpoint()
        {
            if(!string.IsNullOrEmpty(SuspendBlockReason)) throw new InvalidOperationException(SuspendBlockReason);
            EnsureCheckpointIdentities();
            var data=new ChapterCheckpoint { lightingVersion=1,lighting=Lighting.Capture(), token=Guid.NewGuid().ToString("N"),scene=gameObject.scene.path,recovered=Recovered,
                seconds=session.ElapsedPlayTime,player=session.player.CaptureProgress(),cyclopse=Cyclopse.CaptureProgress(),
                portraitActor=Portrait.angry.CaptureProgress(),nurseryActor=Nursery.monster.CaptureProgress(),
                mannequin=Mannequin.CaptureChapterProgress(),mask=Mask.CaptureChapterProgress(),
                portraitCompleted=Portrait.Completed,portraitWitnessed=Portrait.ChapterWitnessed,nurseryReleased=Nursery.Released,
                doors=ChapterItems(Interactable.Kind.Door).Select(x=>new ChapterCheckpoint.Door { id=x.stableId,open=x.IsOpen,
                    leaf=x.movingLeaf.localPosition,hasSecondary=x.secondaryLeaf,secondary=x.secondaryLeaf?x.secondaryLeaf.localPosition:Vector3.zero }).ToArray(),
                supplies=ChapterItems(Interactable.Kind.FirecrackerSupply).Select(x=>new ChapterCheckpoint.Supply { id=x.stableId,available=x.gameObject.activeSelf }).ToArray() };
            data.Validate(); return data;
        }
        public void RestoreCheckpoint(ChapterCheckpoint data)
        {
            var doors=ValidatedCheckpointDoors(data);
            var packs=ChapterItems(Interactable.Kind.FirecrackerSupply);
            RestoreValidatedCheckpoint(data,doors,packs);
        }
        Interactable[] ValidatedCheckpointDoors(ChapterCheckpoint data)
        {
            data.Validate();
            if(!Ready || Recovered!=0 || session.Finished || session.InputAllowed || gameObject.scene.path!=data.scene)
                throw new InvalidOperationException("Restore into a fresh paused school chapter");
            Lighting.ValidateRestore(data.lightingVersion==0?null:data.lighting);
            EnsureCheckpointIdentities();
            var doors=ChapterItems(Interactable.Kind.Door); var packs=ChapterItems(Interactable.Kind.FirecrackerSupply);
            if(!doors.Select(x=>x.stableId).SequenceEqual(data.doors.Select(x=>x.id)) ||
                !packs.Select(x=>x.stableId).SequenceEqual(data.supplies.Select(x=>x.id)) ||
                new[]{data.cyclopse,data.portraitActor,data.nurseryActor}.Any(x=>!string.IsNullOrEmpty(x.door) && !doors.Any(d=>d.stableId==x.door)))
                throw new ArgumentException("School checkpoint geometry mismatch");
            if(!StalkerDoorTraversal.ReferencesValid(data.mannequin.doorPassage,doors) ||
                !StalkerDoorTraversal.ReferencesValid(data.mask.doorPassage,doors))
                throw new ArgumentException("Unknown advanced actor door passage");
            for(int i=0;i<doors.Length;i++)
                if(!doors[i].CanRestoreSchoolDoor(data.doors[i].leaf,data.doors[i].secondary,data.doors[i].hasSecondary))
                    throw new ArgumentException("Invalid school door pose");
            return doors;
        }
        // UI restoration runs at the title/input gate. Rehydrate saved obstacle
        // states, then let the engine update carving before probing saved poses.
        // Invalid schemas/door movement are rejected before touching geometry.
        public void PrepareCheckpointNavigation(ChapterCheckpoint data)
        {
            var doors=ValidatedCheckpointDoors(data);
            for(int i=0;i<doors.Length;i++) doors[i].RestoreSchoolDoor(data.doors[i].open,data.doors[i].leaf,data.doors[i].secondary);
            Physics.SyncTransforms();
        }
        void RestoreValidatedCheckpoint(ChapterCheckpoint data,Interactable[] doors,Interactable[] packs)
        {
            var freshLeaves=doors.Select(x=>x.movingLeaf.localPosition).ToArray();
            var freshSecondary=doors.Select(x=>x.secondaryLeaf?x.secondaryLeaf.localPosition:Vector3.zero).ToArray();
            try
            {
                for(int i=0;i<doors.Length;i++)
                { doors[i].movingLeaf.localPosition=data.doors[i].leaf; if(doors[i].secondaryLeaf) doors[i].secondaryLeaf.localPosition=data.doors[i].secondary; }
                Physics.SyncTransforms();
                if(!session.player.CanRestoreChapterProgress(data.player)) throw new ArgumentException("Invalid saved school player clearance");
            }
            finally
            {
                for(int i=0;i<doors.Length;i++)
                { doors[i].movingLeaf.localPosition=freshLeaves[i]; if(doors[i].secondaryLeaf) doors[i].secondaryLeaf.localPosition=freshSecondary[i]; }
                Physics.SyncTransforms();
            }
            foreach(var pose in new[]{data.cyclopse,data.portraitActor,data.nurseryActor}.Where(x=>x.active).Select(x=>x.position)
                .Concat(data.mannequin.active?new[]{data.mannequin.position}:Array.Empty<Vector3>())
                .Concat(data.mask.active?new[]{data.mask.position}:Array.Empty<Vector3>()))
                if(!NavMesh.SamplePosition(pose,out var hit,.25f,NavMesh.AllAreas) || Vector3.Distance(hit.position,pose)>.25f)
                    throw new ArgumentException("Invalid saved school threat floor");
            // All references, movement constraints and physical floors passed before mutation.
            Recovered=data.recovered;
            for(int i=0;i<Memories.Length;i++) Memories[i].gameObject.SetActive(i>=Recovered);
            for(int i=0;i<packs.Length;i++) packs[i].gameObject.SetActive(data.supplies[i].available);
            for(int i=0;i<doors.Length;i++) doors[i].RestoreSchoolDoor(data.doors[i].open,data.doors[i].leaf,data.doors[i].secondary);
            Portrait.RestoreChapterCompletion(data.portraitCompleted,data.portraitWitnessed); Nursery.RestoreChapterCompletion(data.nurseryReleased);
            Cyclopse.RestoreChapterProgress(data.cyclopse,doors); Portrait.angry.RestoreChapterProgress(data.portraitActor,doors);
            Nursery.monster.RestoreChapterProgress(data.nurseryActor,doors);
            Mannequin.RestoreChapterProgress(data.mannequin); Mask.RestoreChapterProgress(data.mask);
            session.player.RestoreChapterProgress(data.player);
            Lighting.Restore(data.lightingVersion==0?null:data.lighting); Physics.SyncTransforms();
        }
    }
}
