using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

/*///////////////////////////////////////////
           ColliderManagerTests
목적 : ColliderManager의 씬 독립적인 순수 함수들을 검증한다 - 도형별 겹침 판정
       (IsCircleBoxOverlap/IsCircleCircleOverlap)과 레이어 매트릭스 판정(IsLayerCollider).
       전부 GridOverlapJob이 Execute(index) 안에서 그대로 호출하는 함수들이라, 여기서
       독립적으로 검증해두면 Job 자체를 NativeArray로 직접 스케줄해보지 않아도 로직을 신뢰할 수 있다.
 *///////////////////////////////////////////
public class ColliderManagerTests
{
    private static readonly Vector3 AXIS_X = Vector3.right;
    private static readonly Vector3 AXIS_Y = Vector3.up;
    private static readonly Vector3 AXIS_Z = Vector3.forward;

    [Test]
    public void IsCircleBoxOverlap_구가_박스에서_멀리_떨어져있으면_겹치지_않는다()
    {
        bool bOverlap = ColliderManager.IsCircleBoxOverlap(
            _vSphereCenter: new Vector3(100f, 0f, 0f), _fRadius: 1f,
            _vBoxCenter: Vector3.zero, _vAxisX: AXIS_X, _vAxisY: AXIS_Y, _vAxisZ: AXIS_Z,
            _vHalfExtent: new Vector3(1f, 1f, 1f));

        Assert.IsFalse(bOverlap);
    }

    [Test]
    public void IsCircleBoxOverlap_구_중심이_박스_내부에_있으면_겹친다()
    {
        bool bOverlap = ColliderManager.IsCircleBoxOverlap(
            _vSphereCenter: Vector3.zero, _fRadius: 0.1f,
            _vBoxCenter: Vector3.zero, _vAxisX: AXIS_X, _vAxisY: AXIS_Y, _vAxisZ: AXIS_Z,
            _vHalfExtent: new Vector3(1f, 1f, 1f));

        Assert.IsTrue(bOverlap);
    }

    [Test]
    public void IsCircleBoxOverlap_구가_박스_표면에_정확히_반지름만큼_닿으면_겹친다()
    {
        // 박스 표면(x=1)에서 반지름(1)만큼 떨어진 x=2 지점 - 거리 = 반지름과 정확히 같음(경계 포함, <=)
        bool bOverlap = ColliderManager.IsCircleBoxOverlap(
            _vSphereCenter: new Vector3(2f, 0f, 0f), _fRadius: 1f,
            _vBoxCenter: Vector3.zero, _vAxisX: AXIS_X, _vAxisY: AXIS_Y, _vAxisZ: AXIS_Z,
            _vHalfExtent: new Vector3(1f, 1f, 1f));

        Assert.IsTrue(bOverlap);
    }

    [Test]
    public void IsCircleBoxOverlap_박스가_회전해도_로컬_축_기준으로_올바르게_판정한다()
    {
        // Y축 45도 회전 - 월드축 기준으로 안 겹치는 것처럼 보이지만(x=1.5 > halfExtent.x=1),
        // 박스의 실제 로컬 축(대각선 방향)으로 투영하면 겹친다. 만약 구현이 회전을 무시하고
        // 월드 축을 그대로 쓰면 이 테스트가 실패해서 바로 잡아낸다.
        Quaternion tRotation = Quaternion.Euler(0f, 45f, 0f);
        Vector3 vRotatedAxisX = tRotation * Vector3.right;
        Vector3 vRotatedAxisZ = tRotation * Vector3.forward;

        bool bOverlap = ColliderManager.IsCircleBoxOverlap(
            _vSphereCenter: new Vector3(1.5f, 0f, 0f), _fRadius: 0.1f,
            _vBoxCenter: Vector3.zero, _vAxisX: vRotatedAxisX, _vAxisY: AXIS_Y, _vAxisZ: vRotatedAxisZ,
            _vHalfExtent: new Vector3(1f, 1f, 1f));

        Assert.IsTrue(bOverlap);
    }

    [Test]
    public void IsCircleBoxOverlap_축별로_다른_halfExtent가_각각_독립적으로_반영된다()
    {
        Vector3 vHalfExtent = new Vector3(2f, 1f, 0.5f);

        // X축은 halfExtent.x=2라 0.6만큼 떨어진 지점은 여전히 박스 내부 -> 반지름이 0이어도 겹침
        bool bOverlapOnWideAxis = ColliderManager.IsCircleBoxOverlap(
            _vSphereCenter: new Vector3(0.6f, 0f, 0f), _fRadius: 0f,
            _vBoxCenter: Vector3.zero, _vAxisX: AXIS_X, _vAxisY: AXIS_Y, _vAxisZ: AXIS_Z,
            _vHalfExtent: vHalfExtent);

        // 같은 0.6 오프셋이라도 Z축은 halfExtent.z=0.5라 박스 밖 -> 남는 거리 0.1이 작은 반지름(0.05)보다 커서 안 겹침
        bool bOverlapOnNarrowAxis = ColliderManager.IsCircleBoxOverlap(
            _vSphereCenter: new Vector3(0f, 0f, 0.6f), _fRadius: 0.05f,
            _vBoxCenter: Vector3.zero, _vAxisX: AXIS_X, _vAxisY: AXIS_Y, _vAxisZ: AXIS_Z,
            _vHalfExtent: vHalfExtent);

        Assert.IsTrue(bOverlapOnWideAxis);
        Assert.IsFalse(bOverlapOnNarrowAxis);
    }

