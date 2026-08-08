using UnityEditor;
using UnityEngine;

/*///////////////////////////////////////////
                tSlotInfoDrawer
기능 : Interface.tSlotInfo의 iSubType을, eType이 eDataType.Equip일 때만
      eEquipType 드롭다운으로 그려줌 (그 외 DataType은 아직 서브타입 enum이 없어 int 그대로 노출)
 *///////////////////////////////////////////

[CustomPropertyDrawer(typeof(Interface.SlotInfo))]
public class tSlotInfoDrawer : PropertyDrawer
{
    public override void OnGUI(Rect _rPosition, SerializedProperty _pProperty, GUIContent _pLabel)
    {
        EditorGUI.BeginProperty(_rPosition, _pLabel, _pProperty);

        SerializedProperty pSlotSize = _pProperty.FindPropertyRelative("vSlotSize");
        SerializedProperty pPosition = _pProperty.FindPropertyRelative("vPosition");
        SerializedProperty pType = _pProperty.FindPropertyRelative("eType");
        SerializedProperty pSubType = _pProperty.FindPropertyRelative("iSubType");
        SerializedProperty pSlotView = _pProperty.FindPropertyRelative("refSlotView");

        Rect rLine = new Rect(_rPosition.x, _rPosition.y, _rPosition.width, EditorGUIUtility.singleLineHeight);
        float fLineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        EditorGUI.PropertyField(rLine, pSlotSize);
        rLine.y += fLineHeight;

        EditorGUI.PropertyField(rLine, pPosition);
        rLine.y += fLineHeight;

        EditorGUI.PropertyField(rLine, pType);
        rLine.y += fLineHeight;

        eDataType eType = (eDataType)pType.enumValueIndex;
        if (eType == eDataType.Equip)
        {
            eEquipType eEquip = (eEquipType)pSubType.intValue;
            EditorGUI.BeginChangeCheck();
            eEquip = (eEquipType)EditorGUI.EnumPopup(rLine, "iSubType", eEquip);
            if (EditorGUI.EndChangeCheck())
                pSubType.intValue = (int)eEquip;
        }
        else
        {
            EditorGUI.PropertyField(rLine, pSubType);
        }
        rLine.y += fLineHeight;

        EditorGUI.PropertyField(rLine, pSlotView);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty _pProperty, GUIContent _pLabel)
    {
        float fLineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        return fLineHeight * 5f - EditorGUIUtility.standardVerticalSpacing;
    }
}
