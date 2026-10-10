using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스크립트명: TileSwapAbility
/// 기능: Scene의 "Swap" 버튼을 누르면 활성화되는 타일 위치 교환 어빌리티입니다.
/// - 원하는 타일 2개를 터치하여 서로의 위치를 맞바꿉니다.
/// - 처음 지정한 타일을 다시 누르면 선택이 취소됩니다.
/// - 최대 사용 횟수는 3회로 제한되며 인스펙터에서 관리할 수 있습니다.
/// </summary>
public class TileSwapAbility : MonoBehaviour
{
    [Header("스왑 횟수 관리")]
    [Tooltip("스왑 어빌리티의 최대 사용 가능 횟수")]
    [SerializeField] private int maxSwapCount = 3;

    [Tooltip("현재 남은 스왑 사용 횟수 (기본값: 3)")]
    [SerializeField] private int swapCount = 3;

    [Header("UI 컴포넌트")]
    [Tooltip("남은 스왑 횟수를 표시할 TextMeshPro UI (선택 사항)")]
    [SerializeField] private TextMeshProUGUI swapCountText;

    [Tooltip("Swap 버튼 컴포넌트 (비어있으면 현재 오브젝트에서 자동 탐색)")]
    [SerializeField] private Button swapButton;

    [Header("보드 참조")]
    [Tooltip("TileBoard 컴포넌트 참조 (비어있으면 씬에서 자동 탐색)")]
    [SerializeField] private TileBoard board;

    public bool IsSwapMode => isSwapMode;
    private bool isSwapMode = false;

    private TileCell firstSelectedCell = null;
    private Vector3 originalFirstTileScale = Vector3.one;
    private bool isListenersRegistered = false;

    private void Awake()
    {
        // 씬 내의 TileBoard 자동 탐색
        if (board == null)
        {
            board = FindFirstObjectByType<TileBoard>();
        }

        // 현재 게임오브젝트에 Button이 붙어있다면 자동 연결
        if (swapButton == null)
        {
            swapButton = GetComponent<Button>();
        }
    }

    private void Start()
    {
        // 1. 남은 횟수 UI 텍스트 갱신
        UpdateSwapCountUI();

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
            Debug.LogWarning("[TileSwapAbility] TileBoard 또는 TileGrid를 아직 찾지 못했습니다. 스왑 버튼 클릭 시 재시도합니다.");
            return;
        }

