using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/*///////////////////////////////////////////
              RadarSystem
목적 : 플레이어 주변 몬스터를 주기적으로 스캔해 RadarView(미니맵)와
       OffscreenIndicatorView(화면 밖 화살표)에 동시에 반영.
       두 View가 같은 몬스터 목록을 쓰므로 스캔은 한 번만 돈다
 *///////////////////////////////////////////
public sealed class RadarSystem : MonoBehaviour
{
    // 몬스터를 가까운 순으로 정렬하기 위한 비교자 - 매 틱 재사용(할당 없음), Origin만 갱신
    private sealed class DistanceComparer : IComparer<CircleCollider>
    {
        public Vector3 Origin;

        public int Compare(CircleCollider _refA, CircleCollider _refB)
        {
            float fDistA = (_refA.CachedCenter - Origin).sqrMagnitude;
            float fDistB = (_refB.CachedCenter - Origin).sqrMagnitude;
            return fDistA.CompareTo(fDistB);
        }
    }

    [SerializeField] private RadarView m_refView = null;
    [SerializeField] private OffscreenIndicatorView m_refOffscreenView = null;
    [SerializeField] private float m_fRadarRange = 300f;
    [SerializeField] private float m_fScanInterval = 0.1f; // 매 프레임까지는 필요 없어 TargetScanner와 동일한 주기 스캔 방식
    [SerializeField] private LayerMask m_tMonsterLayer;

    private Camera m_refCamera;
    private readonly List<CircleCollider> m_listResult = new List<CircleCollider>();
    private readonly DistanceComparer m_refDistanceComparer = new DistanceComparer();
    private CancellationTokenSource m_refCTS;

    private void Awake()
    {
        m_refCamera = Camera.main;
    }

    // Start가 아니라 OnEnable에서 스캔을 켬 - 상위 Canvas/오브젝트가 잠깐 꺼졌다 켜져도
    // (레벨업 카드 팝업 등으로) CTS를 새로 만들어 다시 도니까 스캔이 영영 안 멈춤.
    // Start()는 오브젝트 생애주기 중 단 한 번만 불려서, 그 안에서 루프를 시작하면
    // OnDisable로 취소된 뒤 재활성화돼도 다시 안 돎(TargetScanner에 있던 것과 동일한 함정 - 그쪽도 같이 수정함)
    private void OnEnable()
    {
        m_refCTS = new CancellationTokenSource();
        ScanLoop(m_refCTS.Token).Forget();
    }

    private void OnDisable()
    {
        m_refCTS.Cancel();
        m_refCTS.Dispose();

        // 마지막으로 그려진 블립/화살표가 화면에 얼어붙어 남지 않도록 정리
        if (m_refView != null)
            m_refView.HideBlipsFrom(0);
        if (m_refOffscreenView != null)
            m_refOffscreenView.HideArrowsFrom(0);
    }

