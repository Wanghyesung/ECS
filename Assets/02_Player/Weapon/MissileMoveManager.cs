using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

/*///////////////////////////////////////////
              MissileMoveManager
목적 : JobMissile(가속 회전으로 유도되다 목표 근처에서 도착 처리되는 총알) 전용 이동 매니저.

       Job은 위치/방향 계산만 NativeArray로 하고(Transform은 안 건드림), Complete() 이후
       메인 스레드에서 Rigidbody.MoveRotation + MovePosition으로 적용한다.
       (Job이 Transform을 직접 쓰면 Physics.SyncColliderTransform 동기화 비용이 새로
       발생하는 걸 확인해서, Rigidbody 경로로 되돌린 버전)

       원본의 Vector3.Slerp(forward, dir, t)는 쿼터니언이 아니라 두 단위벡터 사이의
       구면보간이라, Burst 안에서 벡터 슬러프 공식을 직접 계산한다(회전각 기반 사인 보간).
       그래서 Job 안에서는 쿼터니언을 아예 안 쓰고, 최종 forward만 메인 스레드에서
       Quaternion.LookRotation으로 변환해 적용한다.

       도착 판정/넉백 방향 동기화는 이전과 동일하게 Complete() 이후 메인 스레드에서 처리한다.
 *///////////////////////////////////////////

public class MissileMoveManager : MonoBehaviour
{
    public static MissileMoveManager m_Instance = null;

    private NativeList<float3> m_listPos;
    private NativeList<float3> m_listForward;

    private NativeList<float> m_listSpeed;
    private NativeList<float> m_listElapsedTime;
    private NativeList<float> m_listTargetLength;
    private NativeList<float> m_listProximityRadius;
    private NativeList<float> m_listRotationSpeed;
    private NativeList<float> m_listMaxRotationSpeed;
    private NativeList<float> m_listRotateSpeedRate;

    private NativeList<float3> m_listTargetPos;
    private NativeList<bool> m_listArrived;
    private NativeList<float3> m_listMoveDirOut;
    private NativeList<bool> m_listActive;

    private List<JobMissile> m_listOwnerAtIndex;
    private List<Transform> m_listTargetTr;
    private List<bool> m_listTraceTarget;
    private List<Vector3> m_listFixedTargetPos;

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
        m_listElapsedTime = new NativeList<float>(m_iInitialCapacity, Allocator.Persistent);
        m_listTargetLength = new NativeList<float>(m_iInitialCapacity, Allocator.Persistent);
        m_listProximityRadius = new NativeList<float>(m_iInitialCapacity, Allocator.Persistent);
        m_listRotationSpeed = new NativeList<float>(m_iInitialCapacity, Allocator.Persistent);
        m_listMaxRotationSpeed = new NativeList<float>(m_iInitialCapacity, Allocator.Persistent);
        m_listRotateSpeedRate = new NativeList<float>(m_iInitialCapacity, Allocator.Persistent);

        m_listTargetPos = new NativeList<float3>(m_iInitialCapacity, Allocator.Persistent);
        m_listArrived = new NativeList<bool>(m_iInitialCapacity, Allocator.Persistent);
        m_listMoveDirOut = new NativeList<float3>(m_iInitialCapacity, Allocator.Persistent);
        m_listActive = new NativeList<bool>(m_iInitialCapacity, Allocator.Persistent);

