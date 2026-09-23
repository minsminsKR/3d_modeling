using UnityEngine;
using UnityEngine.InputSystem;

namespace HappyToy.V2
{
    public sealed class FirecrackerInventory : MonoBehaviour
    {
        public int Count {get;private set;}=2;
        public FirecrackerProjectile LastThrown {get;private set;}
        PlayerMotor player;float cooldown;
        void Awake(){player=GetComponent<PlayerMotor>();}
        void Update()
        {
            cooldown=Mathf.Max(0,cooldown-Time.deltaTime);
            if(Keyboard.current!=null&&Keyboard.current.qKey.wasPressedThisFrame)TryThrow();
        }
        public bool TryThrow()
        {
            var session=GameSession.Current;
            if(!session||!session.InputAllowed||player.Hidden||cooldown>0)return false;
            if(session.StoryStep>=4){session.Notify("이름이 복원되어 복도가 조용해졌습니다.");return false;}
            if(Count<=0){session.Notify("폭죽이 없습니다.");return false;}
            var eye=player.eyes.transform;Vector3 point=eye.position;
            if(!Physics.SphereCast(point,.08f,eye.forward,out var hit,.35f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))point+=eye.forward*.3f;
            var root=new GameObject("Thrown firecracker");root.transform.position=point;
            LastThrown=root.AddComponent<FirecrackerProjectile>();LastThrown.Launch(eye.forward*14+Vector3.up*3.5f);
            Count--;cooldown=.5f;
            session.Notify("폭죽 소리는 추적 중이 아닌 몬스터를 유인합니다. 다른 길로 이동하세요.");return true;
        }
    }
}
