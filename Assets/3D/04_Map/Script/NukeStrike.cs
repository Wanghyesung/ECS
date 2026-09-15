using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/*///////////////////////////////////////////
                NukeStrike
목적 : 핵폭탄의 착탄 지점(씬 앵커) + 카메라 컷신. BattleScene의 맵 중앙에 빈 오브젝트로 배치.
       미사일 자체는 SOAttackInfo.PoolPrefab(NukeMissile)을 Bullet.SpawnAttackObject로 꺼내 쓴다.
       미사일은 착탄점 + LaunchOffset에서 출발해 착탄점을 바라보며 대각선으로 진입한다.
       카메라: 낙하 전반은 미사일 뒤 위(로컬 오프셋)에서 추적(CameraManager.FollowTarget)하고,
       FollowRatio 지점부터는 맵 전경 지점으로 후진(MoveToPoint)해 착탄/임팩트를 넓게 보여준 뒤 복귀.
 *///////////////////////////////////////////

public sealed class NukeStrike : MonoBehaviour
{
    [Header("Missile")]
    [Tooltip("착탄점 기준 미사일 출발 위치. 대각선 진입 방향을 이 벡터가 결정")]
    [SerializeField] private Vector3 m_vLaunchOffset = new Vector3(-250f, 220f, -250f);

    [Header("Camera - Follow")]
    [Tooltip("미사일 로컬 오프셋(z- = 뒤, y+ = 위). 낙하 전반 동안 이 위치에서 미사일을 바라봄")]
    [SerializeField] private Vector3 m_vFollowOffset = new Vector3(10f, 30f, -80f);
    [Tooltip("낙하 시간 중 미사일을 따라가는 비율. 0.5 = 절반 지점부터 후진 시작")]
    [Range(0f, 1f)]
    [SerializeField] private float m_fFollowRatio = 0.5f;

    [Header("Camera - Overview")]
    [Tooltip("착탄 지점 기준 맵 전경 카메라 오프셋. 후진의 목적지")]
    [SerializeField] private Vector3 m_vOverviewOffset = new Vector3(0f, 220f, -420f);
    [Tooltip("착탄 후 전경에 머무는 시간(임팩트 감상용). 끝나면 timeScale 복귀")]
    [SerializeField] private float m_fHoldAfterImpact = 1.5f;

    // 씬 로컬 앵커라 DontDestroyOnLoad 없이 OnEnable/OnDisable로만 수명을 잡음
    public static NukeStrike Current { get; private set; }

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        if (Current == this)
            Current = null;
    }

    public void Fire(SOAttackInfo _SOAttackInfo)
    {
        FireAsync(_SOAttackInfo, this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid FireAsync(SOAttackInfo _SOAttackInfo, CancellationToken _token)
    {
        Vector3 vCenter = transform.position;

        tShotInfo tShot = new tShotInfo();
        tShot.TargetPos = vCenter;
        tShot.HitPosition = vCenter;

        Vector3 vLaunchPos = vCenter + m_vLaunchOffset;
        GameObject refObj = Bullet.SpawnAttackObject(_SOAttackInfo.PoolPrefab, vLaunchPos, Quaternion.LookRotation(-m_vLaunchOffset), _SOAttackInfo.MakeAttackInfo(), tShot);
        if (refObj == null || refObj.TryGetComponent(out NukeMissile refMissile) == false)
            return;

        // CardCreator.HandleCardClicked / JokerCardManager.PickData가 SelectFeature 직후 timeScale=1로
        await UniTask.Yield(_token);
        Time.timeScale = 0f;

        float fFollowTime = refMissile.FallTime * m_fFollowRatio;
        await CameraManager.m_Instance.FollowTarget(_token, refMissile.transform, m_vFollowOffset, fFollowTime);

        // 남은 낙하 시간 동안 전경 지점으로 후진 → 착탄과 동시에 도착. 끝나면 MoveToPoint가 timeScale=1 + 시점 복귀
        Vector3 vOverviewPos = vCenter + m_vOverviewOffset;
        await CameraManager.m_Instance.MoveToPoint(
            _token, vOverviewPos, Quaternion.LookRotation(vCenter - vOverviewPos), refMissile.FallTime - fFollowTime, m_fHoldAfterImpact);
    }
}
