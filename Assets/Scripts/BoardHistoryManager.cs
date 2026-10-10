using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 행동(타일 이동, 스왑 등 어빌리티)으로 타일 배치가 변경되기 직전 1회의 정보를 저장하고 복원하는 매니저 클래스입니다.
/// 향후 되돌리기(Undo), 어빌리티 취소, 직전 배치 비교 등의 기능을 구현하기 위한 토대가 됩니다.
/// </summary>
public class BoardHistoryManager : MonoBehaviour
{
    /// <summary>
    /// 싱글톤 인스턴스 (어디서든 접근 가능)
    /// </summary>
    public static BoardHistoryManager Instance { get; private set; }

    /// <summary>
    /// 단일 타일의 위치와 상태 정보를 저장하는 데이터 구조체입니다.
    /// 게임오브젝트 참조 대신 순수 데이터를 저장하므로 파괴/생성 시에도 안전합니다.
    /// </summary>
    [System.Serializable]
    public struct TileData
    {
        public Vector2Int coordinates; // 타일이 위치했던 그리드 좌표 (x, y)
        public int number;             // 타일의 숫자값 (2, 4, 8, ...)
        public int stateIndex;         // TileBoard의 tileStates 배열 인덱스
        public bool isTimerBonus;      // 특수 타일 (타이머 보너스) 여부

        public TileData(Vector2Int coordinates, int number, int stateIndex, bool isTimerBonus = false)
        {
            this.coordinates = coordinates;
            this.number = number;
            this.stateIndex = stateIndex;
            this.isTimerBonus = isTimerBonus;
        }
    }

    /// <summary>
    /// 특정 시점의 보드 전체 타일 배치 및 점수를 캡처한 스냅샷 클래스입니다.
    /// </summary>
    [System.Serializable]
    public class BoardSnapshot
    {
        public List<TileData> tiles = new List<TileData>();
        public int score; // 당시 점수

        public BoardSnapshot(List<TileData> tiles, int score)
        {
            this.tiles = tiles;
            this.score = score;
        }
    }

    [Header("참조 컴포넌트")]
    [Tooltip("타일 보드 컴포넌트 (비어있으면 자동으로 찾습니다)")]
    [SerializeField] private TileBoard board;

    /// <summary>
    /// 직전 1회의 확정된 보드 상태 스냅샷입니다.
    /// </summary>
    private BoardSnapshot previousSnapshot = null;

    /// <summary>
    /// 이동 시도 직전에 캡처해두고, 실제로 보드가 변경되었을 때만 확정하기 위한 임시 스냅샷입니다.
    /// </summary>
    private BoardSnapshot pendingSnapshot = null;

    /// <summary>
    /// 되돌릴 수 있는 이전 상태가 존재하는지 여부입니다. (UI 버튼 활성화 등에 활용)
    /// </summary>
    public bool HasPreviousState => previousSnapshot != null;

    /// <summary>
    /// 직전 스냅샷 데이터를 읽기 전용으로 조회합니다.
    /// </summary>
    public BoardSnapshot PreviousSnapshot => previousSnapshot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (board == null)
        {
            board = FindFirstObjectByType<TileBoard>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 현재 보드 상태를 즉시 직전 1회 기록으로 저장합니다.
    /// 어빌리티(예: 타일 스왑) 발동 직전이나 수동으로 즉시 백업해야 할 때 호출합니다.
    /// </summary>
    public void SaveCurrentState()
    {
        previousSnapshot = CaptureCurrentSnapshot();
    }

    /// <summary>
    /// 타일 이동 연산 시작 직전에 호출하여 임시 상태를 기록합니다.
    /// 실제로 타일이 하나라도 움직였을 때 CommitPendingState()를 호출하여 확정합니다.
    /// </summary>
    public void PrepareStateBeforeMove()
    {
        pendingSnapshot = CaptureCurrentSnapshot();
    }

    /// <summary>
    /// 타일이 실제로 이동/합체되었음이 확인되었을 때, 임시 스냅샷을 확정된 직전 상태로 저장합니다.
    /// </summary>
    public void CommitPendingState()
    {
        if (pendingSnapshot != null)
        {
            previousSnapshot = pendingSnapshot;
            pendingSnapshot = null;
        }
    }

    /// <summary>
    /// 타일 이동이 실패(아무 타일도 움직이지 않음)했을 때 임시 스냅샷을 취소합니다.
    /// </summary>
    public void CancelPendingState()
    {
        pendingSnapshot = null;
    }

    /// <summary>
    /// 저장된 직전 1회 상태로 보드와 점수를 복원합니다.
    /// </summary>
    /// <returns>복원 성공 여부 (저장된 상태가 없으면 false)</returns>
    public bool RestorePreviousState()
    {
        if (!HasPreviousState || board == null)
        {
            Debug.LogWarning("[BoardHistoryManager] 복원할 이전 상태가 없습니다.");
            return false;
        }

        // 1. 현재 화면의 모든 타일을 비웁니다.
        board.ClearBoard();

        // 2. 스냅샷에 저장된 타일들을 복원합니다.
        foreach (var data in previousSnapshot.tiles)
        {
            TileCell cell = board.Grid.GetCell(data.coordinates);
            if (cell != null)
            {
                TileState state = board.GetTileState(data.stateIndex);
                if (state != null)
                {
                    board.CreateTile(cell, state, data.isTimerBonus);
                }
            }
        }

        // 3. 점수도 당시 점수로 복원합니다.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetScore(previousSnapshot.score);
        }

        // 4. 1회 복원 후 스냅샷을 비웁니다. (원하는 정책에 따라 유지하도록 변경 가능)
        previousSnapshot = null;

        Debug.Log("[BoardHistoryManager] 이전 상태로 보드가 복원되었습니다.");
        return true;
    }

    /// <summary>
    /// 히스토리 기록을 초기화합니다. (새 게임 시작 시 호출)
    /// </summary>
    public void ClearHistory()
    {
        previousSnapshot = null;
        pendingSnapshot = null;
    }

    /// <summary>
    /// 현재 보드의 타일 배치와 점수를 캡처하여 스냅샷 객체로 생성합니다.
    /// </summary>
    private BoardSnapshot CaptureCurrentSnapshot()
    {
        if (board == null || board.Grid == null) return null;

        List<TileData> tileList = new List<TileData>();

        // 그리드의 모든 셀을 검사하여 타일이 있는 칸만 데이터로 저장합니다.
        foreach (var cell in board.Grid.cells)
        {
            if (cell.Occupied && cell.tile != null && cell.tile.state != null)
            {
                int stateIndex = board.IndexOf(cell.tile.state);
                tileList.Add(new TileData(cell.coordinates, cell.tile.state.number, stateIndex, cell.tile.isTimerBonus));
            }
        }

        int currentScore = GameManager.Instance != null ? GameManager.Instance.score : 0;
        return new BoardSnapshot(tileList, currentScore);
    }
}