        m_listOwnerAtIndex = new List<JobMissile>(m_iInitialCapacity);
        m_listTargetTr = new List<Transform>(m_iInitialCapacity);
        m_listTraceTarget = new List<bool>(m_iInitialCapacity);
        m_listFixedTargetPos = new List<Vector3>(m_iInitialCapacity);
    }

    // JobMissile.Awake()에서 총알 생애주기 중 딱 한 번만 호출
    public int RegisterPermanent(JobMissile _refOwner)
    {
        int iIndex = m_listSpeed.Length;

        m_listPos.Add(float3.zero);
        m_listForward.Add(new float3(0f, 0f, 1f));

        m_listSpeed.Add(0f);
        m_listElapsedTime.Add(0f);
        m_listTargetLength.Add(0f);
        m_listProximityRadius.Add(0f);
        m_listRotationSpeed.Add(0f);
        m_listMaxRotationSpeed.Add(0f);
        m_listRotateSpeedRate.Add(0f);

        m_listTargetPos.Add(float3.zero);
        m_listArrived.Add(false);
        m_listMoveDirOut.Add(float3.zero);
        m_listActive.Add(false);

        m_listOwnerAtIndex.Add(_refOwner);
        m_listTargetTr.Add(null);
        m_listTraceTarget.Add(false);
        m_listFixedTargetPos.Add(Vector3.zero);

        return iIndex;
    }

    // JobMissile.SetAttack()(발사 시점)에서 호출
    public void Activate(int _iIndex, Vector3 _vPos, Vector3 _vForward, float _fSpeed, float _fTargetLength,
        float _fProximityRadius, float _fRotationSpeed, float _fMaxRotationSpeed, float _fRotateSpeedRate,
        bool _bTraceTarget, Transform _refTargetTr, Vector3 _vFixedTargetPos)
    {
        m_listPos[_iIndex] = new float3(_vPos.x, _vPos.y, _vPos.z);
        m_listForward[_iIndex] = new float3(_vForward.x, _vForward.y, _vForward.z);

        m_listSpeed[_iIndex] = _fSpeed;
        m_listElapsedTime[_iIndex] = 0f;
        m_listTargetLength[_iIndex] = _fTargetLength;
        m_listProximityRadius[_iIndex] = _fProximityRadius;
        m_listRotationSpeed[_iIndex] = _fRotationSpeed;
        m_listMaxRotationSpeed[_iIndex] = _fMaxRotationSpeed;
        m_listRotateSpeedRate[_iIndex] = _fRotateSpeedRate;

        m_listArrived[_iIndex] = false;

        m_listTraceTarget[_iIndex] = _bTraceTarget;
        m_listTargetTr[_iIndex] = _refTargetTr;
        m_listFixedTargetPos[_iIndex] = _vFixedTargetPos;

        m_listActive[_iIndex] = true;
    }

    // JobMissile.OnDisable()(풀 반납 시점)에서 호출
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

            Vector3 vResolvedPos;
            Transform refTargetTr = m_listTargetTr[i];

            if (m_listTraceTarget[i] && refTargetTr != null)
                vResolvedPos = refTargetTr.position;
            else
                vResolvedPos = m_listFixedTargetPos[i];

            m_listTargetPos[i] = new float3(vResolvedPos.x, vResolvedPos.y, vResolvedPos.z);
        }

        var job = new MissileMoveJob
        {
            ArrPos = m_listPos.AsArray(),
            ArrForward = m_listForward.AsArray(),
            ArrSpeed = m_listSpeed.AsArray(),
            ArrElapsedTime = m_listElapsedTime.AsArray(),
            ArrTargetLength = m_listTargetLength.AsArray(),
            ArrProximityRadius = m_listProximityRadius.AsArray(),
            ArrRotationSpeed = m_listRotationSpeed.AsArray(),
            ArrMaxRotationSpeed = m_listMaxRotationSpeed.AsArray(),
            ArrRotateSpeedRate = m_listRotateSpeedRate.AsArray(),
            ArrTargetPos = m_listTargetPos.AsArray(),
            ArrArrived = m_listArrived.AsArray(),
            ArrMoveDirOut = m_listMoveDirOut.AsArray(),
            ArrActive = m_listActive.AsArray(),
            FDeltaTime = Time.fixedDeltaTime
        };

        job.Schedule(iCount, 64).Complete();

        // 부수효과(넉백 방향 동기화, 도착 시 풀 반납, 실제 이동 적용)는 여기 메인 스레드에서 처리
        for (int i = 0; i < iCount; ++i)
        {
            if (!m_listActive[i])
                continue;

            JobMissile refOwner = m_listOwnerAtIndex[i];

            float3 vDirOut = m_listMoveDirOut[i];
            refOwner.SyncMoveDir(new Vector3(vDirOut.x, vDirOut.y, vDirOut.z));

            if (m_listArrived[i])
            {
                refOwner.MarkArrived();
                m_listActive[i] = false; // OnDisable이 실제로 뜨기 전까지 한두 스텝 남는 중복 처리를 막음
                continue; // 도착한 스텝은 이동하지 않았으니 Apply 생략
            }

            float3 vPos = m_listPos[i];
            float3 vFwd = m_listForward[i];
            Vector3 vPosUnity = new Vector3(vPos.x, vPos.y, vPos.z);
            Vector3 vFwdUnity = new Vector3(vFwd.x, vFwd.y, vFwd.z);

            // 여기는 메인 스레드라 Quaternion.LookRotation을 그대로 써도 안전함
            refOwner.ApplyMove(vPosUnity, Quaternion.LookRotation(vFwdUnity));
        }
    }

    private void OnDestroy()
    {
        if (m_listPos.IsCreated) m_listPos.Dispose();
        if (m_listForward.IsCreated) m_listForward.Dispose();

        if (m_listSpeed.IsCreated) m_listSpeed.Dispose();
        if (m_listElapsedTime.IsCreated) m_listElapsedTime.Dispose();
        if (m_listTargetLength.IsCreated) m_listTargetLength.Dispose();
        if (m_listProximityRadius.IsCreated) m_listProximityRadius.Dispose();
        if (m_listRotationSpeed.IsCreated) m_listRotationSpeed.Dispose();
        if (m_listMaxRotationSpeed.IsCreated) m_listMaxRotationSpeed.Dispose();
        if (m_listRotateSpeedRate.IsCreated) m_listRotateSpeedRate.Dispose();

        if (m_listTargetPos.IsCreated) m_listTargetPos.Dispose();
        if (m_listArrived.IsCreated) m_listArrived.Dispose();
        if (m_listMoveDirOut.IsCreated) m_listMoveDirOut.Dispose();
        if (m_listActive.IsCreated) m_listActive.Dispose();
    }

    [BurstCompile]
    private struct MissileMoveJob : IJobParallelFor
    {
        public NativeArray<float3> ArrPos;
        public NativeArray<float3> ArrForward;

        [ReadOnly] public NativeArray<float> ArrSpeed;
        public NativeArray<float> ArrElapsedTime;
        [ReadOnly] public NativeArray<float> ArrTargetLength;
        [ReadOnly] public NativeArray<float> ArrProximityRadius;
        [ReadOnly] public NativeArray<float> ArrRotationSpeed;
        [ReadOnly] public NativeArray<float> ArrMaxRotationSpeed;
        [ReadOnly] public NativeArray<float> ArrRotateSpeedRate;

        [ReadOnly] public NativeArray<float3> ArrTargetPos;
        public NativeArray<bool> ArrArrived;
        public NativeArray<float3> ArrMoveDirOut;
        [ReadOnly] public NativeArray<bool> ArrActive;

        public float FDeltaTime;

        public void Execute(int index)
        {
            if (!ArrActive[index])
                return;

            // 원본 Missiles.UpdateDirMissile()과 동일하게, 도착 판정 이전에 경과시간부터 누적
            float fElapsed = ArrElapsedTime[index] + FDeltaTime;
            ArrElapsedTime[index] = fElapsed;

            float3 vPos = ArrPos[index];
            float3 vToTarget = ArrTargetPos[index] - vPos;
            float fDist = math.length(vToTarget);

            float fSpeed = ArrSpeed[index];
            float fMoveDist = fSpeed * FDeltaTime;
            float fArriveDist = math.max(fMoveDist, ArrProximityRadius[index]);

            // 이번 스텝에 이동할 거리보다 남은 거리가 짧으면 도착 처리하고 이번 스텝은 이동하지 않음
            if (fDist <= fArriveDist)
            {
                ArrArrived[index] = true;
                return;
            }

            float3 vDir = vToTarget / fDist;
            float3 vCurForward = ArrForward[index];

            float fDot = math.clamp(math.dot(vCurForward, vDir), -1f, 1f);
            float fAngleDeg = math.degrees(math.acos(fDot));

            // 시간 기반 가속 + 거리 기반 가속 합산, MaxRotationSpeed로 상한 (원본과 동일한 공식)
            float fTimeAccel = ArrRotateSpeedRate[index] * fElapsed;
            float fBaseSpeed = math.min(ArrRotationSpeed[index] + fTimeAccel, ArrMaxRotationSpeed[index]);
            float fDistAccel = (ArrTargetLength[index] / fDist) * fBaseSpeed * 0.5f;
            float fRotateSpeed = fBaseSpeed + fDistAccel;

            float fStep = fRotateSpeed * FDeltaTime;
            float t = (fAngleDeg > 0.001f) ? math.clamp(fStep / fAngleDeg, 0f, 1f) : 1f;

            // Vector3.Slerp(forward, dir, t)와 동일한 결과를 내는 구면보간 공식(두 단위벡터 사이).
            // 쿼터니언 없이 순수 벡터/사인 연산만으로 계산 가능해서 Burst에서도 완전히 안전함
            float fAngleRad = math.radians(fAngleDeg);
            float fSinAngle = math.sin(fAngleRad);

            float3 vNewForward;
            if (fSinAngle > 1e-6f)
            {
                float fRatioA = math.sin((1f - t) * fAngleRad) / fSinAngle;
                float fRatioB = math.sin(t * fAngleRad) / fSinAngle;
                vNewForward = math.normalize(fRatioA * vCurForward + fRatioB * vDir);
            }
            else
            {
                vNewForward = vDir;
            }

            ArrForward[index] = vNewForward;
            ArrPos[index] = vPos + vNewForward * fSpeed * FDeltaTime;
            ArrMoveDirOut[index] = vNewForward;
        }
    }
}
