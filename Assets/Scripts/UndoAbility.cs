using TMPro;
using UnityEngine;

public class UndoAbility : MonoBehaviour
{
    //되돌리기 횟수
    [SerializeField] private int undoCount = 3;
    [SerializeField] private TextMeshProUGUI undoCountText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SetUndoCountText();
    }

    public void ResetUndoCount()
    {
        undoCount = 3;
        SetUndoCountText();
    }

    public void SetUndoCountText()
    {
        if (undoCountText != null)
        {
            undoCountText.text = undoCount.ToString();
        }
    }

    public void OnClickUndoButton()
    {
        if (BoardHistoryManager.Instance.HasPreviousState && undoCount > 0)
        {
            undoCount--;
            SetUndoCountText();
            BoardHistoryManager.Instance.RestorePreviousState();
        }
        else if (undoCount <= 0)
        {
            Debug.Log("Undo limit reached!");
        }
        else
        {
            Debug.Log("No previous state to undo!");
        }
    }
}