using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/*///////////////////////////////////////////
              TargetScanner
목적 : 플레이어 주변의 몬스터를 파싱하는 역할
 *///////////////////////////////////////////
public class TargetScanner : MonoBehaviour
{
    private Transform m_refNearTargetTr;
    public Transform Target => m_refNearTargetTr;

    [SerializeField] private float m_fFindTime; //몇초 주기로 타겟을 찾을지
    [SerializeField] private float m_fFindRadius;

    [SerializeField] LayerMask m_tFindLayer;

    private CancellationTokenSource m_refCTS;

    // Start가 아니라 OnEnable에서 스캔을 켬 - Start는 오브젝트 생애주기 중 단 한 번만 불려서,
    // 드론처럼 SetActive로 껐다 켜지는 오브젝트는 재활성화 후 스캔이 영영 재개되지 않았음
    // (RadarSystem.cs와 동일한 수정)
    private void OnEnable()
    {
        m_refCTS = new CancellationTokenSource();
        FindTargetLoop(m_refCTS.Token).Forget();
    }

    private void OnDisable()
    {
        m_refCTS.Cancel();
        m_refCTS.Dispose();
        m_refNearTargetTr = null;
    }

    private async UniTaskVoid FindTargetLoop(CancellationToken _token)
    {

        TimeSpan tInterval = TimeSpan.FromSeconds(m_fFindTime);
        await UniTask.WaitUntil(() => ColliderManager.m_Instance != null, cancellationToken : _token);

        while (true)
        {
            ColliderManager.m_Instance.FindNearest(transform.position, m_fFindRadius, m_tFindLayer, out CircleCollider refNearest);
            m_refNearTargetTr = refNearest != null ? refNearest.transform : null;

            await UniTask.Delay(tInterval, cancellationToken: _token);
        }
    }
}