    private async UniTaskVoid ScanLoop(CancellationToken _token)
    {
        TimeSpan tInterval = TimeSpan.FromSeconds(m_fScanInterval);

        while (true)
        {
            // 한 프레임의 예외(잘못 연결된 참조, NaN 등)로 루프 자체가 영구히 멈추지 않도록 격리.
            // await는 밖에 둬서 취소(OperationCanceledException) 전파는 그대로 유지됨
            try
            {
                RefreshHud();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            await UniTask.Delay(tInterval, cancellationToken: _token);
        }
    }

    private void RefreshHud()
    {
        if (m_refView == null)
            return;

        if (Player.CurrentPlayer == null)
        {
            m_refView.HideBlipsFrom(0);
            if (m_refOffscreenView != null)
                m_refOffscreenView.HideArrowsFrom(0);
            return;
        }

        if (ColliderManager.m_Instance == null)
            return;

        Transform refPlayerTr = Player.CurrentPlayer.transform;
        ColliderManager.m_Instance.FindAllInRadius(refPlayerTr.position, m_fRadarRange, m_tMonsterLayer, m_listResult);

        // 가까운 순 정렬 - 슬롯(20개)보다 몬스터가 많을 때 가장 위협적인(가까운) 대상이 밀려나지 않게 함
        m_refDistanceComparer.Origin = refPlayerTr.position;
        m_listResult.Sort(m_refDistanceComparer);

        RefreshRadarBlips(refPlayerTr);
        RefreshOffscreenArrows();
    }

    private void RefreshRadarBlips(Transform _refPlayerTr)
    {
        int iShown = Mathf.Min(m_listResult.Count, m_refView.MaxBlipCount);
        for (int i = 0; i < iShown; ++i)
        {
            Vector3 vOffset = m_listResult[i].CachedCenter - _refPlayerTr.position;
            // 플레이어 진행방향 기준 상대좌표로 회전 - 배가 도는 방향에 맞춰 레이더도 같이 돎
            Vector3 vLocalOffset = _refPlayerTr.InverseTransformDirection(vOffset);

            Vector2 vNormalizedOffset = new Vector2(vLocalOffset.x, vLocalOffset.z) / m_fRadarRange;
            m_refView.SetBlip(i, vNormalizedOffset);
        }

        m_refView.HideBlipsFrom(iShown);
    }

    // 탐지 범위 안이지만 카메라 시야 밖인 몬스터만 화면 가장자리에 화살표로 표시
    private void RefreshOffscreenArrows()
    {
        if (m_refOffscreenView == null)
            return;

        // Camera.main을 Awake에서만 캐싱하면 그 시점에 아직 카메라가 없는 경우 영영 null로 남으므로,
        // 매번 null일 때만 재시도(정상 상황에서는 캐싱된 값을 그대로 씀 - 매 틱 Camera.main 호출 아님)
        if (m_refCamera == null)
        {
            m_refCamera = Camera.main;
            if (m_refCamera == null)
            {
                m_refOffscreenView.HideArrowsFrom(0);
                return;
            }
        }

        RectTransform refRoot = m_refOffscreenView.Root;
        if (refRoot == null)
            return;

        Rect tScreenRect = refRoot.rect;
        float fHalfW = Mathf.Max(tScreenRect.width * 0.5f - m_refOffscreenView.EdgePadding, 0f);
        float fHalfH = Mathf.Max(tScreenRect.height * 0.5f - m_refOffscreenView.EdgePadding, 0f);

        int iArrowIndex = 0;
        int iMaxArrows = m_refOffscreenView.MaxArrowCount;
        int iCount = m_listResult.Count;

        for (int i = 0; i < iCount && iArrowIndex < iMaxArrows; ++i)
        {
            //내 뷰포트 안 쪽 오브젝트는 스킵
            Vector3 vViewport = m_refCamera.WorldToViewportPoint(m_listResult[i].CachedCenter);
            bool bOnScreen = vViewport.z > 0f && vViewport.x >= 0f && vViewport.x <= 1f && vViewport.y >= 0f && vViewport.y <= 1f;
            if (bOnScreen)
                continue;

            // 뷰포트 비율(0~1)이 아니라 실제 픽셀 비율로 방향을 잡아야 화면 종횡비가 정사각형이
            // 아닐 때도 각도/위치가 안 틀어짐 - 여기서 rect 크기를 곱해 픽셀 공간으로 변환
            Vector2 vDir = new Vector2(
                (vViewport.x - 0.5f) * tScreenRect.width,
                (vViewport.y - 0.5f) * tScreenRect.height);

            // 카메라 뒤쪽 대상은 뷰포트 투영이 반전되므로 방향을 다시 뒤집어줌
            if (vViewport.z < 0f)
                vDir = -vDir;

            // 카메라 근접 평면(z~0) 부근에서 WorldToViewportPoint가 Inf/NaN을 낼 수 있음 -
            // sqrMagnitude 비교로는 안 걸러지므로 명시적으로 체크
            if (float.IsNaN(vDir.x) || float.IsNaN(vDir.y) || float.IsInfinity(vDir.x) || float.IsInfinity(vDir.y))
                continue;

            if (vDir.sqrMagnitude < 0.0001f)
                vDir = Vector2.up;
            vDir.Normalize();

            // 화면 중심에서 dir 방향으로 쏜 광선이 여백을 뺀 화면 사각형과 만나는 지점 계산
            float fScaleX = vDir.x != 0f ? fHalfW / Mathf.Abs(vDir.x) : float.MaxValue;
            float fScaleY = vDir.y != 0f ? fHalfH / Mathf.Abs(vDir.y) : float.MaxValue;
            float fScale = Mathf.Min(fScaleX, fScaleY);

            Vector2 vClampedPos = vDir * fScale;
            // 화살표 스프라이트 기본 방향이 위(+Y)라고 가정하고 dir을 향하도록 회전각 계산
            float fAngle = Mathf.Atan2(-vDir.x, vDir.y) * Mathf.Rad2Deg;

            m_refOffscreenView.SetArrow(iArrowIndex, vClampedPos, fAngle);
            ++iArrowIndex;
        }

        m_refOffscreenView.HideArrowsFrom(iArrowIndex);
    }
}
