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

    [SerializeField] private Image m_refAimImage;
    private Vector3 m_tTargetPosition = Vector3.zero;
    public Vector3 TargetPosition => m_tTargetPosition;
    
    private void Update()
    {
        m_tTargetPosition = RayCast();
    }

    public Vector3 RayCast()
    {
        Ray tRay = Camera.main.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));

        RaycastHit tHit;

        if (Physics.Raycast(tRay, out tHit, m_fMaxLength, m_tLayerMask))
        {
            ChangeCollor(true);
            return tHit.point;
        }

        ChangeCollor(false);
        return tRay.origin + tRay.direction * m_fMaxLength;
    }



    private void ChangeCollor(bool hit)
    {
        if (m_refAimImage == null)
            return;

        m_refAimImage.color = hit ? Color.red : Color.white;
    }
}
