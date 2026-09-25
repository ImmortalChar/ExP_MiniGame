using UnityEngine;

/// <summary>
/// 그리드(격자)의 가로 한 줄(행)을 관리하는 클래스입니다.
/// 자식 오브젝트로 붙어 있는 TileCell 컴포넌트들을 자동으로 감지하여 배열에 보관합니다.
/// </summary>
public class TileRow : MonoBehaviour
{
    /// <summary>
    /// 이 가로줄(행)에 속한 칸(TileCell)들의 배열입니다.
    /// 외부에서는 읽기만 가능합니다.
    /// </summary>
    public TileCell[] cells { get; private set; }

    /// <summary>
    /// 오브젝트가 활성화될 때 가장 먼저 실행되는 유니티 생명주기 함수입니다.
    /// 자식 오브젝트들에 있는 모든 TileCell 컴포넌트를 찾아서 cells 배열에 할당합니다.
    /// </summary>
    private void Awake()
    {
        // 이 오브젝트의 하위(자식) 계층에 있는 모든 TileCell 컴포넌트를 찾아 배열로 저장합니다.
        cells = GetComponentsInChildren<TileCell>();
    }
}