        int registeredCount = 0;
        foreach (TileCell cell in board.Grid.cells)
        {
            if (cell == null) continue;

            // [핵심 해결책 1] Cell의 Image 컴포넌트에서 Raycast Target이 꺼져 있으면 Button이 클릭을 감지할 수 없습니다.
            // 코드를 통해 자동으로 Raycast Target을 활성화합니다.
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
                Debug.LogWarning($"[TileSwapAbility] Cell {cell.name}에 Button 컴포넌트가 없습니다.");
            }
        }

        // [핵심 해결책 2] 보드 위 타일 오브젝트들이 Cell의 터치를 가로막지 않도록 Raycast Target 해제
        EnsureTilesDoNotBlockRaycast();

        isListenersRegistered = true;
        Debug.Log($"[TileSwapAbility] 총 {registeredCount}개의 Cell 버튼 등록 및 RaycastTarget 활성화 완료!");
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
    /// 메인 메서드: Swap 버튼을 클릭했을 때 호출되는 함수입니다.
    /// Canvas > Abilities > Swap 버튼의 OnClick 이벤트에 이 함수를 할당하세요.
    /// </summary>
    public void OnClickSwapButton()
    {
        // 1. 남은 사용 횟수 검사
        if (swapCount <= 0)
        {
            Debug.Log("[TileSwapAbility] 스왑 능력을 모두 사용했습니다! (최대 3회)");
            return;
        }

        // 2. 이미 스왑 모드 진행 중인 상태에서 Swap 버튼을 다시 누른 경우 -> 취소 토글
        if (isSwapMode)
        {
            CancelSwapMode();
            Debug.Log("[TileSwapAbility] 스왑 모드가 취소되었습니다.");
            return;
        }

        // 혹시 TP 모드가 활성화되어 있다면 충돌 방지를 위해 TP 모드 취소
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

        // 리스너가 아직 등록되지 않았었다면 즉시 등록
        if (!isListenersRegistered)
        {
            RegisterCellButtonListeners();
        }

        // 보드 위 타일들이 셀 터치를 방해하지 않도록 Raycast Target 정리
        EnsureTilesDoNotBlockRaycast();

        // 3. 스왑 모드 활성화
        isSwapMode = true;
        firstSelectedCell = null;

        // 타일 선택 도중 보드가 스와이프로 밀리지 않도록 보드 입력 잠시 비활성화
        if (board != null)
        {
            board.enabled = false;
        }

        Debug.Log("[TileSwapAbility] 스왑 모드 시작! 교환할 첫 번째 타일을 터치하세요.");
    }

    public void UseSwapAbility()
    {
        OnClickSwapButton();
    }

    /// <summary>
    /// 개별 Cell(버튼)이 클릭되었을 때 호출되는 함수입니다.
    /// </summary>
    /// <param name="cell">클릭된 타일 칸</param>
    public void OnCellClicked(TileCell cell)
    {
        Debug.Log($"[TileSwapAbility] Cell 클릭 감지됨: {cell?.name} (좌표: {cell?.coordinates}) / 스왑모드: {isSwapMode} / 타일존재: {!cell?.Empty}");

        // 스왑 모드가 아닐 때는 셀 터치 무시
        if (!isSwapMode || cell == null) return;

        // 빈 칸은 스왑 대상으로 선택할 수 없음 (타일이 존재하는 칸만 선택 가능)
        if (cell.Empty)
        {
            Debug.Log("[TileSwapAbility] 빈 칸은 선택할 수 없습니다. 숫자가 적힌 타일을 선택해주세요.");
            return;
        }

        // [1] 첫 번째 타일을 선택하는 단계
        if (firstSelectedCell == null)
        {
            firstSelectedCell = cell;
            HighlightTile(cell.tile, true);
            Debug.Log($"[TileSwapAbility] 첫 번째 타일 선택: {cell.coordinates} (값: {cell.tile.state.number}). 다른 타일을 누르면 위치가 바뀌며, 같은 타일을 다시 누르면 취소됩니다.");
        }
        // [2] 두 번째 타일을 선택하는 단계
        else
        {
            // [2-A] 처음에 누른 타일을 다시 누른 경우 -> 선택 취소
            if (firstSelectedCell == cell)
            {
                HighlightTile(firstSelectedCell.tile, false);
                firstSelectedCell = null;
                Debug.Log("[TileSwapAbility] 첫 번째 타일 선택이 취소되었습니다. 다시 타일을 선택하세요.");
            }
            // [2-B] 서로 다른 두 번째 타일을 누른 경우 -> 두 타일의 위치 교환(스왑) 실행!
            else
            {
                HighlightTile(firstSelectedCell.tile, false);
                ExecuteSwap(firstSelectedCell, cell);
            }
        }
    }

    /// <summary>
    /// 두 셀에 위치한 타일의 위치를 실제로 맞바꾸고 후속 처리를 진행합니다.
    /// </summary>
    private void ExecuteSwap(TileCell cellA, TileCell cellB)
    {
        Tile tileA = cellA.tile;
        Tile tileB = cellB.tile;

        if (tileA == null || tileB == null)
        {
            CancelSwapMode();
            return;
        }

        // 1. 직전 상태 저장 (되돌리기 Undo 기능과의 연동)
        if (BoardHistoryManager.Instance != null)
        {
            BoardHistoryManager.Instance.SaveCurrentState();
        }

        // 2. 두 타일 간의 위치 스왑 애니메이션 및 셀 연결 갱신
        tileA.MoveToForSwap(cellB);
        tileB.MoveToForSwap(cellA);

        // 3. 사용 횟수 1 차감 및 UI 업데이트
        swapCount--;
        UpdateSwapCountUI();

        Debug.Log($"[TileSwapAbility] 타일 스왑 완료! 남은 횟수: {swapCount}/{maxSwapCount}");

        // 4. 스왑 모드 종료 및 보드 조작 재개
        firstSelectedCell = null;
        isSwapMode = false;

        if (board != null)
        {
            board.enabled = true;
        }

        // 5. 스왑 완료 후 게임 오버 여부 재확인
        StartCoroutine(CheckGameOverAfterSwap());
    }

    /// <summary>
    /// 스왑 애니메이션 완료 후 게임 오버 상태를 판정하는 코루틴입니다.
    /// </summary>
    private IEnumerator CheckGameOverAfterSwap()
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
    /// 스왑 모드를 취소하고 원래 상태로 되돌립니다.
    /// </summary>
    public void CancelSwapMode()
    {
        if (firstSelectedCell != null && firstSelectedCell.tile != null)
        {
            HighlightTile(firstSelectedCell.tile, false);
        }

        firstSelectedCell = null;
        isSwapMode = false;

        // 보드 조작 다시 활성화
        if (board != null)
        {
            board.enabled = true;
        }
    }

    /// <summary>
    /// 선택된 타일을 시각적으로 강조 표시합니다 (약간 확대/복원).
    /// </summary>
    private void HighlightTile(Tile tile, bool isHighlighted)
    {
        if (tile == null) return;

        if (isHighlighted)
        {
            originalFirstTileScale = tile.transform.localScale;
            tile.transform.localScale = originalFirstTileScale * 1.15f;
        }
        else
        {
            tile.transform.localScale = originalFirstTileScale;
        }
    }

    /// <summary>
    /// 스왑 남은 횟수 UI 텍스트와 버튼 상태를 갱신합니다.
    /// </summary>
    public void UpdateSwapCountUI()
    {
        if (swapCountText != null)
        {
            swapCountText.text = swapCount.ToString();
        }

        if (swapButton != null)
        {
            swapButton.interactable = swapCount > 0;
        }
    }

    /// <summary>
    /// 스왑 사용 횟수를 최대치(3회)로 초기화합니다. (새 게임 시작 시 호출)
    /// </summary>
    public void ResetSwapCount()
    {
        CancelSwapMode();
        swapCount = maxSwapCount;
        UpdateSwapCountUI();
    }
}
