using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2048 게임의 메인 보드를 관리하는 핵심 클래스입니다.
/// 플레이어의 키보드 입력(상, 하, 좌, 우)을 감지하고,
/// 타일의 이동, 합체, 새 타일 생성 및 게임 오버 판정을 총괄합니다.
/// </summary>
public class TileBoard : MonoBehaviour
{
    [Header("프리팹 및 데이터 설정")]
    [Tooltip("생성할 타일의 원본 프리팹")]
    [SerializeField] private Tile tilePrefab;

    [Tooltip("숫자 2, 4, 8, 16... 순서대로 저장된 타일 상태(데이터) 배열")]
    [SerializeField] private TileState[] tileStates;

    [Header("모바일 터치 설정")]
    [Tooltip("스와이프로 인식할 최소 드래그 거리 (픽셀)")]
    [SerializeField] private float minSwipeDistance = 50f;

    [Tooltip("상하좌우 축 기준 허용 각도 (예: 30도 설정 시 축 기준 ±30도 범위만 유효, 대각선은 무시)")]
    [Range(10f, 40f)]
    [SerializeField] private float swipeAngleThreshold = 30f;

    private Vector2 touchStartPosition;// 터치 시작 위치
    private bool isSwiping = false;

    /// <summary>
    /// 그리드(격자)를 제어하는 TileGrid 컴포넌트 참조입니다.
    /// </summary>
    private TileGrid grid;

    /// <summary>
    /// 현재 보드 위에 존재하는 모든 타일들을 담아두는 리스트입니다.
    /// </summary>
    private List<Tile> tiles;

    /// <summary>
    /// 타일이 이동하거나 합쳐지는 애니메이션 도중 중복 입력을 막기 위한 대기 플래그입니다.
    /// </summary>
    private bool waiting;

    /// <summary>
    /// 컴포넌트 초기화: 자식에서 TileGrid를 찾고 타일 보관용 리스트를 초기화합니다.
    /// </summary>
    private void Awake()
    {
        grid = GetComponentInChildren<TileGrid>();
        tiles = new List<Tile>(16); // 4x4 보드이므로 기본 용량을 16으로 잡습니다.
    }

    /// <summary>
    /// 보드를 깨끗이 비웁니다. 새 게임을 시작할 때 호출됩니다.
    /// 모든 칸의 타일 참조를 끊고, 기존 타일 게임오브젝트를 파괴합니다.
    /// </summary>
    public void ClearBoard()
    {
        // 1. 모든 그리드 칸에서 타일 연결을 해제합니다.
        foreach (var cell in grid.cells) {
            cell.tile = null;
        }

        // 2. 화면에 생성되어 있던 타일 오브젝트들을 모두 삭제합니다.
        foreach (var tile in tiles) {
            Destroy(tile.gameObject);
        }

        // 3. 타일 관리 리스트를 비웁니다.
        tiles.Clear();
    }

    /// <summary>
    /// 보드의 빈 칸 중 한 곳에 새 타일(기본 숫자 2)을 생성합니다.
    /// </summary>
    public void CreateTile()
    {
        // 프리팹으로부터 새 타일 인스턴스를 생성하고 부모를 grid로 지정합니다.
        Tile tile = Instantiate(tilePrefab, grid.transform);

        // 첫 번째 상태(일반적으로 숫자 2)를 적용합니다.
        tile.SetState(tileStates[0]);

        // 그리드에서 무작위 빈 칸을 찾아 배치합니다.
        tile.Spawn(grid.GetRandomEmptyCell());

        // 생성된 타일을 관리 리스트에 추가합니다.
        tiles.Add(tile);
    }

    /// <summary>
    /// 매 프레임마다 플레이어의 키 입력을 감지함.
    /// </summary>
        private void Update()
    {
        // 타일 이동 애니메이션 중에는 입력을 받지 않습니다.
        if (waiting) return;

        // 1. 모바일 터치 및 마우스 드래그(스와이프) 처리
        HandleSwipeInput();

        // 2. PC 에디터 테스트용 키보드 입력 유지
        HandleKeyboardInput();
    }

