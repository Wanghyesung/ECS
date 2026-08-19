using NUnit.Framework;
using UnityEngine;

/*///////////////////////////////////////////
           ColliderManagerTests
목적 : ColliderManager.IsCircleBoxOverlap(원-OBB 판정 순수 함수)를 씬 없이 검증한다.
       계획서(운석 Box-Circle 충돌 판정) §완료 기준 5가지를 그대로 테스트 케이스로 옮김.
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
}
