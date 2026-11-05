using UnityEditor;
using UnityEngine;

public class ClearPrefs
{
    [MenuItem("Tools/Clear All PlayerPrefs")]
    public static void ClearAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("PlayerPrefs가 성공적으로 초기화되었습니다.");
    }
}