using System;
using UnityEngine;

/*///////////////////////////////////////////
            LegacyBruteForceCollider
목적 : 커밋 421d1b0(공간분할 그리드 도입 직전) 시점의 BaseCollider를 그대로 옮긴
       벤치마크 전용 베이스 - LegacyBruteForceColliderManager가 그리드 없이 레이어
       리스트 이중 순회(N×M)로 판정하던 원본 알고리즘을 재현한다. 실제 BaseCollider와
       겹치면 안 되므로 별도 타입으로 두되, 판정에 관여하는 로직(ID/레이어/이벤트/
       생애주기)은 원본과 동일하다.

       Layer는 원본과 동일하게 gameObject.layer를 Awake에서 그대로 읽어 캐싱한다 -
       스포너가 AddComponent 전에 go.layer를 먼저 설정해둔다(그래야 Awake 시점에
       올바른 값을 읽는다). 인스펙터에서 실제 Layer가 보이므로 육안 확인도 가능하다.
 *///////////////////////////////////////////
public abstract class LegacyBruteForceCollider : MonoBehaviour
{
    public event Action<LegacyBruteForceCollider> OnHitTargetEnter;
    public event Action<LegacyBruteForceCollider> OnHitTargetStay;
    public event Action<LegacyBruteForceCollider> OnHitTargetExit;

    private static int s_iNextId = 0;

    public int Layer { get; private set; }
    public int ID { get; private set; }
    public eLegacyColliderShape Shape { get; protected set; }
    public Vector3 CachedCenter { get; protected set; }

    // 도형 무관 공통 반지름 상한 - LegacyBoxColliderGrid의 셀 크기 산정에 쓰인다(원본
    // BaseCollider.BoundingRadius와 동일 역할). Circle은 쓰지 않아 기본값 0f로 둔다
    public virtual float BoundingRadius => 0f;

    private bool m_bActivated = false;

    protected virtual void Awake()
    {
        ID = s_iNextId++;
        Layer = gameObject.layer;
    }

    protected virtual void Start()
    {
        LegacyBruteForceColliderManager.Instance.RegisterCollider(this);

        if (m_bActivated == false)
            Activate();
    }

    private void OnEnable()
    {
        if (LegacyBruteForceColliderManager.Instance != null)
            Activate();
    }

    private void Activate()
    {
        LegacyBruteForceColliderManager.Instance.Activate(this);
        m_bActivated = true;
    }

    private void OnDisable()
    {
        if (m_bActivated)
        {
            LegacyBruteForceColliderManager.Instance.UnActivate(this);
            m_bActivated = false;
        }
    }

    public virtual void RefreshCenter() { }

    public void OnEnterCollider(LegacyBruteForceCollider _refOther) => OnHitTargetEnter?.Invoke(_refOther);
    public void OnStayCollider(LegacyBruteForceCollider _refOther) => OnHitTargetStay?.Invoke(_refOther);
    public void OnExitCollider(LegacyBruteForceCollider _refOther) => OnHitTargetExit?.Invoke(_refOther);
}

public enum eLegacyColliderShape
{
    Circle,
    Box,
}
