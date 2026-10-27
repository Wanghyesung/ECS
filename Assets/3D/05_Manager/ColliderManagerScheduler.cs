using UnityEngine;

// 충돌 Job을 Update 초반에 예약한다. 완료와 이벤트 처리는 매니저의 LateUpdate에서 수행한다.
[DefaultExecutionOrder(-1000)]
public class ColliderManagerScheduler : MonoBehaviour
{
    private void Update()
    {
        if (ColliderManager.m_Instance != null)
            ColliderManager.m_Instance.ScheduleFrame();
    }
}
