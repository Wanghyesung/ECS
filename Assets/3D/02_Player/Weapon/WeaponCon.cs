using System;
using R3;
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
    [SerializeField] private ParticleSystem m_refChargeEffect;

    [SerializeField] private Weapon m_refWeapon;
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
    private IDisposable m_disposablePoolPush;

    private bool OwnsBullet => m_refPoolObj != null &&
        m_refPoolObj.Generation == m_iGeneration && m_refPoolObj.PushCount == 0;

    private void Awake()
    {
        if(m_refWeapon == null)
            m_refWeapon = GetComponent<Weapon>();
        OnValidate();
        ClearCharge();
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
        if (OwnsBullet == false)
        {
            ClearCharge();
            return;
        }
        if (Time.timeScale <= 0f || m_refWeapon.isActiveAndEnabled == false)
        {
            Cancel();
            return;
        }

        m_fChargeTimer = Mathf.Min(m_fChargeTimer + Time.deltaTime, m_fMaxChargeTime);
        float fRatio = m_fChargeTimer / m_fMaxChargeTime;
        m_refBulletTr.localScale = m_vBaseScale * Mathf.Lerp(1f, m_fMaxVisualScale, fRatio);

    }

    private void OnDisable() => Cancel();

    public void SetChargeOnly() => m_refWeapon.SetChargeOnly(true);

    public void Begin()
    {
        if (isActiveAndEnabled == false || m_refWeapon.isActiveAndEnabled == false || Time.timeScale <= 0f)
            return;
        if (OwnsBullet == true)
            return;

        ClearCharge();
        m_refBulletObj = m_refWeapon.Begin();
        if (m_refBulletObj == null)
            return;

        //볼렛의 설정값을 캐싱하고 자식 오브젝트로 설정
        m_refBulletTr = m_refBulletObj.transform;
        m_refCircleCollider = m_refBulletObj.GetComponent<CircleCollider>();
        m_refPoolObj = m_refBulletObj.GetComponent<PoolObject>();
        m_iGeneration = m_refPoolObj.Generation;
        m_refPoolParent = m_refBulletTr.parent;
        m_vBaseScale = m_refBulletTr.localScale;
        m_fBaseRadius = m_refCircleCollider.Radius;
        m_disposablePoolPush = m_refPoolObj.OnPush.Subscribe(_ => ResetBullet());

        m_refBulletTr.SetParent(m_refWeapon.FireTransform, false);
        m_refBulletTr.localPosition = Vector3.zero;
        m_refBulletTr.localRotation = Quaternion.identity;

        if (m_refChargeEffect != null)
            m_refChargeEffect.gameObject.SetActive(true);
    }

    public void Release(Vector3 _vTargetPos, Transform _refTarget)
    {
        if (OwnsBullet == false)
        {
            ClearCharge();
            return;
        }
        if (isActiveAndEnabled == false || m_refWeapon.isActiveAndEnabled == false || Time.timeScale <= 0f)
        {
            Cancel();
            return;
        }
        //마우스를 때는 즉시 크기, 스피드를 잡고 총알을 쏜다
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
        if (OwnsBullet == true)
            ObjectPoolManager.m_Instance.PushObject(m_refBulletObj);
        ClearCharge();
        if (m_refChargeEffect != null)
            m_refChargeEffect.gameObject.SetActive(false);
    }

    private void ClearCharge(bool _bStopEffect = true)
    {
        m_disposablePoolPush?.Dispose();
        m_disposablePoolPush = null;
        m_refBulletObj = null;
        m_refBulletTr = null;
        m_refPoolParent = null;
        m_refCircleCollider = null;
        m_refPoolObj = null;
        m_fChargeTimer = 0f;
        m_bFullCharge = false;
        if (_bStopEffect == true)
            StopFullChargeEffect();
    }

    private void ResetBullet()
    {
        if (m_refPoolObj.Generation != m_iGeneration)
            return;

        m_refBulletTr.SetParent(m_refPoolParent, false);
        m_refBulletTr.localScale = m_vBaseScale;
        m_refCircleCollider.SetRadius(m_fBaseRadius);
    }

    private void StopFullChargeEffect()
    {
        if (m_refChargeEffect != null)
            m_refChargeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
