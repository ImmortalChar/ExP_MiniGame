using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스크립트명: RotateAbility
/// 기능:
/// - 보드 중심(4x4 기준)을 둘러싼 드래그 스와이프 입력을 감지하여 시계(CW) 또는 반시계(CCW) 회전 방향을 판정합니다.
/// - 외적(Cross Product)의 z값 및 접선(Tangent) 각도 임계값을 통해 확실한 회전 방향만 인식하며, 방사형이나 애매한 드래그는 무시합니다.
/// - 회전 방향이 결정되면 중앙 2x2 타일은 그대로 고정한 채, 외곽 12개 타일만 중심을 기준으로 90도 회전(대칭 이동)시킵니다.
/// - 다른 어빌리티와 마찬가지로 버튼 토글 시 하이라이트 부여, 일반 타일 이동 및 타 어빌리티 실행 금지, 재클릭 시 취소를 지원합니다.
/// </summary>
public class RotateAbility : MonoBehaviour
{
    [Header("회전 횟수 관리")]
    [Tooltip("로테이트 어빌리티의 최대 사용 가능 횟수")]
    [SerializeField] private int maxRotateCount = 3;

    [Tooltip("현재 남은 로테이트 사용 횟수 (기본값: 3)")]
    [SerializeField] private int rotateCount = 3;

    [Header("하이라이트 효과 설정")]
    [Tooltip("로테이트 발동 시 타일이 확대되는 하이라이트 배율")]
    [SerializeField] private float highlightScaleMultiplier = 1.25f;

    [Tooltip("로테이트 발동 하이라이트 연출 지속 시간 (초)")]
    [SerializeField] private float highlightDuration = 0.2f;

    [Header("UI 컴포넌트")]
    [Tooltip("남은 로테이트 횟수를 표시할 TextMeshPro UI (선택 사항)")]
    [SerializeField] private TextMeshProUGUI rotateCountText;

    [Tooltip("Rotate 버튼 컴포넌트 (비어있으면 현재 오브젝트에서 자동 탐색)")]
    [SerializeField] private Button rotateButton;

    [Header("버튼 하이라이트 UI 설정")]
    [Tooltip("어빌리티 활성화 시 버튼에 적용할 강조 색상 (기본값: 밝은 황금빛)")]
    [SerializeField] private Color buttonActiveColor = new Color(1f, 0.88f, 0.35f, 1f);

    [Tooltip("어빌리티 활성화 시 버튼 확대 배율 (기본값: 1.12배)")]
    [SerializeField] private float buttonActiveScale = 1.12f;

    [Tooltip("어빌리티 활성화 시 버튼이 부드럽게 숨쉬듯 펄스 애니메이션을 재생할지 여부")]
    [SerializeField] private bool usePulseAnimation = true;

    [Tooltip("어빌리티 활성화 시 외곽선(Outline) 강조를 표시할지 여부")]
    [SerializeField] private bool useOutlineHighlight = true;

    [Header("보드 참조")]
    [Tooltip("TileBoard 컴포넌트 참조 (비어있으면 씬에서 자동 탐색)")]
    [SerializeField] private TileBoard board;

    [Header("스와이프 설정")]
    [Tooltip("회전으로 인정할 최소 드래그 거리 (픽셀 단위, TileBoard의 minSwipeDistance와 동일한 역할)")]
    [SerializeField] private float minSwipeDistance = 50f;

    [Tooltip("회전 접선(Tangent) 방향 기준 허용 각도 (예: 35도 설정 시 접선 기준 ±35도 범위만 유효, 애매한 방향은 무시)")]
    [Range(10f, 45f)]
    [SerializeField] private float swipeAngleThreshold = 35f;

    [Tooltip("보드 중심부 터치 시 회전축 오차를 방지하기 위한 최소 중심 거리 (픽셀 단위)")]
    [SerializeField] private float minCenterRadius = 25f;

    public bool IsRotateMode => isRotateMode;
    private bool isRotateMode = false;
    private bool isRotating = false;

    // 드래그(스와이프) 감지용 변수
    private Vector2 touchStartPosition;
    private bool isSwiping = false;

    // 버튼 하이라이트 애니메이션 관련 변수
    private Vector3 originalButtonScale = Vector3.one;
    private Color originalButtonColor = Color.white;
    private ColorBlock originalColorBlock;
    private Image buttonImage;
    private Outline buttonOutline;
    private Coroutine buttonPulseCoroutine;

