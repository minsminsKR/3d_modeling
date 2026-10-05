using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    public sealed class Interactable : MonoBehaviour
    {
        public enum Kind { Door, NameSlip, HidingPlace, Exit, Inspect, CorridorMemory, FirecrackerSupply, ChapterMemory, Decoration }
        public Kind kind;
        public string label;
        public string stableId;
        [TextArea] public string inspectionText;
        public Transform movingLeaf;
        public Transform secondaryLeaf;
        public Vector3 openOffset = new Vector3(1.45f, 0, 0);
        public Transform inside, outside;
        public NavMeshObstacle obstacle;
        bool open;
        public bool IsOpen => open;
        public void RestoreDoor(bool requestedOpen, Vector3 leafPosition)
        {
            if (kind != Kind.Door || !movingLeaf) throw new System.InvalidOperationException("Not a movable door");
            open = requestedOpen; movingLeaf.localPosition = leafPosition;
            if (obstacle) obstacle.enabled = !open;
        }
        public void ConfigureDoor(Transform leaf, NavMeshObstacle blocker, Vector3 offset)
        {
            kind = Kind.Door; movingLeaf = leaf; obstacle = blocker; openOffset = offset;
            if (movingLeaf) closedPosition = movingLeaf.localPosition;
        }
        public bool OpenForPursuer()
        {
            var session = GameSession.Current;
            if (kind != Kind.Door || !movingLeaf || open || !session || !session.InputAllowed || session.Finished) return false;
            SetDoorOpen(true);
            return true;
        }
        void SetDoorOpen(bool value)
        {
            open = value;
            if (obstacle) obstacle.enabled = !open;
            var audio = GetComponent<InteractionAudio>();
            if (!audio) audio = gameObject.AddComponent<InteractionAudio>();
            audio.PlayDoor(open);
        }
        Vector3 closedPosition;
        Vector3 secondaryClosed;
        public string DisplayLabel
        {
            get
            {
                if(kind==Kind.Door)
                {
                    string room=name.StartsWith("CLASSROOM")?"교실":name.StartsWith("WASHROOM")?"화장실":name.StartsWith("INFIRMARY")?"보건실":"";
                    return room+" 문 "+(open?"닫기":"열기");
                }
                if(kind==Kind.HidingPlace)return "캐비닛에 숨기";
                if(kind==Kind.Exit && GameSession.Current && GameSession.Current.CorridorMode)return "봉인된 회랑 문 확인";
                if(kind==Kind.Exit && GameSession.Current && GameSession.Current.ChapterMode)return "1층 출입문 확인";
                if(kind==Kind.Exit)return GameSession.Current&&GameSession.Current.StoryStep>=4?"출석함에 마지막 이름 돌려놓기":"현관 출석함 확인";
                return label;
            }
        }
        void Awake() { if (movingLeaf) closedPosition = movingLeaf.localPosition; if(secondaryLeaf)secondaryClosed=secondaryLeaf.localPosition; }
        void Update()
        {
            if (kind != Kind.Door || !movingLeaf) return;
            movingLeaf.localPosition = Vector3.MoveTowards(movingLeaf.localPosition,
                closedPosition + (open ? openOffset : Vector3.zero), Time.deltaTime * 1.8f);
            if(secondaryLeaf)secondaryLeaf.localPosition=Vector3.MoveTowards(secondaryLeaf.localPosition,secondaryClosed-(open?openOffset:Vector3.zero),Time.deltaTime*1.8f);
            if (obstacle) obstacle.enabled = !open;
        }
        public void Use(PlayerMotor player)
        {
            if (!player || !GameSession.Current || !GameSession.Current.InputAllowed) return;
            switch (kind)
            {
                case Kind.ChapterMemory:
                    if(GameSession.Current.Chapter && GameSession.Current.Chapter.Collect(stableId)) gameObject.SetActive(false);
                    break;
                case Kind.CorridorMemory:
                    if (GameSession.Current.Corridor && GameSession.Current.Corridor.Collect(stableId)) gameObject.SetActive(false);
                    break;
                case Kind.FirecrackerSupply:
                    if (player.Firecrackers && player.Firecrackers.AddSupply()) gameObject.SetActive(false);
                    else GameSession.Current.Notify("폭죽은 다섯 개까지 소지할 수 있습니다.");
                    break;
                case Kind.Door:
                    // Do not close a door on a player standing in its opening.
                    var local=transform.InverseTransformPoint(player.transform.position);
                    float halfWidth=obstacle?obstacle.size.x*.5f:1.2f;
                    if (open && Mathf.Abs(local.x)<halfWidth+.35f && Mathf.Abs(local.z)<.5f && local.y>-.5f && local.y<2.5f)
                    {GameSession.Current.Notify("문 사이에 서 있습니다. 조금 물러난 뒤 닫으세요.");return;}
                    SetDoorOpen(!open);
                    break;
                case Kind.NameSlip:
                    if (GameSession.Current.Collect(stableId)) gameObject.SetActive(false);
                    break;
                case Kind.HidingPlace:
                    if (player.Hidden) player.LeaveHiding();
                    else if (inside && outside) player.Hide(this, inside.position, outside.position);
                    break;
                case Kind.Exit: GameSession.Current.TryEscape(); break;
                case Kind.Inspect:
                    GameSession.Current.Inspect(string.IsNullOrEmpty(stableId)?name:stableId,inspectionText);
                    break;
            }
        }
    }
}
