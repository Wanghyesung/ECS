using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

/*///////////////////////////////////////////
              ColliderCenterRefresher
목적 : 콜라이더의 위치/축(transform.position/rotation) 갱신만 전담한다 - "충돌 판정"과
       분리된 "이동 추적" 관심사. TransformAccessArray + Burst Job으로 transform 읽기를
       워커 스레드에 흩뿌려서, 관리형↔네이티브 경계를 매번 넘는 메인스레드 가상 호출 비용을 없앤다.

       Bullet/Missile/GuidedMoveManager와 같은 TransformAccessArray+Job 패턴이지만, 그것들처럼
       "풀에 영구 등록 + 활성 플래그만 토글"하지 않는다 - Obstacle/Monster/Player 등 실제로
       Destroy될 수 있는 콜라이더까지 다루므로 Register/Unregister로 스왑백 등록/해제한다
       (파괴 전에 반드시 빼야 다음 Job이 죽은 Transform을 참조하는 사고를 막을 수 있다).

       ColliderManager가 소유하는 순수 C# 클래스(BoxColliderGrid와 동일 패턴).
 *///////////////////////////////////////////
public sealed class ColliderCenterRefresher
{
    private const int MIN_CAPACITY = 64;

    // ID -> m_transformArray/출력 배열에서의 슬롯(스왑백 O(1) 제거용). 미등록(Static/비활성)이면 -1
    private readonly List<int> m_listTransformSlot = new List<int>();
    // 슬롯 -> 그 자리의 콜라이더 ID (스왑백 시 반대 방향 갱신용)
    private readonly List<int> m_listColliderIdBySlot = new List<int>();
    private TransformAccessArray m_transformArray;

    // Offset은 등록 시점에 한 번만 채워지는 입력값이라 배열이 자랄 때 기존 내용을 복사해서
    // 보존해야 한다(다른 배열과 다른 유일한 이유). Center/AxisX/Y/Z는 Job이 매 프레임 통째로
    // 덮어쓰는 순수 출력이라 보존 불필요
    private NativeArray<Vector3> m_arrOffset;
    private NativeArray<Vector3> m_arrCenter;
    private NativeArray<Vector3> m_arrAxisX;
    private NativeArray<Vector3> m_arrAxisY;
    private NativeArray<Vector3> m_arrAxisZ;

    public ColliderCenterRefresher(int _iInitialCapacity)
    {
        m_transformArray = new TransformAccessArray(_iInitialCapacity);
        AllocateArrays(Mathf.Max(_iInitialCapacity, MIN_CAPACITY));
    }

    // ColliderManager.ResizeCapacity와 짝을 맞춰 ID 기준 슬롯 리스트 크기를 키운다
    public void ResizeIdCapacity(int _iID)
    {
        while (m_listTransformSlot.Count <= _iID)
            m_listTransformSlot.Add(-1);
    }

    // Static은 위치가 안 바뀌므로 애초에 등록하지 않는다
    public void Register(BaseCollider _refCollider)
    {
        if (_refCollider.StaticObject)
            return;

        int iSlot = m_transformArray.length;
        ResizeArrayCapacity(iSlot + 1);

        m_transformArray.Add(_refCollider.transform);
        m_listColliderIdBySlot.Add(_refCollider.ID);
        m_listTransformSlot[_refCollider.ID] = iSlot;
        m_arrOffset[iSlot] = _refCollider.Offset;
    }

    // 스왑백 제거 - 마지막 슬롯이 이 자리로 옮겨오므로, 옮겨온 콜라이더 쪽 슬롯 기록도 갱신
    public void Unregister(int _iID)
    {
        int iSlot = m_listTransformSlot[_iID];
        if (iSlot < 0)
            return; // Static이라 애초에 등록 안 됨

        int iLastSlot = m_transformArray.length - 1;
        m_transformArray.RemoveAtSwapBack(iSlot);

        if (iSlot != iLastSlot)
        {
            int iMovedId = m_listColliderIdBySlot[iLastSlot];
            m_listTransformSlot[iMovedId] = iSlot;
            m_listColliderIdBySlot[iSlot] = iMovedId;
            m_arrOffset[iSlot] = m_arrOffset[iLastSlot];
        }

        m_listColliderIdBySlot.RemoveAt(iLastSlot);
        m_listTransformSlot[_iID] = -1;
    }

