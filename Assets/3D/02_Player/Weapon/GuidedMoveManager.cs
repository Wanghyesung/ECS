using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using System.Collections.Generic;

/*///////////////////////////////////////////
              GuidedMoveManager
목적 : GuidedBullet(타겟을 향해 순간적으로 방향을 트는 총알) 전용 이동 매니저.
       별도의 JobGuidedBullet 서브클래스 없이 GuidedBullet이 직접 이 매니저에 등록됨.

       TransformAccessArray를 써서 Job이 Transform(위치+회전)에 직접 쓴다. 회전은
       Unity.Mathematics.quaternion으로 계산해서 UnityEngine.Quaternion으로 암시적 변환
       (quaternion.LookRotationSafe는 Burst에서 쓰라고 만들어진 함수라 안전함). 프리팹에
       Rigidbody/Collider가 이미 없어서 Physics.SyncColliderTransform 비용도 없음.

       Job이 Transform까지 직접 쓰기 때문에 GuidedBullet 쪽은 Complete 이후 메인 스레드에서
       따로 적용할 게 없다(도착 판정/넉백 동기화 같은 부수효과가 없는 타입이라 Missiles와 다름).

       타겟(몬스터 등)의 Transform 위치는 매 Update마다 메인 스레드에서 스냅샷 떠서 Job에 넘긴다
       (타겟은 이 매니저가 관리하는 TransformAccessArray 소속이 아니라서 Job 안에서 직접 못 읽음).

       [DefaultExecutionOrder(500)]: Unity는 서로 다른 스크립트의 Update() 순서를 보장하지
       않는다. 총알을 쏘는 Weapon 쪽 Update()가 이 매니저의 Update()보다 먼저 끝나야
       Activate()가 "이미 Schedule되어 아직 Complete 안 된" NativeList를 건드리는 경합이
       안 생긴다 - 그래서 기본 순서(0)보다 확실히 뒤로 미뤄둠 (ColliderManager의 1000보다는 앞)
 *///////////////////////////////////////////

[DefaultExecutionOrder(500)]

public class GuidedMoveManager : MonoBehaviour
{
    public static GuidedMoveManager m_Instance = null;

    private TransformAccessArray m_transformArray;
    private NativeList<float> m_listSpeed;
    private NativeList<float3> m_listTargetPos;
    private NativeList<bool> m_listHasTarget;
    private NativeList<bool> m_listActive;

    private List<Transform> m_listTargetTr;

    [SerializeField] private int m_iInitialCapacity = 256;

    private JobHandle m_tHandle;
    private bool m_bScheduled = false;
    private bool m_bDisposed = false;
    private int COUNt = 0;
    private void Awake()
    {
        if (m_Instance != null)
        {
            Destroy(this);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(this);

        m_transformArray = new TransformAccessArray(m_iInitialCapacity);
        m_listSpeed = new NativeList<float>(m_iInitialCapacity, Allocator.Persistent);
        m_listTargetPos = new NativeList<float3>(m_iInitialCapacity, Allocator.Persistent);
        m_listHasTarget = new NativeList<bool>(m_iInitialCapacity, Allocator.Persistent);
        m_listActive = new NativeList<bool>(m_iInitialCapacity, Allocator.Persistent);

        m_listTargetTr = new List<Transform>(m_iInitialCapacity);
    }

    // GuidedBullet.Awake()에서 총알 생애주기 중 딱 한 번만 호출
    public int RegisterPermanent(GuidedBullet _refOwner)
    {
        int iIndex = m_listSpeed.Length;

        m_transformArray.Add(_refOwner.transform);
        m_listSpeed.Add(0f);
        m_listTargetPos.Add(float3.zero);
        m_listHasTarget.Add(false);
        m_listActive.Add(false);

        m_listTargetTr.Add(null);

        return iIndex;
    }

    // GuidedBullet.SetAttack()(발사 시점)에서 호출
    public void Activate(int _iIndex, float _fSpeed, Transform _refTargetTr)
    {
        if (m_bDisposed)
            return;

        m_listSpeed[_iIndex] = _fSpeed;
        m_listTargetTr[_iIndex] = _refTargetTr;
        m_listActive[_iIndex] = true;

        ++COUNt;

    }

    // GuidedBullet.OnDisable()(풀 반납 시점)에서 호출. 플레이모드 종료/씬 전환 시 다른
    // 오브젝트의 OnDisable이 이 매니저의 OnDestroy(NativeList Dispose) 이후에 불릴 수 있어서 가드 필요
    public void Deactivate(int _iIndex)
    {
        if (m_bDisposed)
            return;

        m_listActive[_iIndex] = false;
        m_listTargetTr[_iIndex] = null;
        --COUNt;

    }

    private void Update()
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
            ArrSpeed = m_listSpeed.AsArray(),
            ArrTargetPos = m_listTargetPos.AsArray(),
            ArrHasTarget = m_listHasTarget.AsArray(),
            ArrActive = m_listActive.AsArray(),
            FDeltaTime = Time.deltaTime
        };

        m_tHandle = job.Schedule(m_transformArray);
        m_bScheduled = true;
    }

    // Job이 TransformAccessArray로 Transform에 직접 썼으므로 Complete만 하면 끝
    private void LateUpdate()
    {
        if (!m_bScheduled)
            return;

        m_tHandle.Complete();
        m_bScheduled = false;
    }

    private void OnDestroy()
    {
        m_bDisposed = true;

        if (m_bScheduled)
            m_tHandle.Complete();

        if (m_transformArray.isCreated) m_transformArray.Dispose();
        if (m_listSpeed.IsCreated) m_listSpeed.Dispose();
        if (m_listTargetPos.IsCreated) m_listTargetPos.Dispose();
        if (m_listHasTarget.IsCreated) m_listHasTarget.Dispose();
        if (m_listActive.IsCreated) m_listActive.Dispose();
    }

    [BurstCompile]
    private struct GuidedMoveJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float> ArrSpeed;
        [ReadOnly] public NativeArray<float3> ArrTargetPos;
        [ReadOnly] public NativeArray<bool> ArrHasTarget;
        [ReadOnly] public NativeArray<bool> ArrActive;
        public float FDeltaTime;

        public void Execute(int index, TransformAccess _transform)
        {
            if (!ArrActive[index])
                return;

            float3 vPos = _transform.position;
            quaternion qRot = _transform.rotation;
            float3 vForward = math.mul(qRot, new float3(0f, 0f, 1f));

            if (ArrHasTarget[index])
                vForward = math.normalize(ArrTargetPos[index] - vPos);

            _transform.position = vPos + vForward * ArrSpeed[index] * FDeltaTime;
            _transform.rotation = quaternion.LookRotationSafe(vForward, math.up());
        }
    }
}
