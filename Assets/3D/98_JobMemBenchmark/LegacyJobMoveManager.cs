using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;

/*///////////////////////////////////////////
           LegacyJobMoveManager
목적 : 현재 BulletMoveManager와 같은 구조(TransformAccessArray + [BurstCompile]
       IJobParallelForTransform, Update에서 Schedule / LateUpdate에서 Complete)를
       벤치마크 씬에서 독립적으로 돌리기 위한 축소판. LegacyUpdateMover와의 비교군.

       실제 매니저와 다른 점은 풀/발사 로직이 없다는 것뿐이고, 측정에 영향을 주는
       부분(Job 타입, 스케줄 시점, Complete 시점, 실행 순서 500)은 그대로 맞췄다.

       Schedule과 Complete가 서로 다른 시점에 있어서 프로파일러에서 그냥 보면
       어디에 붙었는지 안 보인다 - 그래서 마커를 직접 심는다.
 *///////////////////////////////////////////
[DefaultExecutionOrder(500)]
public sealed class LegacyJobMoveManager : MonoBehaviour
{
    private static readonly ProfilerMarker s_tMarkerSchedule = new ProfilerMarker("JobMem.JobMoveSchedule");
    private static readonly ProfilerMarker s_tMarkerComplete = new ProfilerMarker("JobMem.JobMoveComplete");

    private const int INITIAL_CAPACITY = 5120;

    private TransformAccessArray m_transformArray;
    private NativeList<float> m_listSpeed;
    private NativeList<float> m_listBounds;

    private JobHandle m_tHandle;
    private bool m_bScheduled = false;
    private bool m_bDisposed = false;

    private void Awake()
    {
        m_transformArray = new TransformAccessArray(INITIAL_CAPACITY);
        m_listSpeed = new NativeList<float>(INITIAL_CAPACITY, Allocator.Persistent);
        m_listBounds = new NativeList<float>(INITIAL_CAPACITY, Allocator.Persistent);
    }

    public void Register(Transform _refTransform, float _fSpeed, float _fBoundsRadius)
    {
        if (m_bDisposed == true)
            return;

        m_transformArray.Add(_refTransform);
        m_listSpeed.Add(_fSpeed);
        m_listBounds.Add(_fBoundsRadius);
    }

    private void Update()
    {
        if (m_listSpeed.Length == 0)
            return;

        using (s_tMarkerSchedule.Auto())
        {
            MoveJob tJob = new MoveJob
            {
                ArrSpeed = m_listSpeed.AsArray(),
                ArrBounds = m_listBounds.AsArray(),
                FDeltaTime = Time.deltaTime,
            };

            m_tHandle = tJob.Schedule(m_transformArray);
            m_bScheduled = true;
        }
    }

    // Job이 TransformAccessArray로 Transform에 직접 썼으므로 Complete만 하면 끝 -
    // 메인 스레드가 결과를 다시 옮겨 적는 코드(ApplyMove)가 아예 없다
    private void LateUpdate()
    {
        if (m_bScheduled == false)
            return;

        using (s_tMarkerComplete.Auto())
        {
            m_tHandle.Complete();
            m_bScheduled = false;
        }
    }

    private void OnDestroy()
    {
        m_bDisposed = true;

        if (m_bScheduled == true)
            m_tHandle.Complete();

        if (m_transformArray.isCreated == true)
            m_transformArray.Dispose();
        if (m_listSpeed.IsCreated == true)
            m_listSpeed.Dispose();
        if (m_listBounds.IsCreated == true)
            m_listBounds.Dispose();
    }

    [BurstCompile]
    private struct MoveJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float> ArrSpeed;
        [ReadOnly] public NativeArray<float> ArrBounds;
        public float FDeltaTime;

        // LegacyUpdateMover와 정확히 같은 연산을 해야 A/B가 공정하다 - 경계 반사까지 동일
        public void Execute(int index, TransformAccess _transform)
        {
            Vector3 vForward = _transform.rotation * Vector3.forward;
            Vector3 vNextPos = _transform.position + vForward * ArrSpeed[index] * FDeltaTime;

            float fRadius = ArrBounds[index];
            if (vNextPos.sqrMagnitude > fRadius * fRadius)
            {
                Vector3 vNormal = vNextPos.normalized;
                _transform.rotation = Quaternion.LookRotation(Vector3.Reflect(vForward, vNormal));
                vNextPos = vNormal * fRadius;
            }

            _transform.position = vNextPos;
        }
    }
}
