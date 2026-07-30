using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

/*///////////////////////////////////////////
              GuidedMoveManager
목적 : JobGuidedBullet(타겟을 향해 순간적으로 방향을 트는 총알) 전용 이동 매니저.

       Job은 위치/방향 계산만 NativeArray로 하고(Transform은 안 건드림), Complete() 이후
       메인 스레드에서 Rigidbody.MoveRotation + MovePosition으로 적용한다.
       (Job이 TransformAccessArray로 Transform을 직접 쓰면 Physics.SyncColliderTransform
       동기화 비용이 새로 발생하는 걸 확인해서, Rigidbody 경로로 되돌린 버전)

       타겟(몬스터 등)의 Transform 위치는 매 FixedUpdate마다 메인 스레드에서 스냅샷 떠서 Job에 넘긴다.
 *///////////////////////////////////////////

public class GuidedMoveManager : MonoBehaviour
{
    public static GuidedMoveManager m_Instance = null;

    private NativeList<float3> m_listPos;
    private NativeList<float3> m_listForward;
    private NativeList<float> m_listSpeed;
    private NativeList<float3> m_listTargetPos;
    private NativeList<bool> m_listHasTarget;
    private NativeList<bool> m_listActive;

    private List<JobGuidedBullet> m_listOwnerAtIndex;
    private List<Transform> m_listTargetTr;

    [SerializeField] private int m_iInitialCapacity = 256;

    private void Awake()
    {
        if (m_Instance != null)
        {
            Destroy(this);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(this);

        m_listPos = new NativeList<float3>(m_iInitialCapacity, Allocator.Persistent);
        m_listForward = new NativeList<float3>(m_iInitialCapacity, Allocator.Persistent);
        m_listSpeed = new NativeList<float>(m_iInitialCapacity, Allocator.Persistent);
        m_listTargetPos = new NativeList<float3>(m_iInitialCapacity, Allocator.Persistent);
        m_listHasTarget = new NativeList<bool>(m_iInitialCapacity, Allocator.Persistent);
        m_listActive = new NativeList<bool>(m_iInitialCapacity, Allocator.Persistent);

        m_listOwnerAtIndex = new List<JobGuidedBullet>(m_iInitialCapacity);
        m_listTargetTr = new List<Transform>(m_iInitialCapacity);
    }

    // JobGuidedBullet.Awake()에서 총알 생애주기 중 딱 한 번만 호출
    public int RegisterPermanent(JobGuidedBullet _refOwner)
    {
        int iIndex = m_listSpeed.Length;

        m_listPos.Add(float3.zero);
        m_listForward.Add(new float3(0f, 0f, 1f));
        m_listSpeed.Add(0f);
        m_listTargetPos.Add(float3.zero);
        m_listHasTarget.Add(false);
        m_listActive.Add(false);

        m_listOwnerAtIndex.Add(_refOwner);
        m_listTargetTr.Add(null);

        return iIndex;
    }

    // JobGuidedBullet.SetAttack()(발사 시점)에서 호출
    public void Activate(int _iIndex, Vector3 _vPos, Vector3 _vForward, float _fSpeed, Transform _refTargetTr)
    {
        m_listPos[_iIndex] = new float3(_vPos.x, _vPos.y, _vPos.z);
        m_listForward[_iIndex] = new float3(_vForward.x, _vForward.y, _vForward.z);
        m_listSpeed[_iIndex] = _fSpeed;
        m_listTargetTr[_iIndex] = _refTargetTr;
        m_listActive[_iIndex] = true;
    }

    // JobGuidedBullet.OnDisable()(풀 반납 시점)에서 호출
    public void Deactivate(int _iIndex)
    {
        m_listActive[_iIndex] = false;
        m_listTargetTr[_iIndex] = null;
    }

    private void FixedUpdate()
    {
        int iCount = m_listSpeed.Length;
        if (iCount == 0)
            return;

        for (int i = 0; i < iCount; ++i)
        {
            if (!m_listActive[i])
                continue;

            Transform refTarget = m_listTargetTr[i];
            bool bHasTarget = refTarget != null;
            m_listHasTarget[i] = bHasTarget;

            if (bHasTarget)
            {
                Vector3 vPos = refTarget.position;
                m_listTargetPos[i] = new float3(vPos.x, vPos.y, vPos.z);
            }
        }

        var job = new GuidedMoveJob
        {
            ArrPos = m_listPos.AsArray(),
            ArrForward = m_listForward.AsArray(),
            ArrSpeed = m_listSpeed.AsArray(),
            ArrTargetPos = m_listTargetPos.AsArray(),
            ArrHasTarget = m_listHasTarget.AsArray(),
            ArrActive = m_listActive.AsArray(),
            FDeltaTime = Time.fixedDeltaTime
        };

        job.Schedule(iCount, 64).Complete();

        for (int i = 0; i < iCount; ++i)
        {
            if (!m_listActive[i])
                continue;

            float3 vPos = m_listPos[i];
            float3 vFwd = m_listForward[i];
            Vector3 vPosUnity = new Vector3(vPos.x, vPos.y, vPos.z);
            Vector3 vFwdUnity = new Vector3(vFwd.x, vFwd.y, vFwd.z);

            // 여기는 메인 스레드라 Quaternion.LookRotation을 그대로 써도 안전함
            m_listOwnerAtIndex[i].ApplyMove(vPosUnity, Quaternion.LookRotation(vFwdUnity));
        }
    }

    private void OnDestroy()
    {
        if (m_listPos.IsCreated) m_listPos.Dispose();
        if (m_listForward.IsCreated) m_listForward.Dispose();
        if (m_listSpeed.IsCreated) m_listSpeed.Dispose();
        if (m_listTargetPos.IsCreated) m_listTargetPos.Dispose();
        if (m_listHasTarget.IsCreated) m_listHasTarget.Dispose();
        if (m_listActive.IsCreated) m_listActive.Dispose();
    }

    [BurstCompile]
    private struct GuidedMoveJob : IJobParallelFor
    {
        public NativeArray<float3> ArrPos;
        public NativeArray<float3> ArrForward;
        [ReadOnly] public NativeArray<float> ArrSpeed;
        [ReadOnly] public NativeArray<float3> ArrTargetPos;
        [ReadOnly] public NativeArray<bool> ArrHasTarget;
        [ReadOnly] public NativeArray<bool> ArrActive;
        public float FDeltaTime;

        public void Execute(int index)
        {
            if (!ArrActive[index])
                return;

            float3 vForward = ArrForward[index];

            if (ArrHasTarget[index])
                vForward = math.normalize(ArrTargetPos[index] - ArrPos[index]);

            ArrForward[index] = vForward;
            ArrPos[index] += vForward * ArrSpeed[index] * FDeltaTime;
        }
    }
}