    /// <summary>
    /// 모바일 터치 및 에디터 마우스 드래그를 감지하여 타일을 이동시킵니다.
    /// </summary>
        private void HandleSwipeInput()
    {
        // 1. 터치 시작 (클릭 시작)
        if (Input.GetMouseButtonDown(0))
        {
            touchStartPosition = Input.mousePosition;
            isSwiping = true;
        }

        // 2. 드래그 중 판정
        if (Input.GetMouseButton(0) && isSwiping)
        {
            Vector2 currentPosition = Input.mousePosition;
            Vector2 delta = currentPosition - touchStartPosition;

            // 최소 거리 이상 드래그했을 때만 각도 검사
            if (delta.magnitude >= minSwipeDistance)
            {
                // 오른쪽 검사 (Vector2.right와의 각도가 threshold 이내인지)
                if (Vector2.Angle(delta, Vector2.right) <= swipeAngleThreshold)
                {
                    MoveRight();
                    isSwiping = false; // 이동 완료 후 이번 터치 종료
                }
                // 왼쪽 검사
                else if (Vector2.Angle(delta, Vector2.left) <= swipeAngleThreshold)
                {
                    MoveLeft();
                    isSwiping = false;
                }
                // 위쪽 검사
                else if (Vector2.Angle(delta, Vector2.up) <= swipeAngleThreshold)
                {
                    MoveUp();
                    isSwiping = false;
                }
                // 아래쪽 검사
                else if (Vector2.Angle(delta, Vector2.down) <= swipeAngleThreshold)
                {
                    MoveDown();
                    isSwiping = false;
                }
                // [핵심] 어느 축에도 해당하지 않는 애매한 대각선 각도라면?
                // 아무것도 호출하지 않고 isSwiping을 유지합니다.
                // 유저가 손가락을 더 확실한 방향으로 꺾으면 그때 발동되며,
                // 이 상태 그대로 손을 떼면 무시됩니다.
            }
        }

        // 3. 터치 종료
        if (Input.GetMouseButtonUp(0))
        {
            isSwiping = false;
        }
    }

