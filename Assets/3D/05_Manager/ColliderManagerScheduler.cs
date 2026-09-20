using UnityEngine;

/*///////////////////////////////////////////
              ColliderManagerScheduler
목적 : ColliderManager.ScheduleFrame()을 이 프레임에서 최대한 일찍 호출하기 위한 얇은
       트리거. ColliderManager 자체는 [DefaultExecutionOrder(1000)]이라 Complete를
       최대한 늦게(LateUpdate 전체와 겹치도록) 돌리는데, Schedule까지 같은 순서면 Update
       구간에서 겹칠 상대가 없어진다 - 한 클래스는 메서드별로 다른 실행 순서를 가질 수
       없으므로, Schedule 쪽만 이 별도 컴포넌트로 떼어내 아주 이른 순서(-1000)에 둔다.
       이렇게 하면 충돌 Job이 이번 프레임 나머지 Update 전체 + LateUpdate 전체 동안
       워커 스레드에서 돌 수 있다.

       ColliderManager.Awake()가 자기 GameObject에 이 컴포넌트를 자동으로 붙이므로
       씬에서 수동으로 추가할 필요 없다.
 *///////////////////////////////////////////
[DefaultExecutionOrder(-1000)]
public class ColliderManagerScheduler : MonoBehaviour
{
    private void Update()
    {
        if (ColliderManager.m_Instance != null)
            ColliderManager.m_Instance.ScheduleFrame();
    }
}