    [Test]
    public void IsCircleCircleOverlap_두_원이_멀리_떨어져있으면_겹치지_않는다()
    {
        bool bOverlap = ColliderManager.IsCircleCircleOverlap(
            _vCenterA: Vector3.zero, _fRadiusA: 1f,
            _vCenterB: new Vector3(100f, 0f, 0f), _fRadiusB: 1f);

        Assert.IsFalse(bOverlap);
    }

    [Test]
    public void IsCircleCircleOverlap_두_원이_겹치는_위치에_있으면_겹친다()
    {
        bool bOverlap = ColliderManager.IsCircleCircleOverlap(
            _vCenterA: Vector3.zero, _fRadiusA: 1f,
            _vCenterB: new Vector3(1.5f, 0f, 0f), _fRadiusB: 1f);

        Assert.IsTrue(bOverlap);
    }

    [Test]
    public void IsCircleCircleOverlap_두_원_반지름_합과_거리가_정확히_같으면_겹친다()
    {
        // 중심 거리 = 2, 반지름 합 = 1+1 = 2 - 정확히 맞닿음(경계 포함, <=)
        bool bOverlap = ColliderManager.IsCircleCircleOverlap(
            _vCenterA: Vector3.zero, _fRadiusA: 1f,
            _vCenterB: new Vector3(2f, 0f, 0f), _fRadiusB: 1f);

        Assert.IsTrue(bOverlap);
    }

    [Test]
    public void IsCircleCircleOverlap_반지름이_달라도_거리와_반지름합만으로_판정한다()
    {
        // 중심 거리 = 3, 반지름 합 = 0.5+2 = 2.5 -> 안 겹침
        bool bOverlapFar = ColliderManager.IsCircleCircleOverlap(
            _vCenterA: Vector3.zero, _fRadiusA: 0.5f,
            _vCenterB: new Vector3(3f, 0f, 0f), _fRadiusB: 2f);

        // 중심 거리 = 2, 반지름 합 = 0.5+2 = 2.5 -> 겹침
        bool bOverlapNear = ColliderManager.IsCircleCircleOverlap(
            _vCenterA: Vector3.zero, _fRadiusA: 0.5f,
            _vCenterB: new Vector3(2f, 0f, 0f), _fRadiusB: 2f);

        Assert.IsFalse(bOverlapFar);
        Assert.IsTrue(bOverlapNear);
    }

    [Test]
    public void IsLayerCollider_매트릭스에_등록된_레이어_쌍은_충돌로_인식된다()
    {
        NativeArray<int> arrMatrix = new NativeArray<int>(4, Allocator.Temp);
        try
        {
            arrMatrix[0] = 1 << 1; // 레이어0 -> 레이어1과 충돌
            arrMatrix[1] = 0;
            arrMatrix[2] = 0;
            arrMatrix[3] = 0;

            Assert.IsTrue(ColliderManager.IsLayerCollider(arrMatrix, 0, 1));
        }
        finally
        {
            arrMatrix.Dispose();
        }
    }

    [Test]
    public void IsLayerCollider_한쪽_방향만_등록해도_양방향으로_인식된다()
    {
        // Unity Physics 매트릭스처럼 A->B만 등록해도 B->A 조회에서도 true여야 함
        NativeArray<int> arrMatrix = new NativeArray<int>(4, Allocator.Temp);
        try
        {
            arrMatrix[0] = 1 << 1;
            arrMatrix[1] = 0; // 반대 방향은 비어있음
            arrMatrix[2] = 0;
            arrMatrix[3] = 0;

            Assert.IsTrue(ColliderManager.IsLayerCollider(arrMatrix, 1, 0));
        }
        finally
        {
            arrMatrix.Dispose();
        }
    }

    [Test]
    public void IsLayerCollider_매트릭스에_없는_레이어_쌍은_충돌로_인식되지_않는다()
    {
        NativeArray<int> arrMatrix = new NativeArray<int>(4, Allocator.Temp);
        try
        {
            arrMatrix[0] = 1 << 1; // 레이어0은 레이어1하고만 충돌
            arrMatrix[1] = 0;
            arrMatrix[2] = 0;
            arrMatrix[3] = 0;

            Assert.IsFalse(ColliderManager.IsLayerCollider(arrMatrix, 0, 2));
            Assert.IsFalse(ColliderManager.IsLayerCollider(arrMatrix, 2, 3));
        }
        finally
        {
            arrMatrix.Dispose();
        }
    }
}
