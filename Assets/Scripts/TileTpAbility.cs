using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스크립트명: TileTpAbility
/// 기능: Scene의 "Tp" (Teleport) 버튼을 누르면 활성화되는 타일 순간이동/이동 어빌리티입니다.
/// - 원하는 타일 1개를 터치하여 선택한 후, 비어있는 칸을 터치하면 해당 위치로 타일을 옮깁니다.
/// - 처음 지정한 타일을 다시 누르면 선택이 취소됩니다.
/// - 다른 숫자가 있는 타일을 누르면 이동시킬 대상 타일이 새로 누른 타일로 변경됩니다.
/// - 최대 사용 횟수는 3회로 제한되며 인스펙터에서 관리할 수 있습니다.
/// </summary>
public class TileTpAbility : MonoBehaviour
{
    [Header("텔레포트 횟수 관리")]
    [Tooltip("텔레포트 어빌리티의 최대 사용 가능 횟수")]
    [SerializeField] private int maxTpCount = 3;

    [Tooltip("현재 남은 텔레포트 사용 횟수 (기본값: 3)")]
    [SerializeField] private int tpCount = 3;

    [Header("UI 컴포넌트")]
    [Tooltip("남은 텔레포트 횟수를 표시할 TextMeshPro UI (선택 사항)")]
    [SerializeField] private TextMeshProUGUI tpCountText;

    [Tooltip("Tp 버튼 컴포넌트 (비어있으면 현재 오브젝트에서 자동 탐색)")]
    [SerializeField] private Button tpButton;

    [Header("보드 참조")]
    [Tooltip("TileBoard 컴포넌트 참조 (비어있으면 씬에서 자동 탐색)")]
    [SerializeField] private TileBoard board;

