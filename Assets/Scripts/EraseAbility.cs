using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스크립트명: EraseAbility
/// 기능: Scene의 "Erase" 버튼을 누르면 활성화되는 줄 지우기 어빌리티입니다.
/// - 원하는 칸 2개를 순서대로 터치하여 선택합니다 (타일이 있는 칸 및 빈 칸 모두 선택 가능).
/// - 단, 두 번째 칸은 첫 번째 칸과 반드시 같은 가로줄(행) 또는 같은 세로줄(열)에 있어야 선택됩니다.
/// - 두 칸이 유효하게 선택되면, 두 칸을 잇는 라인(가로 또는 세로)의 타일들을 모두 삭제합니다.
/// - 삭제된 타일들의 숫자(값)를 모두 합산하여 플레이어 점수에 가산합니다.
/// - 최대 사용 횟수는 3회로 제한되며 인스펙터에서 관리할 수 있습니다.
/// </summary>
public class EraseAbility : MonoBehaviour
{
    [Header("지우기 횟수 관리")]
    [Tooltip("지우기 어빌리티의 최대 사용 가능 횟수")]
    [SerializeField] private int maxEraseCount = 1;

    [Tooltip("현재 남은 지우기 사용 횟수 (기본값: 3)")]
    [SerializeField] private int eraseCount = 1;

    [Header("지우기 옵션")]
    [Tooltip("true이면 선택한 방향의 해당 라인(행 또는 열) 전체를 삭제합니다.\nfalse이면 선택한 두 타일을 포함하여 그 사이에 있는 타일들만 삭제합니다. (기본값: false)")]
    [SerializeField] private bool clearEntireLine = false;

    [Header("UI 컴포넌트")]
    [Tooltip("남은 지우기 횟수를 표시할 TextMeshPro UI (선택 사항)")]
    [SerializeField] private TextMeshProUGUI eraseCountText;

    [Tooltip("Erase 버튼 컴포넌트 (비어있으면 현재 오브젝트에서 자동 탐색)")]
    [SerializeField] private Button eraseButton;

    [Header("보드 참조")]
    [Tooltip("TileBoard 컴포넌트 참조 (비어있으면 씬에서 자동 탐색)")]
    [SerializeField] private TileBoard board;

    public bool IsEraseMode => isEraseMode;
    private bool isEraseMode = false;

    private TileCell firstSelectedCell = null;
    private Vector3 originalFirstTileScale = Vector3.one;
    private Color originalCellColor = Color.white;
    private Image highlightedCellImage = null;
    private bool isListenersRegistered = false;

    private void Awake()
    {
        // 씬 내의 TileBoard 자동 탐색
        if (board == null)
        {
            board = FindFirstObjectByType<TileBoard>();
        }

        // 현재 게임오브젝트에 Button이 붙어있다면 자동 연결
        if (eraseButton == null)
        {
            eraseButton = GetComponent<Button>();
        }
    }

    private void Start()
    {
        // 1. 남은 횟수 UI 텍스트 갱신
        UpdateEraseCountUI();

        // 2. 각 Cell에 할당된 Button 컴포넌트에 클릭 리스너 자동 등록
        RegisterCellButtonListeners();
    }

    /// <summary>
    /// 그리드의 모든 Cell에 붙어있는 Button 컴포넌트에 OnCellClicked 이벤트를 자동으로 등록합니다.
    /// 또한 Cell의 Image에 Raycast Target을 활성화하여 터치/클릭이 정상 수신되도록 보장합니다.
    /// </summary>
    public void RegisterCellButtonListeners()
    {
        if (isListenersRegistered) return;

        if (board == null)
        {
            board = FindFirstObjectByType<TileBoard>();
        }

        if (board == null || board.Grid == null || board.Grid.cells == null)
        {
            Debug.LogWarning("[EraseAbility] TileBoard 또는 TileGrid를 아직 찾지 못했습니다. 지우기 버튼 클릭 시 재시도합니다.");
            return;
        }

        int registeredCount = 0;
        foreach (TileCell cell in board.Grid.cells)
        {
            if (cell == null) continue;

            Image cellImage = cell.GetComponent<Image>();
            if (cellImage != null)
            {
                cellImage.raycastTarget = true;
            }

            Button cellButton = cell.GetComponent<Button>();
            if (cellButton != null)
            {
                cellButton.interactable = true;
                if (cellButton.targetGraphic == null && cellImage != null)
                {
                    cellButton.targetGraphic = cellImage;
                }

                cellButton.onClick.AddListener(() => OnCellClicked(cell));
                registeredCount++;
            }
            else
            {
                Debug.LogWarning($"[EraseAbility] Cell {cell.name}에 Button 컴포넌트가 없습니다.");
            }
        }

        // 보드 위 타일 오브젝트들이 Cell의 터치를 가로막지 않도록 Raycast Target 해제
        EnsureTilesDoNotBlockRaycast();

        isListenersRegistered = true;
        Debug.Log($"[EraseAbility] 총 {registeredCount}개의 Cell 버튼 등록 및 RaycastTarget 활성화 완료!");
    }

