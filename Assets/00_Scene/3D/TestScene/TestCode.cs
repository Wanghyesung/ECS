using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestCode : MonoBehaviour
{
    [SerializeField] private AnimationTable Target;
    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            Target.PlayAimation(eEntityState.Success);
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            Target.PlayAimation(eEntityState.Fail);
        }
    }
}
