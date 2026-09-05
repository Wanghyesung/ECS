using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using System.Collections.Generic;

/*///////////////////////////////////////////
              MissileMoveManager
목적 : Missiles(가속 회전으로 유도되다 목표 근처에서 도착 처리되는 총알) 전용 이동 매니저.
       별도의 JobMissile 서브클래스 없이 Missiles가 직접 이 매니저에 등록됨.

       TransformAccessArray를 써서 Job이 Transform(위치+회전)에 직접 쓴다. 프리팹에
       Rigidbody/Collider가 이미 없어서 Physics.SyncColliderTransform 비용이 없으므로
       안전하게 쓸 수 있음.

       원본의 Vector3.Slerp(forward, dir, t)는 쿼터니언이 아니라 두 단위벡터 사이의
       구면보간이라, Burst 안에서 벡터 슬러프 공식을 직접 계산한다(회전각 기반 사인 보간).
       최종 forward가 나오면 quaternion.LookRotationSafe로 회전을 만들어 바로 Transform에
       쓴다 - Unity.Mathematics.quaternion은 Burst에서 쓰라고 만들어진 타입이라 안전함
       (UnityEngine.Quaternion과는 암시적으로 상호 변환됨).

       도착 판정(MarkArrived)/넉백 방향 동기화(SyncMoveDir)는 매니지드 오브젝트 호출이라
       Job 안에서 못 하므로, Complete() 이후 메인 스레드에서 활성 슬롯만 순회하며 처리한다.
       위치/회전은 이미 Job이 다 써놨으니 이 루프는 그 두 가지 부수효과만 처리하면 됨.

       [DefaultExecutionOrder(500)]: Unity는 서로 다른 스크립트의 Update() 순서를 보장하지
       않는다. 총알을 쏘는 Weapon 쪽 Update()가 이 매니저의 Update()보다 먼저 끝나야
       Activate()가 "이미 Schedule되어 아직 Complete 안 된" NativeList를 건드리는 경합이
       안 생긴다 - 그래서 기본 순서(0)보다 확실히 뒤로 미뤄둠 (ColliderManager의 1000보다는 앞)
 *///////////////////////////////////////////

[DefaultExecutionOrder(500)]
public class MissileMoveManager : MonoBehaviour
{
    public static MissileMoveManager m_Instance = null;

    private TransformAccessArray m_transformArray;

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

    private List<Missiles> m_listOwnerAtIndex;
    private List<Transform> m_listTargetTr;
    private List<bool> m_listTraceTarget;
    private List<Vector3> m_listFixedTargetPos;

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

        m_listOwnerAtIndex = new List<Missiles>(m_iInitialCapacity);
        m_listTargetTr = new List<Transform>(m_iInitialCapacity);
        m_listTraceTarget = new List<bool>(m_iInitialCapacity);
        m_listFixedTargetPos = new List<Vector3>(m_iInitialCapacity);
    }

    // Missiles.Awake()에서 총알 생애주기 중 딱 한 번만 호출
    public int RegisterPermanent(Missiles _refOwner)
    {
        int iIndex = m_listSpeed.Length;

        m_transformArray.Add(_refOwner.transform);

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

    // Missiles.SetAttack()(발사 시점)에서 호출
    public void Activate(int _iIndex, float _fSpeed, float _fTargetLength,
        float _fProximityRadius, float _fRotationSpeed, float _fMaxRotationSpeed, float _fRotateSpeedRate,
        bool _bTraceTarget, Transform _refTargetTr, Vector3 _vFixedTargetPos)
    {
        if (m_bDisposed)
            return;

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

        ++COUNt;
    }

    // Missiles.OnDisable()(풀 반납 시점)에서 호출. 플레이모드 종료/씬 전환 시 다른 오브젝트의
    // OnDisable이 이 매니저의 OnDestroy(NativeList Dispose) 이후에 불릴 수 있어서 가드 필요
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
            FDeltaTime = Time.deltaTime
        };

        m_tHandle = job.Schedule(m_transformArray);
        m_bScheduled = true;
    }

    private void LateUpdate()
    {
        if (!m_bScheduled)
            return;

        m_tHandle.Complete();
        m_bScheduled = false;

        // 위치/회전은 Job이 이미 다 써놨으니, 여기선 매니지드 오브젝트 호출이 필요한
        // 부수효과(넉백 방향 동기화, 도착 시 풀 반납)만 처리
        int iCount = m_listSpeed.Length;
        for (int i = 0; i < iCount; ++i)
        {
            if (!m_listActive[i])
                continue;

            Missiles refOwner = m_listOwnerAtIndex[i];

            float3 vDirOut = m_listMoveDirOut[i];
            refOwner.SyncMoveDir(new Vector3(vDirOut.x, vDirOut.y, vDirOut.z));

            if (m_listArrived[i] == true)
            {
                refOwner.Arrived();
                m_listActive[i] = false; // OnDisable이 실제로 뜨기 전까지 한두 스텝 남는 중복 처리를 막음
            }
        }
    }

    private void OnDestroy()
    {
        m_bDisposed = true;

        if (m_bScheduled)
            m_tHandle.Complete();

        if (m_transformArray.isCreated) m_transformArray.Dispose();

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
    private struct MissileMoveJob : IJobParallelForTransform
    {
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

        public void Execute(int index, TransformAccess _transform)
        {
            if (!ArrActive[index])
                return;

            // 원본 Missiles.UpdateDirMissile()과 동일하게, 도착 판정 이전에 경과시간부터 누적
            float fElapsed = ArrElapsedTime[index] + FDeltaTime;
            ArrElapsedTime[index] = fElapsed;

            float3 vPos = _transform.position;
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
            quaternion qRot = _transform.rotation;
            float3 vCurForward = math.mul(qRot, new float3(0f, 0f, 1f));

            float fDot = math.clamp(math.dot(vCurForward, vDir), -1f, 1f);
            float fAngleDeg = math.degrees(math.acos(fDot));

            // 시간 기반 가속 + 거리 기반 가속 합산, MaxRotationSpeed로 상한 (원본과 동일한 공식)
            float fTimeAccel = ArrRotateSpeedRate[index] * fElapsed;
            float fBaseSpeed = math.min(ArrRotationSpeed[index] + fTimeAccel, ArrMaxRotationSpeed[index]);
            float fDistAccel = (ArrTargetLength[index] / fDist) * fBaseSpeed * 0.5f;
            float fRotateSpeed = fBaseSpeed + fDistAccel;

            float fStep = fRotateSpeed * FDeltaTime;
            float t = math.clamp(fStep / fAngleDeg, 0f, 1f);

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

            _transform.position = vPos + vNewForward * fSpeed * FDeltaTime;
            _transform.rotation = quaternion.LookRotationSafe(vNewForward, math.up());
            ArrMoveDirOut[index] = vNewForward;
        }
    }
}