    /// <summary>
    /// 타일(Tile) 프리팹이나 생성된 타일 오브젝트의 Image/Text가 뒤에 있는 Cell의 터치를 가로막지 않도록 보장합니다.
    /// </summary>
    private void EnsureTilesDoNotBlockRaycast()
    {
        if (board == null) return;

        Tile[] tiles = board.GetComponentsInChildren<Tile>();
        foreach (Tile tile in tiles)
        {
            if (tile == null) continue;

            Image img = tile.GetComponent<Image>();
            if (img != null)
            {
                img.raycastTarget = false;
            }

            TextMeshProUGUI txt = tile.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.raycastTarget = false;
            }
        }
    }

    /// <summary>
    /// 메인 메서드: Erase 버튼을 클릭했을 때 호출되는 함수입니다.
    /// Canvas > Abilities > Erase 버튼의 OnClick 이벤트에 이 함수를 할당하세요.
    /// </summary>
    public void OnClickEraseButton()
    {
        // 1. 남은 사용 횟수 검사
        if (eraseCount <= 0)
        {
            Debug.Log("[EraseAbility] 지우기 능력을 모두 사용했습니다! (최대 3회)");
            return;
        }

        // 2. 이미 지우기 모드 진행 중인 상태에서 Erase 버튼을 다시 누른 경우 -> 취소 토글
        if (isEraseMode)
        {
            CancelEraseMode();
            Debug.Log("[EraseAbility] 지우기 모드가 취소되었습니다.");
            return;
        }

        // 다른 모드가 활성화되어 있다면 충돌 방지를 위해 모두 취소
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

        // 리스너가 아직 등록되지 않았었다면 즉시 등록
        if (!isListenersRegistered)
        {
            RegisterCellButtonListeners();
        }

        // 보드 위 타일들이 셀 터치를 방해하지 않도록 Raycast Target 정리
        EnsureTilesDoNotBlockRaycast();

        // 3. 지우기 모드 활성화
        isEraseMode = true;
        firstSelectedCell = null;

        // 타일 선택 도중 보드가 스와이프로 밀리지 않도록 보드 입력 잠시 비활성화
        if (board != null)
        {
            board.enabled = false;
        }

        Debug.Log("[EraseAbility] 지우기 모드 시작! 첫 번째 타일을 터치하세요.");
    }

    public void UseEraseAbility()
    {
        OnClickEraseButton();
    }

    /// <summary>
    /// 개별 Cell(버튼)이 클릭되었을 때 호출되는 함수입니다.
    /// 타일이 존재하는 칸뿐만 아니라 빈 칸도 선택할 수 있습니다.
    /// </summary>
    /// <param name="cell">클릭된 타일 칸</param>
    public void OnCellClicked(TileCell cell)
    {
        Debug.Log($"[EraseAbility] Cell 클릭 감지됨: {cell?.name} (좌표: {cell?.coordinates}) / 지우기모드: {isEraseMode}");

        // 지우기 모드가 아닐 때는 셀 터치 무시
        if (!isEraseMode || cell == null) return;

        // [1] 첫 번째 칸을 선택하는 단계 (빈 칸 및 타일이 있는 칸 모두 선택 가능)
        if (firstSelectedCell == null)
        {
            firstSelectedCell = cell;
            HighlightCell(cell, true);
            string targetInfo = cell.Occupied ? $"값: {cell.tile.state.number}" : "빈 칸";
            Debug.Log($"[EraseAbility] 첫 번째 칸 선택: {cell.coordinates} ({targetInfo}). 같은 가로/세로 줄에 있는 두 번째 칸을 터치하세요. (같은 칸 재터치 시 취소)");
        }
        // [2] 두 번째 칸을 선택하는 단계
        else
        {
            // [2-A] 처음에 누른 칸을 다시 누른 경우 -> 선택 취소
            if (firstSelectedCell == cell)
            {
                HighlightCell(firstSelectedCell, false);
                firstSelectedCell = null;
                Debug.Log("[EraseAbility] 첫 번째 칸 선택이 취소되었습니다. 다시 칸을 선택하세요.");
                return;
            }

            // [2-B] 두 번째 칸이 첫 칸과 같은 가로 라인인지, 세로 라인인지 검사
            bool isSameRow = (firstSelectedCell.coordinates.y == cell.coordinates.y);
            bool isSameCol = (firstSelectedCell.coordinates.x == cell.coordinates.x);

            // 가로줄이나 세로줄 어느 쪽에도 속하지 않는 대각선 위치라면 선택 불가!
            if (!isSameRow && !isSameCol)
            {
                Debug.LogWarning($"[EraseAbility] 두 번째 칸({cell.coordinates})은 첫 번째 칸({firstSelectedCell.coordinates})과 같은 가로줄 또는 세로줄에 위치해야 합니다! (선택되지 않음)");
                return;
            }

            // 조건 충족: 하이라이트 해제 후 라인 삭제 실행!
            HighlightCell(firstSelectedCell, false);
            ExecuteErase(firstSelectedCell, cell);
        }
    }

    /// <summary>
    /// 선택한 두 타일을 잇는 가로 또는 세로 라인의 타일을 모두 삭제하고 점수를 합산합니다.
    /// </summary>
    private void ExecuteErase(TileCell cellA, TileCell cellB)
    {
        if (board == null || board.Grid == null)
        {
            CancelEraseMode();
            return;
        }

        // 1. 직전 상태 저장 (되돌리기 Undo 기능과의 연동)
        if (BoardHistoryManager.Instance != null)
        {
            BoardHistoryManager.Instance.SaveCurrentState();
        }

        bool isSameRow = (cellA.coordinates.y == cellB.coordinates.y);
        List<TileCell> targetCells = new List<TileCell>();

        // 2. 삭제할 셀 목록 수집
        if (isSameRow)
        {
            int y = cellA.coordinates.y;

            if (clearEntireLine)
            {
                // 해당 가로줄(행) 전체 삭제
                for (int x = 0; x < board.Grid.Width; x++)
                {
                    TileCell c = board.Grid.GetCell(x, y);
                    if (c != null) targetCells.Add(c);
                }
            }
            else
            {
                // 두 타일 사이(포함) 구간만 삭제
                int minX = Mathf.Min(cellA.coordinates.x, cellB.coordinates.x);
                int maxX = Mathf.Max(cellA.coordinates.x, cellB.coordinates.x);

                for (int x = minX; x <= maxX; x++)
                {
                    TileCell c = board.Grid.GetCell(x, y);
                    if (c != null) targetCells.Add(c);
                }
            }
        }
        else // isSameCol
        {
            int x = cellA.coordinates.x;

            if (clearEntireLine)
            {
                // 해당 세로줄(열) 전체 삭제
                for (int y = 0; y < board.Grid.Height; y++)
                {
                    TileCell c = board.Grid.GetCell(x, y);
                    if (c != null) targetCells.Add(c);
                }
            }
            else
            {
                // 두 타일 사이(포함) 구간만 삭제
                int minY = Mathf.Min(cellA.coordinates.y, cellB.coordinates.y);
                int maxY = Mathf.Max(cellA.coordinates.y, cellB.coordinates.y);

                for (int y = minY; y <= maxY; y++)
                {
                    TileCell c = board.Grid.GetCell(x, y);
                    if (c != null) targetCells.Add(c);
                }
            }
        }

        // 3. 타일 수집 및 점수 합산
        int totalScore = 0;
        List<Tile> tilesToDestroy = new List<Tile>();

        foreach (TileCell c in targetCells)
        {
            if (c.Occupied && c.tile != null)
            {
                Tile tile = c.tile;
                if (tile.state != null)
                {
                    totalScore += tile.state.number;
                }
                tilesToDestroy.Add(tile);
            }
        }

        // 타일 삭제 및 보드에서 제거
        int destroyedCount = tilesToDestroy.Count;
        foreach (Tile tile in tilesToDestroy)
        {
            board.DestroyTile(tile);
        }

        // 4. 획득한 총 점수 적용
        if (GameManager.Instance != null && totalScore > 0)
        {
            GameManager.Instance.IncreaseScore(totalScore);
        }

        // 5. 사용 횟수 1 차감 및 UI 업데이트
        eraseCount--;
        UpdateEraseCountUI();

        string lineDirection = isSameRow ? "가로" : "세로";
        Debug.Log($"[EraseAbility] {lineDirection}줄 지우기 완료! ({cellA.coordinates} ~ {cellB.coordinates}) - 삭제된 타일: {destroyedCount}개, 획득 점수: +{totalScore}점. 남은 횟수: {eraseCount}/{maxEraseCount}");

        // 6. 지우기 모드 종료 및 보드 조작 재개
        firstSelectedCell = null;
        isEraseMode = false;

        if (board != null)
        {
            board.enabled = true;
        }

        // 7. 지우기 완료 후 게임 오버 여부 재확인
        StartCoroutine(CheckGameOverAfterErase());
    }

    /// <summary>
    /// 지우기 완료 후 게임 오버 상태를 판정하는 코루틴입니다.
    /// </summary>
    private IEnumerator CheckGameOverAfterErase()
    {
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
    /// 지우기 모드를 취소하고 원래 상태로 되돌립니다.
    /// </summary>
    public void CancelEraseMode()
    {
        if (firstSelectedCell != null)
        {
            HighlightCell(firstSelectedCell, false);
        }

        firstSelectedCell = null;
        isEraseMode = false;

        // 보드 조작 다시 활성화
        if (board != null)
        {
            board.enabled = true;
        }
    }

    /// <summary>
    /// 선택된 칸을 시각적으로 강조 표시합니다.
    /// 타일이 있으면 타일 크기를 확대/복원하고, 셀 자체에도 강조 색상을 적용하여 빈 칸 선택 시에도 시각적 피드백을 제공합니다.
    /// </summary>
    private void HighlightCell(TileCell cell, bool isHighlighted)
    {
        if (cell == null) return;

        // 1. 해당 칸에 타일이 있는 경우 타일 확대/복원
        if (cell.tile != null)
        {
            if (isHighlighted)
            {
                originalFirstTileScale = cell.tile.transform.localScale;
                cell.tile.transform.localScale = originalFirstTileScale * 1.15f;
            }
            else
            {
                cell.tile.transform.localScale = originalFirstTileScale;
            }
        }

        // 2. 셀 이미지에 대한 시각적 피드백 (빈 칸 및 타일 칸 공통)
        Image cellImg = cell.GetComponent<Image>();
        if (cellImg != null)
        {
            if (isHighlighted)
            {
                highlightedCellImage = cellImg;
                originalCellColor = cellImg.color;
                // 선택된 칸임을 명확히 인지할 수 있도록 밝고 따뜻한 톤으로 강조
                cellImg.color = new Color(0.95f, 0.85f, 0.45f, cellImg.color.a);
            }
            else
            {
                if (highlightedCellImage != null)
                {
                    highlightedCellImage.color = originalCellColor;
                    highlightedCellImage = null;
                }
                else
                {
                    cellImg.color = originalCellColor;
                }
            }
        }
    }

    /// <summary>
    /// 지우기 남은 횟수 UI 텍스트와 버튼 상태를 갱신합니다.
    /// </summary>
    public void UpdateEraseCountUI()
    {
        if (eraseCountText != null)
        {
            eraseCountText.text = eraseCount.ToString();
        }

        if (eraseButton != null)
        {
            eraseButton.interactable = eraseCount > 0;
        }
    }

    /// <summary>
    /// 지우기 사용 횟수를 최대치(3회)로 초기화합니다. (새 게임 시작 시 호출)
    /// </summary>
    public void ResetEraseCount()
    {
        CancelEraseMode();
        eraseCount = maxEraseCount;
        UpdateEraseCountUI();
    }
}