    private void Awake()
    {
        // 씬 내의 TileBoard 자동 탐색
        if (board == null)
        {
            board = FindFirstObjectByType<TileBoard>();
        }

        // 현재 게임오브젝트에 Button이 붙어있다면 자동 연결
        if (rotateButton == null)
        {
            rotateButton = GetComponent<Button>();
        }

        InitButtonHighlight();
    }

    private void Start()
    {
        // 남은 횟수 UI 텍스트 및 버튼 인터랙션 초기화
        UpdateRotateCountUI();
        UpdateRotateButtonState();
    }

    private void Update()
    {
        if (!isRotateMode || isRotating) return;

        // PC 에디터 편의용 키보드 입력 지원 (Q: 반시계, E: 시계, Escape: 취소)
        HandleKeyboardTestInput();

        // 모바일 터치 및 마우스 드래그(스와이프) 회전 입력 처리
        HandleSwipeInput();
    }

    /// <summary>
    /// PC 에디터 테스트 편의를 위해 Q / E 키로도 회전을 테스트할 수 있도록 지원합니다.
    /// </summary>
    private void HandleKeyboardTestInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelRotateMode();
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            ExecuteRotate(true); // 시계 방향
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            ExecuteRotate(false); // 반시계 방향
        }
    }

    /// <summary>
    /// 모바일 터치 및 에디터 마우스 드래그를 감지하여 보드 중심 기준 시계/반시계 회전을 판정합니다.
    /// TileBoard.cs의 HandleSwipeInput 구조를 기반으로 회전 외적 및 접선 각도 검사를 적용했습니다.
    /// </summary>
    private void HandleSwipeInput()
    {
        // 1. 터치 시작 (클릭 시작)
        if (Input.GetMouseButtonDown(0))
        {
            // Rotate 버튼 자체를 다시 클릭하여 취소하려는 경우, 스와이프를 시작하지 않음
            if (rotateButton != null)
            {
                RectTransform btnRect = rotateButton.GetComponent<RectTransform>();
                if (btnRect != null && RectTransformUtility.RectangleContainsScreenPoint(btnRect, Input.mousePosition))
                {
                    return;
                }
            }

            touchStartPosition = Input.mousePosition;
            isSwiping = true;
        }

        // 2. 드래그 중 판정
        if (Input.GetMouseButton(0) && isSwiping)
        {
            Vector2 currentPosition = (Vector2)Input.mousePosition;
            Vector2 delta = currentPosition - touchStartPosition;

            // 최소 거리 이상 드래그했을 때만 회전 검사
            if (delta.magnitude >= minSwipeDistance)
            {
                Vector2 center = GetBoardCenterScreenPosition();
                Vector2 r = touchStartPosition - center; // 중심에서 드래그 시작 위치로 가는 벡터

                // 중심과 너무 가까운 곳에서 터치한 경우 회전 기준이 불안정하므로 무시
                if (r.magnitude >= minCenterRadius)
                {
                    // [핵심 1] 2D 화면 좌표계(X: 우측 +, Y: 상단 +) 기준 외적 z 성분 계산:
                    // r x delta = r.x * delta.y - r.y * delta.x
                    // crossZ < 0: 시계 방향 회전 (Clockwise)
                    // crossZ > 0: 반시계 방향 회전 (Counter-Clockwise)
                    float crossZ = (r.x * delta.y) - (r.y * delta.x);

                    // [핵심 2] 해당 위치 r에서의 이상적인 시계 / 반시계 접선(Tangent) 벡터 계산
                    Vector2 idealCW = new Vector2(r.y, -r.x);    // 시계 방향 이상 접선
                    Vector2 idealCCW = new Vector2(-r.y, r.x);  // 반시계 방향 이상 접선

                    float angleCW = Vector2.Angle(delta, idealCW);
                    float angleCCW = Vector2.Angle(delta, idealCCW);

                    // 시계 방향 검사 (접선과의 각도가 threshold 이내이고 crossZ 음수)
                    if (angleCW <= swipeAngleThreshold && crossZ < 0f)
                    {
                        isSwiping = false; // 이번 터치 종료
                        ExecuteRotate(true);
                    }
                    // 반시계 방향 검사 (접선과의 각도가 threshold 이내이고 crossZ 양수)
                    else if (angleCCW <= swipeAngleThreshold && crossZ > 0f)
                    {
                        isSwiping = false; // 이번 터치 종료
                        ExecuteRotate(false);
                    }
                    // [핵심 3: TileBoard와 동일한 처리] 어느 회전 방향에도 해당하지 않는 애매한 각도(예: 중심을 향하거나 밖으로 뻗는 방사형 드래그 등)라면?
                    // 아무것도 실행하지 않고 isSwiping을 유지합니다.
                    // 유저가 손가락을 더 확실한 회전 방향으로 꺾으면 그때 발동되며, 이 상태 그대로 손을 떼면 무시됩니다.
                }
            }
        }

        // 3. 터치 종료
        if (Input.GetMouseButtonUp(0))
        {
            isSwiping = false;
        }
    }

    /// <summary>
    /// 보드(4x4 그리드)의 화면상 중심 좌표(픽셀)를 반환합니다.
    /// </summary>
    private Vector2 GetBoardCenterScreenPosition()
    {
        if (board == null)
        {
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        Canvas canvas = board.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        if (board.Grid != null && board.Grid.cells != null && board.Grid.cells.Length == 16)
        {
            TileCell c0 = board.Grid.GetCell(0, 0);
            TileCell c3 = board.Grid.GetCell(3, 3);
            if (c0 != null && c3 != null)
            {
                Vector3 centerWorld = (c0.transform.position + c3.transform.position) * 0.5f;
                return RectTransformUtility.WorldToScreenPoint(cam, centerWorld);
            }
        }

        return RectTransformUtility.WorldToScreenPoint(cam, board.transform.position);
    }

    /// <summary>
    /// 메인 메서드: Rotate 버튼을 클릭했을 때 호출되는 함수입니다.
    /// Canvas > Abilities > Rotate 버튼의 OnClick 이벤트에 이 함수를 할당하세요.
    /// </summary>
    public void OnClickRotateButton()
    {
        // 1. 남은 사용 횟수 검사
        if (rotateCount <= 0)
        {
            Debug.Log("[RotateAbility] 로테이트 능력을 모두 사용했습니다! (최대 3회)");
            return;
        }

        // 2. 이미 로테이트 모드 진행 중인 상태에서 다시 버튼을 누른 경우 -> 취소 토글
        if (isRotateMode)
        {
            CancelRotateMode();
            Debug.Log("[RotateAbility] 로테이트 모드가 취소되었습니다.");
            return;
        }

        // 3. 충돌 방지를 위해 다른 모든 어빌리티 모드 취소
        CancelOtherAbilities();

        // 4. 로테이트 모드 활성화
        isRotateMode = true;
        isSwiping = false;
        SetButtonHighlight(true);

        // 드래그 회전 조작 도중 일반 보드 슬라이드가 일어나지 않도록 보드 입력 잠시 비활성화
        if (board != null)
        {
            board.enabled = false;
        }

        Debug.Log("[RotateAbility] 로테이트 모드 시작! 보드를 시계 또는 반시계 방향으로 드래그하세요. (다시 누르면 취소)");
    }

    public void UseRotateAbility()
    {
        OnClickRotateButton();
    }

    /// <summary>
    /// 로테이트 모드를 취소하고 원래 상태로 되돌립니다.
    /// </summary>
    public void CancelRotateMode()
    {
        isRotateMode = false;
        isSwiping = false;
        SetButtonHighlight(false);

        // 보드 조작 다시 활성화
        if (board != null)
        {
            board.enabled = true;
        }
    }

    /// <summary>
    /// 다른 활성화된 어빌리티 모드가 있다면 모두 취소합니다.
    /// </summary>
    private void CancelOtherAbilities()
    {
        TileSwapAbility swap = FindFirstObjectByType<TileSwapAbility>();
        if (swap != null && swap.IsSwapMode)
        {
            swap.CancelSwapMode();
        }

        TileTpAbility tp = FindFirstObjectByType<TileTpAbility>();
        if (tp != null && tp.IsTpMode)
        {
            tp.CancelTpMode();
        }

        BombAbility bomb = FindFirstObjectByType<BombAbility>();
        if (bomb != null && bomb.IsBombMode)
        {
            bomb.CancelBombMode();
        }

        EraseAbility erase = FindFirstObjectByType<EraseAbility>();
        if (erase != null && erase.IsEraseMode)
        {
            erase.CancelEraseMode();
        }
    }

    /// <summary>
    /// 회전 실행 코루틴을 시작합니다.
    /// </summary>
    /// <param name="clockwise">true: 시계 방향, false: 반시계 방향</param>
    private void ExecuteRotate(bool clockwise)
    {
        if (isRotating) return;
        StartCoroutine(ExecuteRotateRoutine(clockwise));
    }

    /// <summary>
    /// 4x4 그리드의 외곽 12개 셀을 시계 방향 순서로 가져옵니다.
    /// 중앙 2x2 셀 (1,1), (2,1), (1,2), (2,2)는 완전히 제외됩니다.
    /// </summary>
    private TileCell[] GetOuterRingCells()
    {
        if (board == null || board.Grid == null) return null;

        TileGrid grid = board.Grid;
        if (grid.Width != 4 || grid.Height != 4)
        {
            Debug.LogWarning("[RotateAbility] 4x4 그리드가 아니어서 회전을 수행할 수 없습니다.");
            return null;
        }

        // 시계 방향 순서로 외곽 12개 셀 매핑:
        // (0,0) (1,0) (2,0) (3,0)
        // (0,1)             (3,1)
        // (0,2)             (3,2)
        // (0,3) (1,3) (2,3) (3,3)
        return new TileCell[12]
        {
            grid.GetCell(0, 0), // 0: 좌상단 코너
            grid.GetCell(1, 0), // 1: 상단 변1
            grid.GetCell(2, 0), // 2: 상단 변2
            grid.GetCell(3, 0), // 3: 우상단 코너
            grid.GetCell(3, 1), // 4: 우측 변1
            grid.GetCell(3, 2), // 5: 우측 변2
            grid.GetCell(3, 3), // 6: 우하단 코너
            grid.GetCell(2, 3), // 7: 하단 변2
            grid.GetCell(1, 3), // 8: 하단 변1
            grid.GetCell(0, 3), // 9: 좌하단 코너
            grid.GetCell(0, 2), // 10: 좌측 변2
            grid.GetCell(0, 1)  // 11: 좌측 변1
        };
    }

    /// <summary>
    /// 외곽 12개 타일을 시계/반시계 방향으로 90도 회전(대칭 이동)시키는 코루틴입니다.
    /// 중앙 2x2 타일은 원래 자리를 유지합니다.
    /// </summary>
    private IEnumerator ExecuteRotateRoutine(bool clockwise)
    {
        TileCell[] ringCells = GetOuterRingCells();
        if (ringCells == null)
        {
            CancelRotateMode();
            yield break;
        }

        // 1. 현재 외곽 12개 칸에 배치된 타일들 수집
        Tile[] currentTiles = new Tile[12];
        bool hasAnyOuterTile = false;
        for (int i = 0; i < 12; i++)
        {
            currentTiles[i] = ringCells[i].tile;
            if (currentTiles[i] != null)
            {
                hasAnyOuterTile = true;
            }
        }

        // 외곽에 타일이 단 하나도 없다면 회전을 수행하지 않음
        if (!hasAnyOuterTile)
        {
            Debug.Log("[RotateAbility] 외곽 12개 칸에 회전할 타일이 없습니다.");
            CancelRotateMode();
            yield break;
        }

        // 중복 실행 및 추가 조작 방지
        isRotating = true;
        isRotateMode = false;
        isSwiping = false;

        // 2. 직전 상태 저장 (되돌리기 Undo 기능과의 연동)
        if (BoardHistoryManager.Instance != null)
        {
            BoardHistoryManager.Instance.SaveCurrentState();
        }

        // 3. 외부 12개 셀의 기존 타일 연결을 모두 임시 해제
        for (int i = 0; i < 12; i++)
        {
            ringCells[i].tile = null;
        }

        // 4. 각 타일을 목표 셀로 재할당하고 부드러운 이동 애니메이션 시작
        // 12개 셀의 원형 링에서 90도 회전은 정확히 3칸 시프트에 해당합니다.
        // - 시계 방향(CW): target = (i + 3) % 12
        // - 반시계 방향(CCW): target = (i + 9) % 12  ((i - 3 + 12) % 12)
        List<Tile> movedTiles = new List<Tile>();
        for (int i = 0; i < 12; i++)
        {
            Tile tile = currentTiles[i];
            if (tile != null)
            {
                int targetIndex = clockwise ? (i + 3) % 12 : (i + 9) % 12;
                TileCell targetCell = ringCells[targetIndex];

                tile.MoveToForSwap(targetCell);
                movedTiles.Add(tile);
            }
        }

        // 타일 이동 애니메이션 대기 (Tile.cs의 이동 시간은 0.15초)
        yield return new WaitForSeconds(0.15f);

        // 5. 회전 완료된 타일들에 팝 하이라이트 연출 적용
        foreach (Tile tile in movedTiles)
        {
            if (tile != null)
            {
                tile.PlayHighlightEffect(highlightScaleMultiplier, highlightDuration);
            }
        }

        if (highlightDuration > 0f)
        {
            yield return new WaitForSeconds(highlightDuration);
        }

        // 6. 사용 횟수 1 차감 및 UI 업데이트
        rotateCount--;
        UpdateRotateCountUI();
        UpdateRotateButtonState();

        Debug.Log($"[RotateAbility] 외곽 타일 회전 완료! (방향: {(clockwise ? "시계" : "반시계")}) 남은 횟수: {rotateCount}/{maxRotateCount}");

        // 버튼 하이라이트 해제 및 보드 조작 재활성화
        SetButtonHighlight(false);
        isRotating = false;

        if (board != null)
        {
            board.enabled = true;
        }

        // 7. 회전 후 게임 오버 여부 판정
        yield return new WaitForSeconds(0.15f);
        if (board != null && board.CheckForGameOver())
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.GameOver();
            }
        }
    }

    /// <summary>
    /// 남은 로테이트 사용 횟수 UI 텍스트를 갱신합니다.
    /// </summary>
    public void UpdateRotateCountUI()
    {
        if (rotateCountText != null)
        {
            rotateCountText.text = rotateCount.ToString();
        }
    }

    /// <summary>
    /// 횟수가 모두 소진되었을 때 버튼 상호작용을 비활성화합니다.
    /// </summary>
    public void UpdateRotateButtonState()
    {
        if (rotateButton != null)
        {
            rotateButton.interactable = rotateCount > 0;
        }
    }

    /// <summary>
    /// 로테이트 사용 횟수를 최대치(3회)로 초기화합니다. (새 게임 시작 시 호출)
    /// </summary>
    public void ResetRotateCount()
    {
        CancelRotateMode();
        rotateCount = maxRotateCount;
        UpdateRotateCountUI();
        UpdateRotateButtonState();
    }

    /// <summary>
    /// 버튼 하이라이트 관련 원본 값 초기화 및 아웃라인 설정을 수행합니다.
    /// </summary>
    private void InitButtonHighlight()
    {
        if (rotateButton != null)
        {
            originalButtonScale = rotateButton.transform.localScale;
            originalColorBlock = rotateButton.colors;

            buttonImage = rotateButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                originalButtonColor = buttonImage.color;
            }

            if (useOutlineHighlight)
            {
                buttonOutline = rotateButton.GetComponent<Outline>();
                if (buttonOutline == null)
                {
                    buttonOutline = rotateButton.gameObject.AddComponent<Outline>();
                }
                buttonOutline.effectColor = new Color(1f, 0.9f, 0.2f, 0.9f);
                buttonOutline.effectDistance = new Vector2(3f, -3f);
                buttonOutline.enabled = false;
            }
        }
    }

    /// <summary>
    /// 로테이트 버튼의 활성화/비활성화 하이라이트 상태를 적용합니다.
    /// </summary>
    private void SetButtonHighlight(bool isActive)
    {
        if (rotateButton == null) return;

        if (isActive)
        {
            if (buttonPulseCoroutine != null)
            {
                StopCoroutine(buttonPulseCoroutine);
            }

            if (usePulseAnimation)
            {
                buttonPulseCoroutine = StartCoroutine(ButtonPulseRoutine());
            }
            else
            {
                rotateButton.transform.localScale = originalButtonScale * buttonActiveScale;
            }

            if (buttonImage != null)
            {
                buttonImage.color = buttonActiveColor;
            }

            ColorBlock cb = rotateButton.colors;
            cb.normalColor = buttonActiveColor;
            cb.highlightedColor = buttonActiveColor;
            cb.selectedColor = buttonActiveColor;
            rotateButton.colors = cb;

            if (buttonOutline != null && useOutlineHighlight)
            {
                buttonOutline.enabled = true;
            }
        }
        else
        {
            if (buttonPulseCoroutine != null)
            {
                StopCoroutine(buttonPulseCoroutine);
                buttonPulseCoroutine = null;
            }

            rotateButton.transform.localScale = originalButtonScale;

            if (buttonImage != null)
            {
                buttonImage.color = originalButtonColor;
            }

            rotateButton.colors = originalColorBlock;

            if (buttonOutline != null)
            {
                buttonOutline.enabled = false;
            }
        }
    }

    /// <summary>
    /// 어빌리티 활성화 중 버튼이 부드럽게 숨쉬듯 커졌다 작아지는 펄스 연출입니다.
    /// </summary>
    private IEnumerator ButtonPulseRoutine()
    {
        float timer = 0f;
        float pulseSpeed = 4f;

        while (true)
        {
            timer += Time.deltaTime * pulseSpeed;
            float scaleFactor = Mathf.Lerp(1f, buttonActiveScale, (Mathf.Sin(timer) + 1f) * 0.5f);
            if (rotateButton != null)
            {
                rotateButton.transform.localScale = originalButtonScale * scaleFactor;
            }
            yield return null;
        }
    }
}
