using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    public sealed partial class CorridorRun
    {
        Interactable[] CheckpointDoors => world.GetComponentsInChildren<Interactable>(true)
            .Where(x=>x.kind==Interactable.Kind.Door).OrderBy(x=>x.stableId,StringComparer.Ordinal).ToArray();
        Interactable[] CheckpointItems(Interactable.Kind kind) => world.GetComponentsInChildren<Interactable>(true)
            .Where(x=>x.kind==kind).OrderBy(x=>x.stableId,StringComparer.Ordinal).ToArray();
        public CorridorCheckpoint CaptureCheckpoint()
        {
            if(!string.IsNullOrEmpty(SuspendBlockReason)) throw new InvalidOperationException(SuspendBlockReason);
            var data=new CorridorCheckpoint { token=Guid.NewGuid().ToString("N"),seed=Seed,seconds=session.ElapsedPlayTime,
                recovered=Enumerable.Range(0,5).Select(i=>recovered.Contains("memory-"+i)).ToArray(),
                supplies=CheckpointItems(Interactable.Kind.FirecrackerSupply).Select(x=>x.gameObject.activeSelf).ToArray(),
                player=session.player.CaptureProgress(),threats=threats.Select(x=>x.CaptureProgress()).ToArray(),
                doors=CheckpointDoors.Select(x=>new CorridorCheckpoint.Door { id=x.stableId,open=x.IsOpen,leaf=x.movingLeaf.localPosition }).ToArray() };
            data.Validate(); return data;
        }
        public string SuspendBlockReason
        {
            get
            {
                if(!Ready || session.Finished || session.Shell.Screen!=GameShell.Page.Pause) return "탐색 중 일시정지 메뉴에서 중단 저장하세요.";
                if(session.player.Hidden) return "은신처에서 나온 뒤 중단 저장할 수 있습니다.";
                if(threats.Any(x=>x.AttackActive)) return "공격 동작이 끝난 뒤 일시정지해 중단 저장하세요.";
                if(FindObjectsByType<FirecrackerProjectile>(FindObjectsSortMode.None).Any(x=>x.gameObject.scene==gameObject.scene)) return "던진 폭죽의 효과가 끝난 뒤 중단 저장하세요.";
                var cue=session.player.GetComponent<DetectionFeedback>();
                return cue&&cue.Active?"인식 연출이 끝난 뒤 중단 저장하세요.":"";
            }
        }
        public void RestoreCheckpoint(CorridorCheckpoint data)
        {
            data.Validate();
            if(!Ready || Seed!=data.seed || session.Finished || session.InputAllowed)
                throw new InvalidOperationException("Checkpoint must match a prepared, paused corridor");
            var doors=CheckpointDoors; var memories=CheckpointItems(Interactable.Kind.CorridorMemory);
            var packs=CheckpointItems(Interactable.Kind.FirecrackerSupply);
            // Validate all references/physical points before changing any gameplay state.
            if(memories.Length!=5 || packs.Length!=8 || doors.Length!=data.doors.Length ||
                !doors.Select(x=>x.stableId).SequenceEqual(data.doors.Select(x=>x.id)) ||
                data.threats.Any(x=>!string.IsNullOrEmpty(x.door)&&!doors.Any(d=>d.stableId==x.door)))
                throw new ArgumentException("Checkpoint geometry mismatch");
            // A valid saved player can stand in an opening whose fresh leaf is
            // closed. Probe the saved geometry, then always put the fresh leaves
            // back; failed validation must not consume pickups or change AI.
            var freshLeaves=doors.Select(x=>x.movingLeaf.localPosition).ToArray();
            try
            {
                for(int i=0;i<doors.Length;i++) doors[i].movingLeaf.localPosition=data.doors[i].leaf;
                Physics.SyncTransforms();
                if(!session.player.CanRestoreProgress(data.player)) throw new ArgumentException("Invalid saved player clearance");
            }
            finally
            {
                for(int i=0;i<doors.Length;i++) doors[i].movingLeaf.localPosition=freshLeaves[i];
                Physics.SyncTransforms();
            }
            foreach(var threat in data.threats)
                if(threat.active && (!NavMesh.SamplePosition(threat.position,out var hit,.25f,NavMesh.AllAreas) ||
                    Vector3.Distance(hit.position,threat.position)>.25f)) throw new ArgumentException("Invalid threat floor position");
            recovered.Clear();
            for(int i=0;i<5;i++) { if(data.recovered[i]) recovered.Add("memory-"+i); memories[i].gameObject.SetActive(!data.recovered[i]); }
            for(int i=0;i<8;i++) packs[i].gameObject.SetActive(data.supplies[i]);
            for(int i=0;i<doors.Length;i++) doors[i].RestoreDoor(data.doors[i].open,data.doors[i].leaf);
            for(int i=0;i<threats.Count;i++) threats[i].RestoreProgress(data.threats[i],doors);
            session.player.RestoreProgress(data.player); Physics.SyncTransforms();
        }
    }
}
