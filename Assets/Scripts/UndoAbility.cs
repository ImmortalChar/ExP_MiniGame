using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UndoAbility : MonoBehaviour
{
    //되돌리기 횟수
    [SerializeField] private int undoCount = 3;

    [Header("UI 컴포넌트")]
    [Tooltip("되돌리기 횟수 텍스트")]
    [SerializeField] private TextMeshProUGUI undoCountText;

    [Tooltip("Undo 버튼 컴포넌트 (비어있으면 현재 오브젝트에서 자동 탐색)")]
    [SerializeField] private Button undoButton;

    [Header("버튼 하이라이트 UI 설정")]
    [Tooltip("되돌리기 실행 시 버튼에 적용할 강조 색상 (기본값: 밝은 황금빛)")]
    [SerializeField] private Color buttonActiveColor = new Color(1f, 0.88f, 0.35f, 1f);

    [Tooltip("되돌리기 실행 시 버튼 확대 배율 (기본값: 1.15배)")]
    [SerializeField] private float buttonActiveScale = 1.15f;

    [Tooltip("되돌리기 실행 시 외곽선(Outline) 강조를 표시할지 여부")]
    [SerializeField] private bool useOutlineHighlight = true;

    [Header("보드 참조")]
    [Tooltip("TileBoard 컴포넌트 참조 (비어있으면 씬에서 자동 탐색)")]
    [SerializeField] private TileBoard board;

    [Header("하이라이트 효과 설정")]
    [Tooltip("되돌리기 발동 시 복원된 타일들이 확대되는 하이라이트 배율")]
    [SerializeField] private float highlightScaleMultiplier = 1.25f;

    [Tooltip("되돌리기 발동 하이라이트 연출 지속 시간 (초)")]
    [SerializeField] private float highlightDuration = 0.2f;

    private Vector3 originalButtonScale = Vector3.one;
    private Color originalButtonColor = Color.white;
    private ColorBlock originalColorBlock;
    private Image buttonImage;
    private Outline buttonOutline;
    private Coroutine buttonPopCoroutine;

    private void Awake()
    {
        if (board == null)
        {
            board = FindFirstObjectByType<TileBoard>();
        }

        if (undoButton == null)
        {
            undoButton = GetComponent<Button>();
        }

        InitButtonHighlight();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SetUndoCountText();
    }

    public void ResetUndoCount()
    {
        undoCount = 3;
        SetUndoCountText();
        UpdateUndoButtonState();
    }

    public void SetUndoCountText()
    {
        if (undoCountText != null)
        {
            undoCountText.text = undoCount.ToString();
        }
    }

    public void UpdateUndoButtonState()
    {
        if (undoButton != null)
        {
            undoButton.interactable = undoCount > 0;
        }
    }

    public void OnClickUndoButton()
    {
        if (board == null)
        {
            board = FindFirstObjectByType<TileBoard>();
        }

        if (BoardHistoryManager.Instance != null && BoardHistoryManager.Instance.HasPreviousState && undoCount > 0)
        {
            undoCount--;
            SetUndoCountText();
            UpdateUndoButtonState();

            // 이전 상태로 복원
            bool restored = BoardHistoryManager.Instance.RestorePreviousState();

            // [핵심 1] 어빌리티 작동 버튼 자체에 하이라이트 팝 연출 적용!
            PlayButtonClickHighlight();

            // [핵심 2] 어빌리티 발동 타일(복원된 타일들) 자체에 일괄 하이라이트 팝 효과 적용!
            if (restored)
            {
                HighlightRestoredTiles();
            }
        }
        else if (undoCount <= 0)
        {
            Debug.Log("[UndoAbility] 되돌리기 사용 횟수를 모두 소진했습니다!");
        }
        else
        {
            Debug.Log("[UndoAbility] 되돌릴 이전 상태가 없습니다!");
        }
    }

    /// <summary>
    /// 버튼 하이라이트 관련 원본 값 초기화 및 아웃라인 설정을 수행합니다.
    /// </summary>
    private void InitButtonHighlight()
    {
        if (undoButton != null)
        {
            originalButtonScale = undoButton.transform.localScale;
            originalColorBlock = undoButton.colors;

            buttonImage = undoButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                originalButtonColor = buttonImage.color;
            }

            if (useOutlineHighlight)
            {
                buttonOutline = undoButton.GetComponent<Outline>();
                if (buttonOutline == null)
                {
                    buttonOutline = undoButton.gameObject.AddComponent<Outline>();
                }
                buttonOutline.effectColor = new Color(1f, 0.9f, 0.2f, 0.9f);
                buttonOutline.effectDistance = new Vector2(3f, -3f);
                buttonOutline.enabled = false;
            }
        }
    }

    /// <summary>
    /// 되돌리기 버튼 클릭 시 버튼 자체에 순간적인 하이라이트 팝 피드백을 재생합니다.
    /// </summary>
    public void PlayButtonClickHighlight()
    {
        if (undoButton != null && gameObject.activeInHierarchy)
        {
            if (buttonPopCoroutine != null)
            {
                StopCoroutine(buttonPopCoroutine);
            }
            buttonPopCoroutine = StartCoroutine(UndoButtonPopRoutine());
        }
    }

    private IEnumerator UndoButtonPopRoutine()
    {
        if (undoButton == null) yield break;

        // 색상 및 아웃라인 활성화
        if (buttonImage != null) buttonImage.color = buttonActiveColor;
        if (buttonOutline != null) buttonOutline.enabled = true;

        Vector3 targetScale = originalButtonScale * buttonActiveScale;
        float halfDuration = 0.1f;

        // 확대
        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            if (undoButton == null) yield break;
            undoButton.transform.localScale = Vector3.Lerp(originalButtonScale, targetScale, elapsed / halfDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        undoButton.transform.localScale = targetScale;

        // 복구
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            if (undoButton == null) yield break;
            undoButton.transform.localScale = Vector3.Lerp(targetScale, originalButtonScale, elapsed / halfDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        undoButton.transform.localScale = originalButtonScale;

        if (buttonImage != null) buttonImage.color = originalButtonColor;
        if (buttonOutline != null) buttonOutline.enabled = false;
        buttonPopCoroutine = null;
    }

    /// <summary>
    /// 되돌리기로 복원된 모든 타일들에게 하이라이트 팝 효과를 적용합니다.
    /// </summary>
    private void HighlightRestoredTiles()
    {
        if (board == null || board.Grid == null || board.Grid.cells == null) return;

        foreach (TileCell cell in board.Grid.cells)
        {
            if (cell != null && cell.Occupied && cell.tile != null)
            {
                cell.tile.PlayHighlightEffect(highlightScaleMultiplier, highlightDuration);
            }
        }
    }
}