    // Job을 Schedule+Complete까지 동기적으로 끝낸다 - 호출부가 곧바로 Apply를 부를 수 있게
    public void ScheduleAndComplete()
    {
        if (m_transformArray.length == 0)
            return;

        RefreshCenterJob tJob = new RefreshCenterJob
        {
            Offset = m_arrOffset,
            Center = m_arrCenter,
            AxisX = m_arrAxisX,
            AxisY = m_arrAxisY,
            AxisZ = m_arrAxisZ,
        };

        tJob.Schedule(m_transformArray).Complete();
    }

    // ScheduleAndComplete 이후에만 유효 - Job 결과를 콜라이더 자신에게 되돌려 쓴다
    public void Apply(BaseCollider _refCollider)
    {
        int iSlot = m_listTransformSlot[_refCollider.ID];
        if (iSlot < 0)
            return; // Static - 등록 자체가 안 됨

        _refCollider.ApplyCachedCenter(m_arrCenter[iSlot]);

        if (_refCollider is ObbCollider refBox)
            refBox.ApplyAxis(m_arrAxisX[iSlot], m_arrAxisY[iSlot], m_arrAxisZ[iSlot]);
    }

    public void Dispose()
    {
        if (m_transformArray.isCreated)
            m_transformArray.Dispose();

        DisposeArrays();
    }

    private void ResizeArrayCapacity(int _iCount)
    {
        if (m_arrCenter.IsCreated && m_arrCenter.Length >= _iCount)
            return;

        int iNewCapacity = m_arrCenter.IsCreated ? m_arrCenter.Length : MIN_CAPACITY;
        while (iNewCapacity < _iCount)
            iNewCapacity <<= 1;

        NativeArray<Vector3> arrNewOffset = new NativeArray<Vector3>(iNewCapacity, Allocator.Persistent);
        if (m_arrOffset.IsCreated)
        {
            NativeArray<Vector3>.Copy(m_arrOffset, arrNewOffset, m_arrOffset.Length);
            m_arrOffset.Dispose();
        }
        m_arrOffset = arrNewOffset;

        DisposeOutputArrays();
        AllocateOutputArrays(iNewCapacity);
    }

    private void AllocateArrays(int _iCapacity)
    {
        m_arrOffset = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        AllocateOutputArrays(_iCapacity);
    }

    private void AllocateOutputArrays(int _iCapacity)
    {
        m_arrCenter = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        m_arrAxisX = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        m_arrAxisY = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
        m_arrAxisZ = new NativeArray<Vector3>(_iCapacity, Allocator.Persistent);
    }

    private void DisposeArrays()
    {
        if (m_arrOffset.IsCreated)
            m_arrOffset.Dispose();

        DisposeOutputArrays();
    }

    private void DisposeOutputArrays()
    {
        if (m_arrCenter.IsCreated)
            m_arrCenter.Dispose();
        if (m_arrAxisX.IsCreated)
            m_arrAxisX.Dispose();
        if (m_arrAxisY.IsCreated)
            m_arrAxisY.Dispose();
        if (m_arrAxisZ.IsCreated)
            m_arrAxisZ.Dispose();
    }

    // TransformAccess.rotation은 이미 추출된 값이라 Circle/Box 가리지 않고 축까지 매번 계산해도
    // 싸다(추가 네이티브 호출이 아니라 Burst 쿼터니언 곱셈일 뿐) - Circle 슬롯의 Axis는 안 읽히고 버려짐
    [BurstCompile]
    private struct RefreshCenterJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<Vector3> Offset;
        public NativeArray<Vector3> Center;
        public NativeArray<Vector3> AxisX;
        public NativeArray<Vector3> AxisY;
        public NativeArray<Vector3> AxisZ;

        public void Execute(int index, TransformAccess _transform)
        {
            Quaternion tRotation = _transform.rotation;

            Center[index] = _transform.position + tRotation * Offset[index];
            AxisX[index] = tRotation * Vector3.right;
            AxisY[index] = tRotation * Vector3.up;
            AxisZ[index] = tRotation * Vector3.forward;
        }
    }
}
