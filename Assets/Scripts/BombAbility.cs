using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스크립트명: BombAbility
/// 기능: Scene의 "Bomb" 버튼을 누르면 활성화되는 폭탄 어빌리티입니다.
/// - 중앙 2x2 영역의 타일 중 하나를 지정합니다.
/// - 지정한 타일을 중심으로 3x3 범위 내에 있는 모든 타일을 일괄 삭제합니다.
/// - 삭제된 타일들의 숫자(값)를 모두 합산하여 플레이어 점수에 가산합니다.
/// - 최대 사용 횟수는 3회로 제한되며 인스펙터에서 관리할 수 있습니다.
/// </summary>
public class BombAbility : MonoBehaviour
{
    [Header("폭탄 횟수 관리")]
    [Tooltip("폭탄 어빌리티의 최대 사용 가능 횟수")]
    [SerializeField] private int maxBombCount = 1;

    [Tooltip("현재 남은 폭탄 사용 횟수 (기본값: 3)")]
    [SerializeField] private int bombCount = 1;

    [Header("폭탄 옵션")]
    [Tooltip("중심 칸으로 선택할 때 반드시 타일이 존재해야 하는지 여부 (기본값: true, false면 빈 칸도 중심점으로 선택 가능)")]
    [SerializeField] private bool requireTileOnCenter = true;

    [Header("UI 컴포넌트")]
    [Tooltip("남은 폭탄 횟수를 표시할 TextMeshPro UI (선택 사항)")]
    [SerializeField] private TextMeshProUGUI bombCountText;

    [Tooltip("Bomb 버튼 컴포넌트 (비어있으면 현재 오브젝트에서 자동 탐색)")]
    [SerializeField] private Button bombButton;

    [Header("보드 참조")]
    [Tooltip("TileBoard 컴포넌트 참조 (비어있으면 씬에서 자동 탐색)")]
    [SerializeField] private TileBoard board;

    public bool IsBombMode => isBombMode;
    private bool isBombMode = false;

    private bool isListenersRegistered = false;

    private void Awake()
    {
        // 씬 내의 TileBoard 자동 탐색
        if (board == null)
        {
            board = FindFirstObjectByType<TileBoard>();
        }

        // 현재 게임오브젝트에 Button이 붙어있다면 자동 연결
        if (bombButton == null)
        {
            bombButton = GetComponent<Button>();
        }
    }