    public bool IsTpMode => isTpMode;
    private bool isTpMode = false;

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
        if (tpButton == null)
        {
            tpButton = GetComponent<Button>();
        }
    }

    private void Start()
    {
        // 1. 남은 횟수 UI 텍스트 갱신
        UpdateTpCountUI();

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
            Debug.LogWarning("[TileTpAbility] TileBoard 또는 TileGrid를 아직 찾지 못했습니다. TP 버튼 클릭 시 재시도합니다.");
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
                Debug.LogWarning($"[TileTpAbility] Cell {cell.name}에 Button 컴포넌트가 없습니다.");
            }
        }

        // [핵심 해결책 2] 보드 위 타일 오브젝트들이 Cell의 터치를 가로막지 않도록 Raycast Target 해제
        EnsureTilesDoNotBlockRaycast();

        isListenersRegistered = true;
        Debug.Log($"[TileTpAbility] 총 {registeredCount}개의 Cell 버튼 등록 및 RaycastTarget 활성화 완료!");
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
    /// 메인 메서드: TP 버튼을 클릭했을 때 호출되는 함수입니다.
    /// Canvas > Abilities > TP 버튼의 OnClick 이벤트에 이 함수를 할당하세요.
    /// </summary>
    public void OnClickTpButton()
    {
        // 1. 남은 사용 횟수 검사
        if (tpCount <= 0)
        {
            Debug.Log("[TileTpAbility] 텔레포트 능력을 모두 사용했습니다! (최대 3회)");
            return;
        }

        // 2. 이미 TP 모드 진행 중인 상태에서 TP 버튼을 다시 누른 경우 -> 취소 토글
        if (isTpMode)
        {
            CancelTpMode();
            Debug.Log("[TileTpAbility] 텔레포트 모드가 취소되었습니다.");
            return;
        }

        // 혹시 스왑 모드가 활성화되어 있다면 충돌 방지를 위해 스왑 모드 취소
        TileSwapAbility swap = FindFirstObjectByType<TileSwapAbility>();
        if (swap != null && swap.IsSwapMode)
        {
            swap.CancelSwapMode();
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

        // 3. TP 모드 활성화
        isTpMode = true;
        firstSelectedCell = null;

        // 타일 선택 도중 보드가 스와이프로 밀리지 않도록 보드 입력 잠시 비활성화
        if (board != null)
        {
            board.enabled = false;
        }

        Debug.Log("[TileTpAbility] 텔레포트 모드 시작! 이동할 타일을 먼저 선택하세요.");
    }

    public void UseTpAbility()
    {
        OnClickTpButton();
    }

    /// <summary>
    /// 개별 Cell(버튼)이 클릭되었을 때 호출되는 함수입니다.
    /// </summary>
    /// <param name="cell">클릭된 타일 칸</param>
    public void OnCellClicked(TileCell cell)
    {
        Debug.Log($"[TileTpAbility] Cell 클릭 감지됨: {cell?.name} (좌표: {cell?.coordinates}) / TP모드: {isTpMode} / 빈칸여부: {cell?.Empty}");

        // TP 모드가 아닐 때는 셀 터치 무시
        if (!isTpMode || cell == null) return;

        // [1] 이동할 타일을 선택하는 단계 (첫 번째 터치)
        if (firstSelectedCell == null)
        {
            // 빈 칸은 이동시킬 수 없음 (타일이 존재하는 칸만 선택 가능)
            if (cell.Empty)
            {
                Debug.Log("[TileTpAbility] 빈 칸은 이동시킬 수 없습니다. 옮길 타일을 먼저 선택해주세요.");
                return;
            }

            firstSelectedCell = cell;
            HighlightTile(cell.tile, true);
            Debug.Log($"[TileTpAbility] 이동할 타일 선택: {cell.coordinates} (값: {cell.tile.state.number}). 이동할 빈 칸을 터치하세요. (같은 타일 재터치 시 취소)");
        }
        // [2] 이동할 목적지 칸을 선택하는 단계 (두 번째 터치)
        else
        {
            // [2-A] 처음에 누른 타일을 다시 누른 경우 -> 선택 취소
            if (firstSelectedCell == cell)
            {
                HighlightTile(firstSelectedCell.tile, false);
                firstSelectedCell = null;
                Debug.Log("[TileTpAbility] 타일 선택이 취소되었습니다. 다시 타일을 선택하세요.");
            }
            // [2-B] 이미 타일이 존재하는 다른 칸을 누른 경우 -> 이동할 대상 타일 변경 (UX 배려)
            else if (!cell.Empty)
            {
                HighlightTile(firstSelectedCell.tile, false);
                firstSelectedCell = cell;
                HighlightTile(cell.tile, true);
                Debug.Log($"[TileTpAbility] 이동할 타일을 변경했습니다: {cell.coordinates} (값: {cell.tile.state.number}). 이동할 빈 칸을 터치하세요.");
            }
            // [2-C] 비어있는 칸을 누른 경우 -> 선택한 타일을 해당 빈 칸으로 텔레포트(이동) 실행!
            else
            {
                HighlightTile(firstSelectedCell.tile, false);
                ExecuteTp(firstSelectedCell, cell);
            }
        }
    }

    /// <summary>
    /// 선택한 타일을 비어있는 목표 셀로 이동시키고 후속 처리를 진행합니다.
    /// </summary>
    private void ExecuteTp(TileCell fromCell, TileCell toCell)
    {
        Tile tile = fromCell.tile;

        if (tile == null || !toCell.Empty)
        {
            CancelTpMode();
            return;
        }

        // 1. 직전 상태 저장 (되돌리기 Undo 기능과의 연동)
        if (BoardHistoryManager.Instance != null)
        {
            BoardHistoryManager.Instance.SaveCurrentState();
        }

        // 2. 타일을 목표 빈 칸으로 이동 (Tile의 MoveTo는 기존 칸 연결 해제, 새 칸 연결 및 이동 애니메이션을 수행)
        tile.MoveTo(toCell);

        // 3. 사용 횟수 1 차감 및 UI 업데이트
        tpCount--;
        UpdateTpCountUI();

        Debug.Log($"[TileTpAbility] 타일 텔레포트 완료! ({fromCell.coordinates} -> {toCell.coordinates}) 남은 횟수: {tpCount}/{maxTpCount}");

        // 4. TP 모드 종료 및 보드 조작 재개
        firstSelectedCell = null;
        isTpMode = false;

        if (board != null)
        {
            board.enabled = true;
        }

        // 5. 이동 완료 후 게임 오버 여부 재확인
        StartCoroutine(CheckGameOverAfterTp());
    }

    /// <summary>
    /// 이동 애니메이션 완료 후 게임 오버 상태를 판정하는 코루틴입니다.
    /// </summary>
    private IEnumerator CheckGameOverAfterTp()
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
    /// TP 모드를 취소하고 원래 상태로 되돌립니다.
    /// </summary>
    public void CancelTpMode()
    {
        if (firstSelectedCell != null && firstSelectedCell.tile != null)
        {
            HighlightTile(firstSelectedCell.tile, false);
        }

        firstSelectedCell = null;
        isTpMode = false;

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
    /// 텔레포트 남은 횟수 UI 텍스트와 버튼 상태를 갱신합니다.
    /// </summary>
    public void UpdateTpCountUI()
    {
        if (tpCountText != null)
        {
            tpCountText.text = tpCount.ToString();
        }

        if (tpButton != null)
        {
            tpButton.interactable = tpCount > 0;
        }
    }

    /// <summary>
    /// 텔레포트 사용 횟수를 최대치(3회)로 초기화합니다. (새 게임 시작 시 호출)
    /// </summary>
    public void ResetTpCount()
    {
        CancelTpMode();
        tpCount = maxTpCount;
        UpdateTpCountUI();
    }
}
