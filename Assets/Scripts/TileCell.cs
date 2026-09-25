using UnityEngine;

/// <summary>
/// 그리드(격자판)의 개별 칸(셀) 하나를 나타내는 클래스입니다.
/// 자신의 격자 좌표(x, y)와 현재 이 칸에 위치한 타일(Tile) 정보를 보관합니다.
/// </summary>
public class TileCell : MonoBehaviour
{
    /// <summary>
    /// 그리드 상에서의 2차원 좌표값입니다 (예: x=0, y=0 은 첫 번째 칸).
    /// </summary>
    public Vector2Int coordinates { get; set; }

    /// <summary>
    /// 현재 이 칸에 놓여 있는 타일 객체입니다. (칸이 비어 있으면 null)
    /// </summary>
    public Tile tile { get; set; }

    /// <summary>
    /// 현재 칸이 비어 있는지 여부를 반환합니다.
    /// 타일이 없으면(tile == null) true를 반환합니다.
    /// </summary>
    public bool Empty => tile == null;

    /// <summary>
    /// 현재 칸에 타일이 채워져 있는지 여부를 반환합니다.
    /// 타일이 존재하면(tile != null) true를 반환합니다.
    /// </summary>
    public bool Occupied => tile != null;
}