    /// <summary>
    /// PC 에디터 개발 편의를 위한 키보드 입력 처리
    /// </summary>
    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) {
            MoveUp();
        } 
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) {
            MoveLeft();
        } 
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) {
            MoveDown();
        } 
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) {
            MoveRight();
        }
    }

    // 방향별 Move 호출 메서드 분리 (가독성 향상)
    private void MoveUp()    => Move(Vector2Int.up, 0, 1, 1, 1);
    private void MoveLeft()  => Move(Vector2Int.left, 1, 1, 0, 1);
    private void MoveDown()  => Move(Vector2Int.down, 0, 1, grid.Height - 2, -1);
    private void MoveRight() => Move(Vector2Int.right, grid.Width - 2, -1, 0, 1);

    /// <summary>
    /// 지정된 방향으로 보드 전체의 타일들을 밀어냅니다.
    /// 이동하려는 방향에 가장 가까운 줄부터 차례대로 검사해야 타일들이 올바르게 밀립니다.
    /// </summary>
    /// <param name="direction">이동 방향 (Vector2Int.up, down, left, right)</param>
    /// <param name="startX">가로 검사 시작 인덱스</param>
    /// <param name="incrementX">가로 검사 증감값 (+1 또는 -1)</param>
    /// <param name="startY">세로 검사 시작 인덱스</param>
    /// <param name="incrementY">세로 검사 증감값 (+1 또는 -1)</param>
    private void Move(Vector2Int direction, int startX, int incrementX, int startY, int incrementY)
    {
        bool changed = false; // 실제로 타일이 움직이거나 합쳐졌는지 여부

        // 이동 방향에 맞춘 순서로 그리드의 모든 칸을 2중 반복문으로 순회합니다.
        for (int x = startX; x >= 0 && x < grid.Width; x += incrementX)
        {
            for (int y = startY; y >= 0 && y < grid.Height; y += incrementY)
            {
                TileCell cell = grid.GetCell(x, y);

                // 칸에 타일이 존재한다면 해당 타일을 목표 방향으로 밀어봅니다.
                if (cell.Occupied) {
                    // changed |= MoveTile(...) : 하나라도 움직였다면 changed가 true가 됩니다.
                    changed |= MoveTile(cell.tile, direction);
                }
            }
        }

        // 최소 하나의 타일이라도 움직이거나 합쳐졌다면, 애니메이션 대기 및 후속 처리 코루틴을 실행합니다.
        if (changed) {
            StartCoroutine(WaitForChanges());
        }
    }

    /// <summary>
    /// 단일 타일을 주어진 방향으로 끝까지 미끄러뜨리거나 인접 타일과 합칩니다.
    /// </summary>
    /// <param name="tile">이동시킬 타일</param>
    /// <param name="direction">이동할 방향</param>
    /// <returns>타일이 실제로 이동했거나 합쳐졌으면 true, 제자리 그대로면 false</returns>
    private bool MoveTile(Tile tile, Vector2Int direction)
    {
        TileCell newCell = null; // 타일이 최종적으로 정착할 빈 칸
        TileCell adjacent = grid.GetAdjacentCell(tile.cell, direction); // 바로 다음 인접 칸

        // 이동 방향으로 한 칸씩 나아가며 어디까지 갈 수 있는지 탐색합니다.
        while (adjacent != null)
        {
            // 인접한 칸에 다른 타일이 이미 있는 경우
            if (adjacent.Occupied)
            {
                // 두 타일의 숫자가 같고 잠겨있지 않다면 합체(Merge)합니다!
                if (CanMerge(tile, adjacent.tile))
                {
                    MergeTiles(tile, adjacent.tile);
                    return true;
                }

                // 숫자가 달라 합칠 수 없다면 더 이상 나아가지 못하고 벽에 막힌 것입니다.
                break;
            }

            // 인접 칸이 비어 있다면, 일단 그 칸까지 갈 수 있는 후보 칸으로 저장합니다.
            newCell = adjacent;
            // 더 먼 다음 칸도 비어있는지 계속해서 전진 탐색합니다.
            adjacent = grid.GetAdjacentCell(adjacent, direction);
        }

        // 이동할 수 있는 빈 칸이 발견되었다면 해당 칸으로 이동시킵니다.
        if (newCell != null)
        {
            tile.MoveTo(newCell);
            return true;
        }

        // 전혀 움직이지 못했다면 false를 반환합니다.
        return false;
    }

    /// <summary>
    /// 두 타일이 서로 합쳐질 수 있는 조건인지 검사합니다.
    /// </summary>
    /// <param name="a">밀려오는 타일</param>
    /// <param name="b">목표 위치에 대기 중인 타일</param>
    /// <returns>숫자가 서로 같고, 목표 타일이 이번 턴에 이미 합쳐진 상태(locked)가 아니면 true</returns>
    private bool CanMerge(Tile a, Tile b)
    {
        return a.state == b.state && !b.locked;
    }

    /// <summary>
    /// 두 타일을 하나로 합치는 처리를 수행합니다.
    /// </summary>
    /// <param name="a">흡수되어 사라질 타일</param>
    /// <param name="b">남아서 숫자가 2배로 커질 타일</param>
    private void MergeTiles(Tile a, Tile b)
    {
        // 1. 사라질 타일 a를 관리 리스트에서 제거하고 합체 애니메이션을 시작합니다.
        tiles.Remove(a);
        a.Merge(b.cell);

        // 2. 타일 b의 다음 단계(예: 2 -> 4, 4 -> 8) 상태 데이터를 찾습니다.
        int index = Mathf.Clamp(IndexOf(b.state) + 1, 0, tileStates.Length - 1);
        TileState newState = tileStates[index];

        // 3. 타일 b에 새로운 숫자와 색상을 적용합니다.
        b.SetState(newState);

        // 4. 합쳐져서 새로 만들어진 타일의 숫자만큼 점수를 올립니다.
        GameManager.Instance.IncreaseScore(newState.number);
    }

    /// <summary>
    /// 특정 TileState가 tileStates 배열에서 몇 번째 인덱스인지 찾습니다.
    /// </summary>
    /// <param name="state">찾고자 하는 타일 상태 데이터</param>
    /// <returns>배열의 인덱스 번호 (없으면 -1)</returns>
    private int IndexOf(TileState state)
    {
        for (int i = 0; i < tileStates.Length; i++)
        {
            if (state == tileStates[i]) {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 타일 이동 애니메이션이 끝날 때까지 기다린 후, 후속 처리(잠금 해제, 새 타일 생성, 게임 오버 확인)를 진행하는 코루틴입니다.
    /// </summary>
    private IEnumerator WaitForChanges()
    {
        waiting = true; // 이동 애니메이션 중 키 입력 차단

        // 타일 이동 시간(0.1초) 동안 기다립니다.
        yield return new WaitForSeconds(0.1f);

        waiting = false; // 입력 차단 해제

        // 모든 타일의 합체 잠금(locked) 플래그를 해제하여 다음 턴에 다시 합쳐질 수 있도록 합니다.
        foreach (var tile in tiles) {
            tile.locked = false;
        }

        // 보드에 빈 칸이 남아있다면 새 타일 하나를 생성합니다.
        if (tiles.Count != grid.Size) {
            CreateTile();
        }

        // 더 이상 움직일 수 없는 상태인지(게임 오버) 확인합니다.
        if (CheckForGameOver()) {
            GameManager.Instance.GameOver();
        }
    }

    /// <summary>
    /// 게임 오버 조건인지 판정합니다.
    /// 모든 칸이 타일로 꽉 찼고, 상하좌우 어떤 방향으로도 합칠 수 있는 인접 타일이 없다면 게임 오버입니다.
    /// </summary>
    /// <returns>게임 오버라면 true, 계속 플레이 가능하면 false</returns>
    public bool CheckForGameOver()
    {
        // 아직 빈 칸이 남아있다면 게임 오버가 아닙니다.
        if (tiles.Count != grid.Size) {
            return false;
        }

        // 모든 칸이 찬 경우, 각 타일마다 상하좌우에 자신과 숫자가 같은(합칠 수 있는) 타일이 있는지 전수 조사합니다.
        foreach (var tile in tiles)
        {
            TileCell up = grid.GetAdjacentCell(tile.cell, Vector2Int.up);
            TileCell down = grid.GetAdjacentCell(tile.cell, Vector2Int.down);
            TileCell left = grid.GetAdjacentCell(tile.cell, Vector2Int.left);
            TileCell right = grid.GetAdjacentCell(tile.cell, Vector2Int.right);

            // 위쪽 타일과 합칠 수 있는 경우
            if (up != null && CanMerge(tile, up.tile)) {
                return false;
            }

            // 아래쪽 타일과 합칠 수 있는 경우
            if (down != null && CanMerge(tile, down.tile)) {
                return false;
            }

            // 왼쪽 타일과 합칠 수 있는 경우
            if (left != null && CanMerge(tile, left.tile)) {
                return false;
            }

            // 오른쪽 타일과 합칠 수 있는 경우
            if (right != null && CanMerge(tile, right.tile)) {
                return false;
            }
        }

        // 보드가 꽉 찼고 합칠 수 있는 타일도 하나도 없다면 게임 오버입니다!
        return true;
    }
}
