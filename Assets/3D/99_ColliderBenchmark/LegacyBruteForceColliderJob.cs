using System;
using UnityEngine;

/*///////////////////////////////////////////
            LegacyBruteForceColliderJob
목적 : 커밋 daef9e3(Box 브로드페이즈를 Burst IJobParallelFor로 교체) 단계 전용 베이스.
       LegacyBruteForceCollider(8edea44 스냅샷, GridTestScene0)와 로직은 거의 동일하지만
       "코드를 바꾸지 말고 버전2로 새 파일을 만들라"는 지시에 따라 완전히 별도 타입으로
       분리했다 - GridTestScene0/1이 서로 독립적으로 그 시점 실측을 계속 재현할 수 있게
       하기 위함(한쪽을 고치면 다른 쪽 히스토리가 오염됨).

       LegacyBruteForceColliderManagerJob.Instance를 참조한다는 점만 V1과 다르다.
       Layer는 gameObject.layer를 Awake에서 캐싱 - 스포너가 AddComponent 전에
       go.layer를 먼저 설정해둔다.
 *///////////////////////////////////////////
public abstract class LegacyBruteForceColliderJob : MonoBehaviour
{
    public event Action<LegacyBruteForceColliderJob> OnHitTargetEnter;
    public event Action<LegacyBruteForceColliderJob> OnHitTargetStay;
    public event Action<LegacyBruteForceColliderJob> OnHitTargetExit;

    private static int s_iNextId = 0;

    public int Layer { get; private set; }
    public int ID { get; private set; }
    public eLegacyColliderShapeJob Shape { get; protected set; }
    public Vector3 CachedCenter { get; protected set; }

    // 도형 무관 공통 반지름 상한 - Box SoA 구성/그리드 셀 크기 산정에 쓰인다
    public virtual float BoundingRadius => 0f;

    private bool m_bActivated = false;

    protected virtual void Awake()
    {
        ID = s_iNextId++;
        Layer = gameObject.layer;
    }

    protected virtual void Start()
    {
        LegacyBruteForceColliderManagerJob.Instance.RegisterCollider(this);

        if (m_bActivated == false)
            Activate();
    }

    private void OnEnable()
    {
        if (LegacyBruteForceColliderManagerJob.Instance != null)
            Activate();
    }

    private void Activate()
    {
        LegacyBruteForceColliderManagerJob.Instance.Activate(this);
        m_bActivated = true;
    }

    private void OnDisable()
    {
        if (m_bActivated)
        {
            LegacyBruteForceColliderManagerJob.Instance.UnActivate(this);
            m_bActivated = false;
        }
    }

    public virtual void RefreshCenter() { }

    public void OnEnterCollider(LegacyBruteForceColliderJob _refOther) => OnHitTargetEnter?.Invoke(_refOther);
    public void OnStayCollider(LegacyBruteForceColliderJob _refOther) => OnHitTargetStay?.Invoke(_refOther);
    public void OnExitCollider(LegacyBruteForceColliderJob _refOther) => OnHitTargetExit?.Invoke(_refOther);
}

public enum eLegacyColliderShapeJob
{
    Circle,
    Box,
}
