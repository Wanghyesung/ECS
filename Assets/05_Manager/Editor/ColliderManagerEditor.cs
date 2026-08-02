using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/*///////////////////////////////////////////
              ColliderManagerEditor
목적 : ColliderManager의 m_arrLayerCollisionMatrix(LayerMask[32])를
       Unity Project Settings > Physics의 Layer Collision Matrix처럼
       체크박스 그리드로 편집할 수 있게 하는 커스텀 인스펙터.
       32개 LayerMask 드롭다운을 하나하나 여는 대신, 이름 붙은 레이어만 모아
       행(이름)×열(번호) 표로 보여주고 하단에 번호-이름 범례를 둔다.
 *///////////////////////////////////////////

[CustomEditor(typeof(ColliderManager))]
public class ColliderManagerEditor : Editor
{
    private const string MATRIX_FIELD_NAME = "m_arrLayerCollisionMatrix";
    private const float ROW_LABEL_WIDTH = 130f;
    private const float CELL_WIDTH = 24f;
    private const float CELL_HEIGHT = 18f;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 매트릭스 필드만 빼고 나머지 필드는 기본 방식대로 그림
        SerializedProperty propIter = serializedObject.GetIterator();
        bool bEnterChildren = true;
        while (propIter.NextVisible(bEnterChildren))
        {
            bEnterChildren = false;

            if (propIter.name == MATRIX_FIELD_NAME)
                continue;

            EditorGUILayout.PropertyField(propIter, true);
        }

        EditorGUILayout.Space();
        DrawLayerMatrix();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawLayerMatrix()
    {
        SerializedProperty propMatrix = serializedObject.FindProperty(MATRIX_FIELD_NAME);
        if (propMatrix == null || propMatrix.arraySize < 32)
        {
            EditorGUILayout.HelpBox($"{MATRIX_FIELD_NAME}가 32칸짜리 배열이어야 합니다.", MessageType.Warning);
            return;
        }

        // 이름 붙은 레이어만 이미 걸러진 목록 (Tags and Layers 설정값을 그대로 읽어옴)
        string[] arrLayerNames = InternalEditorUtility.layers;
        List<int> listLayer = new List<int>(arrLayerNames.Length);
        for (int i = 0; i < arrLayerNames.Length; ++i)
            listLayer.Add(LayerMask.NameToLayer(arrLayerNames[i]));

        EditorGUILayout.LabelField("Layer Collision Matrix", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("체크하면 두 레이어가 서로 충돌합니다. 한쪽만 체크해도 인식됩니다 (Unity Physics 매트릭스와 동일).", MessageType.None);

        int iCount = listLayer.Count;

        // 열 번호 헤더
        Rect tHeaderRect = GUILayoutUtility.GetRect(ROW_LABEL_WIDTH + iCount * CELL_WIDTH, CELL_HEIGHT);
        for (int c = 0; c < iCount; ++c)
        {
            Rect tCellRect = new Rect(tHeaderRect.x + ROW_LABEL_WIDTH + c * CELL_WIDTH, tHeaderRect.y, CELL_WIDTH, CELL_HEIGHT);
            GUI.Label(tCellRect, (c + 1).ToString(), EditorStyles.centeredGreyMiniLabel);
        }

        // 행(레이어 이름) + 삼각형 체크박스 그리드 (대칭이라 절반만 그려도 충분)
        for (int r = 0; r < iCount; ++r)
        {
            Rect tRowRect = GUILayoutUtility.GetRect(ROW_LABEL_WIDTH + iCount * CELL_WIDTH, CELL_HEIGHT);

            Rect tRowLabelRect = new Rect(tRowRect.x, tRowRect.y, ROW_LABEL_WIDTH, CELL_HEIGHT);
            GUI.Label(tRowLabelRect, $"{r + 1}. {arrLayerNames[r]}");

            for (int c = 0; c <= r; ++c)
            {
                int iLayerA = listLayer[r];
                int iLayerB = listLayer[c];

                Rect tToggleRect = new Rect(
                    tRowRect.x + ROW_LABEL_WIDTH + c * CELL_WIDTH + (CELL_WIDTH - 16f) * 0.5f,
                    tRowRect.y, 16f, CELL_HEIGHT);

                SerializedProperty propBitsA = propMatrix.GetArrayElementAtIndex(iLayerA).FindPropertyRelative("m_Bits");

                bool bChecked = (propBitsA.intValue & (1 << iLayerB)) != 0;
                bool bNewChecked = EditorGUI.Toggle(tToggleRect, bChecked);

                if (bNewChecked != bChecked)
                {
                    if (bNewChecked)
                        propBitsA.intValue |= (1 << iLayerB);
                    else
                        propBitsA.intValue &= ~(1 << iLayerB);
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("열 번호 범례", EditorStyles.miniBoldLabel);
        for (int c = 0; c < iCount; ++c)
            EditorGUILayout.LabelField($"{c + 1} = {arrLayerNames[c]}", EditorStyles.miniLabel);
    }
}
