using UnityEngine;

/*///////////////////////////////////////////
            SOStrafeNode
기능 : 일정 시간마다 방향을 반전하며 횡이동
       타이머/방향은 BlackBoard에 저장 (SO 데이터 오염 방지)
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_StrafeNode", menuName = "Game/Monster/ActionNode/StrafeNode")]
public class SOStrafeNode : SONode
{

    [SerializeField] private bool m_bHorizontaol = false;
    [SerializeField] private float m_fMinTime = 1.0f;
    [SerializeField] private float m_fMaxTime = 3.0f;
   
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.ObjInfo.State != eEntityState.Idle)
            return eNodeState.Failure;

        if (_refBB.StrafeTimer <= 0f)
            UpdateDir(_refBB);

        _refBB.StrafeTimer -= Time.deltaTime;
        _refBB.Owner.transform.position += _refBB.StrafeDir * _refBB.ObjInfo.Speed * Time.deltaTime;

        return eNodeState.Running;
    }

    private void UpdateDir(BlackBoard _refBB)
    {
        if (_refBB.StrafeDir == Vector3.zero)
        {
            if (m_bHorizontaol == true)
                _refBB.StrafeDir = _refBB.Owner.transform.right;
            else
                _refBB.StrafeDir = _refBB.Owner.transform.up;
        }
        else
            _refBB.StrafeDir = -_refBB.StrafeDir;

        _refBB.StrafeTimer = Random.Range(m_fMinTime, m_fMaxTime);
    }
}