    private void Start()
    {
        // 1. 남은 횟수 UI 텍스트 갱신
        UpdateBombCountUI();

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
            Debug.LogWarning("[BombAbility] TileBoard 또는 TileGrid를 아직 찾지 못했습니다. 폭탄 버튼 클릭 시 재시도합니다.");
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
                Debug.LogWarning($"[BombAbility] Cell {cell.name}에 Button 컴포넌트가 없습니다.");
            }
        }

        // 보드 위 타일 오브젝트들이 Cell의 터치를 가로막지 않도록 Raycast Target 해제
        EnsureTilesDoNotBlockRaycast();

        isListenersRegistered = true;
        Debug.Log($"[BombAbility] 총 {registeredCount}개의 Cell 버튼 등록 및 RaycastTarget 활성화 완료!");
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
    /// 메인 메서드: Bomb 버튼을 클릭했을 때 호출되는 함수입니다.
    /// Canvas > Abilities > Bomb 버튼의 OnClick 이벤트에 이 함수를 할당하세요.
    /// </summary>
    public void OnClickBombButton()
    {
        // 1. 남은 사용 횟수 검사
        if (bombCount <= 0)
        {
            Debug.Log("[BombAbility] 폭탄 능력을 모두 사용했습니다! (최대 3회)");
            return;
        }

        // 2. 이미 폭탄 모드 진행 중인 상태에서 Bomb 버튼을 다시 누른 경우 -> 취소 토글
        if (isBombMode)
        {
            CancelBombMode();
            Debug.Log("[BombAbility] 폭탄 모드가 취소되었습니다.");
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

        // 3. 폭탄 모드 활성화
        isBombMode = true;

        // 타일 선택 도중 보드가 스와이프로 밀리지 않도록 보드 입력 잠시 비활성화
        if (board != null)
        {
            board.enabled = false;
        }

        Debug.Log("[BombAbility] 폭탄 모드 시작! 중앙 2x2 타일 중 하나를 터치하세요.");
    }

    public void UseBombAbility()
    {
        OnClickBombButton();
    }

    /// <summary>
    /// 개별 Cell(버튼)이 클릭되었을 때 호출되는 함수입니다.
    /// </summary>
    /// <param name="cell">클릭된 타일 칸</param>
    public void OnCellClicked(TileCell cell)
    {
        Debug.Log($"[BombAbility] Cell 클릭 감지됨: {cell?.name} (좌표: {cell?.coordinates}) / 폭탄모드: {isBombMode}");

        // 폭탄 모드가 아닐 때는 셀 터치 무시
        if (!isBombMode || cell == null) return;

        // 중앙 2x2 영역인지 판정
        if (!IsCenter2x2(cell.coordinates))
        {
            Debug.LogWarning($"[BombAbility] 선택한 칸({cell.coordinates})은 중앙 2x2 영역이 아닙니다. 중앙 2x2에 위치한 타일을 선택해주세요.");
            return;
        }

        // 중심 칸에 타일이 반드시 있어야 하는 옵션인 경우 빈 칸 체크
        if (requireTileOnCenter && cell.Empty)
        {
            Debug.Log("[BombAbility] 선택한 칸이 비어 있습니다. 숫자가 적힌 중앙 타일을 선택해주세요.");
            return;
        }

        // 폭탄 실행!
        ExecuteBomb(cell);
    }

    /// <summary>
    /// 지정한 좌표가 보드의 중앙 2x2 영역에 해당하는지 판별합니다.
    /// (예: 4x4 보드의 경우 x: 1~2, y: 1~2)
    /// </summary>
    private bool IsCenter2x2(Vector2Int coord)
    {
        if (board == null || board.Grid == null) return false;

        int width = board.Grid.Width;
        int height = board.Grid.Height;

        int minX = (width - 1) / 2;
        int maxX = width / 2;
        int minY = (height - 1) / 2;
        int maxY = height / 2;

        return coord.x >= minX && coord.x <= maxX && coord.y >= minY && coord.y <= maxY;
    }

    /// <summary>
    /// 선택된 셀을 중심으로 3x3 범위 안의 타일을 모두 삭제하고 점수를 합산하여 추가합니다.
    /// </summary>
    /// <param name="centerCell">중심이 되는 셀</param>
    private void ExecuteBomb(TileCell centerCell)
    {
        if (board == null || board.Grid == null)
        {
            CancelBombMode();
            return;
        }

        // 1. 직전 상태 저장 (되돌리기 Undo 기능과의 연동)
        if (BoardHistoryManager.Instance != null)
        {
            BoardHistoryManager.Instance.SaveCurrentState();
        }

        // 2. 중심 칸 기준 3x3 영역 범위 계산
        int cx = centerCell.coordinates.x;
        int cy = centerCell.coordinates.y;

        int startX = Mathf.Max(0, cx - 1);
        int endX = Mathf.Min(board.Grid.Width - 1, cx + 1);
        int startY = Mathf.Max(0, cy - 1);
        int endY = Mathf.Min(board.Grid.Height - 1, cy + 1);

        int totalScore = 0;
        List<Tile> tilesToDestroy = new List<Tile>();

        // 3x3 범위 내의 모든 타일 수집 및 점수 합산
        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                TileCell targetCell = board.Grid.GetCell(x, y);
                if (targetCell != null && targetCell.Occupied && targetCell.tile != null)
                {
                    Tile tile = targetCell.tile;
                    if (tile.state != null)
                    {
                        totalScore += tile.state.number;
                    }
                    tilesToDestroy.Add(tile);
                }
            }
        }

        // 수집된 타일들 파괴 및 보드에서 제거
        int destroyedCount = tilesToDestroy.Count;
        foreach (Tile tile in tilesToDestroy)
        {
            board.DestroyTile(tile);
        }

        // 3. 획득한 총 점수 적용
        if (GameManager.Instance != null && totalScore > 0)
        {
            GameManager.Instance.IncreaseScore(totalScore);
        }

        // 4. 사용 횟수 1 차감 및 UI 업데이트
        bombCount--;
        UpdateBombCountUI();

        Debug.Log($"[BombAbility] 폭탄 폭발 완료! (중심: {centerCell.coordinates}) - 삭제된 타일: {destroyedCount}개, 획득 점수: +{totalScore}점. 남은 횟수: {bombCount}/{maxBombCount}");

        // 5. 폭탄 모드 종료 및 보드 조작 재개
        isBombMode = false;

        if (board != null)
        {
            board.enabled = true;
        }

        // 6. 폭탄 사용 후 게임 오버 여부 재확인
        StartCoroutine(CheckGameOverAfterBomb());
    }

    /// <summary>
    /// 폭탄 폭발 후 게임 오버 상태를 판정하는 코루틴입니다.
    /// </summary>
    private IEnumerator CheckGameOverAfterBomb()
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
    /// 폭탄 모드를 취소하고 원래 상태로 되돌립니다.
    /// </summary>
    public void CancelBombMode()
    {
        isBombMode = false;

        // 보드 조작 다시 활성화
        if (board != null)
        {
            board.enabled = true;
        }
    }

    /// <summary>
    /// 폭탄 남은 횟수 UI 텍스트와 버튼 상태를 갱신합니다.
    /// </summary>
    public void UpdateBombCountUI()
    {
        if (bombCountText != null)
        {
            bombCountText.text = bombCount.ToString();
        }

        if (bombButton != null)
        {
            bombButton.interactable = bombCount > 0;
        }
    }

    /// <summary>
    /// 폭탄 사용 횟수를 최대치(3회)로 초기화합니다. (새 게임 시작 시 호출)
    /// </summary>
    public void ResetBombCount()
    {
        CancelBombMode();
        bombCount = maxBombCount;
        UpdateBombCountUI();
    }
}
