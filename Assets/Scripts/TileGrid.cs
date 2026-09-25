using UnityEngine;

/// <summary>
/// 2048 게임의 전체 격자판(그리드) 구조를 관리하는 클래스입니다.
/// 모든 행(TileRow)과 칸(TileCell)들을 탐색하고, 좌표 기반 조회 및 빈 칸 찾기 기능을 제공합니다.
/// </summary>
public class TileGrid : MonoBehaviour
{
    /// <summary>
    /// 그리드를 구성하는 가로줄(행, TileRow)들의 배열입니다.
    /// </summary>
    public TileRow[] rows { get; private set; }

    /// <summary>
    /// 그리드에 포함된 모든 칸(TileCell)들의 1차원 배열입니다.
    /// </summary>
    public TileCell[] cells { get; private set; }

    /// <summary>
    /// 전체 칸의 총 개수입니다 (예: 4x4 그리드의 경우 16).
    /// </summary>
    public int Size => cells.Length;

    /// <summary>
    /// 그리드의 세로 행(Row) 개수(높이)입니다 (기본 4).
    /// </summary>
    public int Height => rows.Length;

    /// <summary>
    /// 그리드의 가로 열 개수(너비)입니다.
    /// 전체 셀 개수를 세로 높이로 나누어 계산합니다 (기본 16 / 4 = 4).
    /// </summary>
    public int Width => Size / Height;

    /// <summary>
    /// 게임 오브젝트가 생성될 때 실행되어 그리드를 초기화합니다.
    /// 자식 컴포넌트들을 찾고 각 셀에 (x, y) 2차원 좌표를 부여합니다.
    /// </summary>
    private void Awake()
    {
        // 자식 오브젝트들에서 모든 Row와 Cell 컴포넌트를 가져옵니다.
        rows = GetComponentsInChildren<TileRow>();
        cells = GetComponentsInChildren<TileCell>();

        // 1차원 배열로 나열된 각 셀에 2차원 좌표(x, y)를 순서대로 계산하여 부여합니다.
        // 예: 4x4 그리드에서 인덱스 0 -> (0, 0), 인덱스 1 -> (1, 0), 인덱스 4 -> (0, 1)
        for (int i = 0; i < cells.Length; i++) {
            cells[i].coordinates = new Vector2Int(i % Width, i / Width);
        }
    }

    /// <summary>
    /// Vector2Int 형식의 좌표를 받아 해당하는 칸(TileCell)을 반환하는 함수입니다.
    /// </summary>
    /// <param name="coordinates">찾고자 하는 셀의 (x, y) 좌표</param>
    /// <returns>해당 좌표의 TileCell 객체 (범위 밖이면 null)</returns>
    public TileCell GetCell(Vector2Int coordinates)
    {
        return GetCell(coordinates.x, coordinates.y);
    }

    /// <summary>
    /// x, y 정수 좌표를 받아 유효 범위를 검사한 후 해당하는 칸(TileCell)을 반환합니다.
    /// </summary>
    /// <param name="x">가로 열 인덱스 (0 ~ Width-1)</param>
    /// <param name="y">세로 행 인덱스 (0 ~ Height-1)</param>
    /// <returns>해당 위치의 TileCell, 유효 범위를 벗어나면 null</returns>
    public TileCell GetCell(int x, int y)
    {
        // 좌표가 그리드 유효 범위 내에 있는지 확인합니다.
        if (x >= 0 && x < Width && y >= 0 && y < Height) {
            return rows[y].cells[x];
        } else {
            return null;
        }
    }

    /// <summary>
    /// 기준 칸(cell)에서 특정 방향(direction)으로 한 칸 이동했을 때의 인접한 셀을 찾습니다.
    /// </summary>
    /// <param name="cell">기준이 되는 현재 칸</param>
    /// <param name="direction">이동하려는 방향 (Vector2Int.up, down, left, right 등)</param>
    /// <returns>인접한 TileCell (그리드 경계를 벗어나면 null)</returns>
    public TileCell GetAdjacentCell(TileCell cell, Vector2Int direction)
    {
        Vector2Int coordinates = cell.coordinates;
        coordinates.x += direction.x;
        // UI 좌표계에서는 위쪽 행이 인덱스 0번부터 시작하므로, 
        // 위쪽(Vector2Int.up, y=+1)으로 갈수록 행 인덱스가 감소하도록 direction.y를 뺍니다.
        coordinates.y -= direction.y;

        return GetCell(coordinates);
    }

    /// <summary>
    /// 현재 비어 있는 칸 중 무작위로 하나를 골라 반환합니다.
    /// 새 타일을 생성할 때 사용됩니다.
    /// </summary>
    /// <returns>비어 있는 TileCell (모든 칸이 꽉 차 있으면 null)</returns>
    public TileCell GetRandomEmptyCell()
    {
        // 무작위 시작 인덱스를 선정합니다.
        int index = Random.Range(0, cells.Length);
        int startingIndex = index;

        // 선택한 칸에 이미 타일이 있다면(Occupied), 빈 칸을 만날 때까지 다음 칸으로 이동합니다.
        while (cells[index].Occupied)
        {
            index++;

            // 배열의 끝에 도달하면 다시 0번 인덱스(처음)로 순환합니다.
            if (index >= cells.Length) {
                index = 0;
            }

            // 한 바퀴를 완전히 돌아 원래 시작 지점으로 돌아왔다면, 모든 칸이 꽉 찬 상태입니다.
            if (index == startingIndex) {
                return null;
            }
        }

        return cells[index];
    }
}
