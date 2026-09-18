using UnityEngine;

/*///////////////////////////////////////////
                    WeaponCon
목적 : 무기에 붙어 준비된 탄환의 크기와 차지 효과를 관리하고,
       버튼을 놓을 때만 최종 반경·속도를 Weapon에 전달한다.
 *///////////////////////////////////////////
[DisallowMultipleComponent]
[RequireComponent(typeof(Weapon))]
public sealed class WeaponCon : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float m_fMaxChargeTime = 2f;
    [SerializeField, Min(1f)] private float m_fMaxVisualScale = 2f;
    [SerializeField, Min(1f)] private float m_fMaxSpeedMultiplier = 2.5f;
    [SerializeField] private ParticleSystem m_refFullChargeEffect;

    private Weapon m_refWeapon;
    private GameObject m_refBulletObj;
    private Transform m_refBulletTr;
    private Transform m_refPoolParent;
    private CircleCollider m_refCircleCollider;
    private PoolObject m_refPoolObj;
    private Vector3 m_vBaseScale;
    private float m_fBaseRadius;
    private float m_fChargeTimer;
    private int m_iGeneration;
    private bool m_bFullCharge;
    private bool m_bOwnsFullChargeEffect;

    private bool OwnsBullet => m_refPoolObj != null &&
        m_refPoolObj.Generation == m_iGeneration && m_refPoolObj.PushCount == 0;

    private void Awake()
    {
        m_refWeapon = GetComponent<Weapon>();
        if (m_refFullChargeEffect == null)
            CreateDefaultFullChargeEffect();
        OnValidate();
        ClearCharge();
    }

    private void CreateDefaultFullChargeEffect()
    {
        ParticleSystem refSource = GetComponentInChildren<ParticleSystem>();
        if (refSource == null)
            return;

        m_refFullChargeEffect = Instantiate(refSource, transform);
        m_refFullChargeEffect.name = "ChargeFullEffect";
        ParticleSystem.MainModule tMain = m_refFullChargeEffect.main;
        tMain.loop = true;
        m_refFullChargeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        m_bOwnsFullChargeEffect = true;
    }

    private void OnDestroy()
    {
        if (m_bOwnsFullChargeEffect && m_refFullChargeEffect != null)
            Destroy(m_refFullChargeEffect.gameObject);
    }

    private void OnValidate()
    {
        m_fMaxChargeTime = Mathf.Max(0.01f, m_fMaxChargeTime);
        m_fMaxVisualScale = Mathf.Max(1f, m_fMaxVisualScale);
        m_fMaxSpeedMultiplier = Mathf.Max(1f, m_fMaxSpeedMultiplier);
    }

    private void Update()
    {
        if (m_refBulletObj == null)
            return;
        if (!OwnsBullet)
        {
            ClearCharge();
            return;
        }
        if (Time.timeScale <= 0f || !m_refWeapon.isActiveAndEnabled)
        {
            Cancel();
            return;
        }

        m_fChargeTimer = Mathf.Min(m_fChargeTimer + Time.deltaTime, m_fMaxChargeTime);
        float fRatio = m_fChargeTimer / m_fMaxChargeTime;
        m_refBulletTr.localScale = m_vBaseScale * Mathf.Lerp(1f, m_fMaxVisualScale, fRatio);

        if (m_bFullCharge || fRatio < 1f)
            return;
        m_bFullCharge = true;
        if (m_refFullChargeEffect != null)
            m_refFullChargeEffect.Play(true);
    }

    private void OnDisable() => Cancel();

    public void Begin()
    {
        if (!isActiveAndEnabled || !m_refWeapon.isActiveAndEnabled || Time.timeScale <= 0f)
            return;
        if (OwnsBullet)
            return;

        ClearCharge();
        m_refBulletObj = m_refWeapon.Begin();
        if (m_refBulletObj == null)
            return;

        m_refBulletTr = m_refBulletObj.transform;
        m_refCircleCollider = m_refBulletObj.GetComponent<CircleCollider>();
        m_refPoolObj = m_refBulletObj.GetComponent<PoolObject>();
        if (m_refCircleCollider == null || m_refPoolObj == null)
        {
            if (m_refPoolObj != null && ObjectPoolManager.m_Instance != null)
                ObjectPoolManager.m_Instance.PushObject(m_refBulletObj);
            ClearCharge();
            return;
        }

        m_iGeneration = m_refPoolObj.Generation;
        m_refPoolParent = m_refBulletTr.parent;
        m_vBaseScale = m_refBulletTr.localScale;
        m_fBaseRadius = m_refCircleCollider.Radius;

        m_refBulletTr.SetParent(m_refWeapon.FireTransform, false);
        m_refBulletTr.localPosition = Vector3.zero;
        m_refBulletTr.localRotation = Quaternion.identity;
    }

    public void Release(Vector3 _vTargetPos, Transform _refTarget)
    {
        if (!OwnsBullet)
        {
            ClearCharge();
            return;
        }
        if (!isActiveAndEnabled || !m_refWeapon.isActiveAndEnabled || Time.timeScale <= 0f)
        {
            Cancel();
            return;
        }

        float fRatio = m_fChargeTimer / m_fMaxChargeTime;
        float fScale = Mathf.Lerp(1f, m_fMaxVisualScale, fRatio);
        float fSpeedMultiplier = Mathf.Lerp(1f, m_fMaxSpeedMultiplier, fRatio);
        m_refBulletTr.SetParent(m_refPoolParent, true);
        m_refCircleCollider.SetRadius(m_fBaseRadius * fScale);
        StopFullChargeEffect();
        m_refWeapon.Fire(m_refBulletObj, _vTargetPos, _refTarget, fSpeedMultiplier);
        ClearCharge(false);
    }

    public void Cancel()
    {
        if (OwnsBullet && ObjectPoolManager.m_Instance != null)
            ObjectPoolManager.m_Instance.PushObject(m_refBulletObj);
        ClearCharge();
    }

    private void ClearCharge(bool _bStopEffect = true)
    {
        m_refBulletObj = null;
        m_refBulletTr = null;
        m_refPoolParent = null;
        m_refCircleCollider = null;
        m_refPoolObj = null;
        m_fChargeTimer = 0f;
        m_bFullCharge = false;
        if (_bStopEffect)
            StopFullChargeEffect();
    }

    private void StopFullChargeEffect()
    {
        if (m_refFullChargeEffect != null)
            m_refFullChargeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
