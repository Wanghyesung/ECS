using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



/*///////////////////////////////////////////
                   Aim
기능 : ray, claude플레이어가 쏠 방향을 제공하는 클래스
 *///////////////////////////////////////////

public class Aim : MonoBehaviour
{
    [SerializeField] private LayerMask m_tLayerMask;
    [SerializeField] private float m_fMaxLength;
    [SerializeField] private Transform m_refPlayer;

    [SerializeField] private Image m_refAimImage;
    private Vector3 m_tTargetPosition = Vector3.zero;
    public Vector3 TargetPosition => m_tTargetPosition;

    private void Update()
    {
        m_tTargetPosition = RayCast();
    }

    // 쿼터뷰 전환: 화면 중앙이 아니라 마우스 커서 위치를 기준으로, 플레이어와 같은 높이(Y 고정)의
    // 평면과 마우스 레이가 만나는 지점을 조준점으로 사용 (XZ 평면 조준)
    public Vector3 RayCast()
    {
        Vector2 vScreenPos = InputManager.m_Instance.InputInfo.ScreenPos;

        // 조준점 UI(리티클)를 마우스 커서 위치로 이동. Canvas가 Screen Space - Overlay라
        // 스크린 좌표를 그대로 넣으면 됨
        if (m_refAimImage != null)
            m_refAimImage.rectTransform.position = vScreenPos;

        Ray tRay = Camera.main.ScreenPointToRay(vScreenPos);

        Plane tGroundPlane = new Plane(Vector3.up, new Vector3(0f, m_refPlayer.position.y, 0f));

        Vector3 vTargetPos = Vector3.zero;

        if (tGroundPlane.Raycast(tRay, out float fEnter))
        {
            vTargetPos = tRay.GetPoint(fEnter);

            bool bHitTarget = Physics.Raycast(tRay, out RaycastHit tHit, m_fMaxLength, m_tLayerMask);
            ChangeCollor(bHitTarget);
        }
        else
        {
            vTargetPos = tRay.origin + tRay.direction * m_fMaxLength;
            ChangeCollor(false);
        }
       
        vTargetPos.y = 0.0f;
        return vTargetPos;
    }



    private void ChangeCollor(bool hit)
    {
        if (m_refAimImage == null)
            return;

        m_refAimImage.color = hit ? Color.red : Color.white;
    }
}
