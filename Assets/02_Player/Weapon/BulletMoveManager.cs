using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

/*///////////////////////////////////////////
              BulletMoveManager
목적 : 직선으로만 움직이는 JobBullet의 이동을 한곳에 모아 Job(Burst)으로 일괄 계산한다.

       이전엔 Job이 TransformAccessArray를 통해 Transform을 직접 썼는데, 이러면 Rigidbody API를
       거치지 않은 "물리 엔진 몰래 이동"이 되어 매 스텝 Physics.SyncColliderTransform 동기화 비용이
       새로 발생했다. 그래서 이번엔 Job은 순수 위치 계산만 NativeArray로 하고, 계산 결과를
       Complete() 이후 메인 스레드에서 Rigidbody.MovePosition으로 적용한다(=예전 방식과 동일한
       "물리 엔진이 아는 이동" 경로 유지, 계산만 병렬화).
 *///////////////////////////////////////////

public class BulletMoveManager : MonoBehaviour
{
    public static BulletMoveManager m_Instance = null;

    private NativeList<float3> m_listPos;
    private NativeList<float3> m_listForward;
    private NativeList<float> m_listSpeed;
    private NativeList<bool> m_listActive;

    private List<JobBullet> m_listOwnerAtIndex;

    [SerializeField] private int m_iInitialCapacity = 2048;

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
        m_listActive = new NativeList<bool>(m_iInitialCapacity, Allocator.Persistent);

        m_listOwnerAtIndex = new List<JobBullet>(m_iInitialCapacity);
    }

    // JobBullet.Awake()에서 총알 생애주기 중 딱 한 번만 호출
    public int RegisterPermanent(JobBullet _refOwner)
    {
        int iIndex = m_listSpeed.Length;

        m_listPos.Add(float3.zero);
        m_listForward.Add(new float3(0f, 0f, 1f));
        m_listSpeed.Add(0f);
        m_listActive.Add(false);

        m_listOwnerAtIndex.Add(_refOwner);

        return iIndex;
    }

    // JobBullet.SetAttack()(발사 시점)에서 호출
    public void Activate(int _iIndex, Vector3 _vPos, Vector3 _vForward, float _fSpeed)
    {
        m_listPos[_iIndex] = new float3(_vPos.x, _vPos.y, _vPos.z);
        m_listForward[_iIndex] = new float3(_vForward.x, _vForward.y, _vForward.z);
        m_listSpeed[_iIndex] = _fSpeed;
        m_listActive[_iIndex] = true;
    }

    // JobBullet.OnDisable()(풀 반납 시점)에서 호출
    public void Deactivate(int _iIndex)
    {
        m_listActive[_iIndex] = false;
    }

    private void FixedUpdate()
    {
        int iCount = m_listSpeed.Length;
        if (iCount == 0)
            return;

        var job = new StraightMoveJob
        {
            ArrPos = m_listPos.AsArray(),
            ArrForward = m_listForward.AsArray(),
            ArrSpeed = m_listSpeed.AsArray(),
            ArrActive = m_listActive.AsArray(),
            FDeltaTime = Time.fixedDeltaTime
        };

        job.Schedule(iCount, 64).Complete();

        // 계산된 위치를 Rigidbody.MovePosition으로 적용 (물리 엔진이 아는 이동 경로 유지)
        for (int i = 0; i < iCount; ++i)
        {
            if (!m_listActive[i])
                continue;

            float3 vPos = m_listPos[i];
            m_listOwnerAtIndex[i].ApplyMove(new Vector3(vPos.x, vPos.y, vPos.z));
        }
    }

    private void OnDestroy()
    {
        if (m_listPos.IsCreated) m_listPos.Dispose();
        if (m_listForward.IsCreated) m_listForward.Dispose();
        if (m_listSpeed.IsCreated) m_listSpeed.Dispose();
        if (m_listActive.IsCreated) m_listActive.Dispose();
    }

    [BurstCompile]
    private struct StraightMoveJob : IJobParallelFor
    {
        public NativeArray<float3> ArrPos;
        [ReadOnly] public NativeArray<float3> ArrForward;
        [ReadOnly] public NativeArray<float> ArrSpeed;
        [ReadOnly] public NativeArray<bool> ArrActive;
        public float FDeltaTime;

        public void Execute(int index)
        {
            if (!ArrActive[index])
                return;

            ArrPos[index] += ArrForward[index] * ArrSpeed[index] * FDeltaTime;
        }
    }
}
