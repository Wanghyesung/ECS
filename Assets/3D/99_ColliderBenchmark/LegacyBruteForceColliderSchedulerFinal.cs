using UnityEngine;

/*///////////////////////////////////////////
     LegacyBruteForceColliderSchedulerFinal
목적 : 프레임 최상단([DefaultExecutionOrder(-1000)])에서 매니저의 스케줄 작업(삭제
       정리 + Circle Transform 갱신 + 그리드 재구성 + Job Schedule)을 돌리는 얇은
       트리거. LegacyBruteForceColliderManagerFinal.Awake()가 자동으로
       AddComponent해서 씬에 수동 배치할 필요가 없다(Docs/Collider.md §18 실제
       설계와 동일).
 *///////////////////////////////////////////
[DefaultExecutionOrder(-1000)]
public sealed class LegacyBruteForceColliderSchedulerFinal : MonoBehaviour
{
    private void Update()
    {
        LegacyBruteForceColliderManagerFinal.Instance.DoScheduleWork();
    }
}
