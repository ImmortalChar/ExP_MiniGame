using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면에 보여지는 실제 개별 타일 오브젝트를 제어하는 클래스입니다.
/// 타일의 숫자, 배경색, 애니메이션(이동 및 합체), 소속된 셀(TileCell)과의 연결 등을 관리합니다.
/// </summary>
public class Tile : MonoBehaviour
{
    /// <summary>
    /// 현재 타일의 상태 정보(숫자, 배경 색상, 텍스트 색상)입니다.
    /// </summary>
    public TileState state { get; private set; }

    /// <summary>
    /// 현재 이 타일이 위치하고 있는 격자 칸(TileCell)입니다.
    /// </summary>
    public TileCell cell { get; private set; }

    /// <summary>
    /// 한 번의 이동(턴) 중에 이미 다른 타일과 합쳐졌는지 여부를 나타냅니다.
    /// 2048 규칙상 한 턴에 같은 타일이 연속으로 2번 합쳐지는 것을 방지하기 위해 잠금(lock)을 겁니다.
    /// (예: 2 + 2 + 4를 왼쪽으로 밀었을 때 한 번에 8이 되지 않고 4와 4가 되도록 방지)
    /// </summary>
    public bool locked { get; set; }

    /// <summary>
    /// 타일의 배경 색상을 칠하기 위한 UI Image 컴포넌트입니다.
    /// </summary>
    private Image background;

    /// <summary>
    /// 타일 중앙에 숫자를 표시하기 위한 TextMeshPro UI 컴포넌트입니다.
    /// </summary>
    private TextMeshProUGUI text;

    /// <summary>
    /// 오브젝트가 생성될 때 UI 컴포넌트 참조들을 초기화합니다.
    /// </summary>
    private void Awake()
    {
        // 자신의 Image 컴포넌트를 가져옵니다.
        background = GetComponent<Image>();
        // 자식 오브젝트에 있는 TextMeshProUGUI 컴포넌트를 가져옵니다.
        text = GetComponentInChildren<TextMeshProUGUI>();
    }

    /// <summary>
    /// 타일의 상태(숫자 및 색상)를 갱신합니다.
    /// 타일이 처음 생길 때나 다른 타일과 합쳐져 숫자가 커질 때 호출됩니다.
    /// </summary>
    /// <param name="state">새로 적용할 타일 상태 데이터(ScriptableObject)</param>
    public void SetState(TileState state)
    {
        this.state = state;

        // ScriptableObject에 정의된 색상과 숫자를 UI에 반영합니다.
        background.color = state.backgroundColor;
        text.color = state.textColor;
        text.text = state.number.ToString();
    }

    /// <summary>
    /// 특정 셀(TileCell)에 타일을 처음 배치(스폰)합니다.
    /// 이동 애니메이션 없이 해당 셀의 위치로 즉시 순간 이동합니다.
    /// </summary>
    /// <param name="cell">타일을 배치할 목표 칸</param>
    public void Spawn(TileCell cell)
    {
        // 이전에 연결되어 있던 셀이 있다면 관계를 끊어줍니다.
        if (this.cell != null) {
            this.cell.tile = null;
        }

        // 새로운 셀과 상호 참조를 연결합니다.
        this.cell = cell;
        this.cell.tile = this;

        // 타일의 실제 월드/UI 위치를 해당 셀의 위치와 같게 맞춥니다.
        transform.position = cell.transform.position;
    }

    /// <summary>
    /// 타일을 비어 있는 다른 셀(TileCell)로 부드럽게 이동시킵니다.
    /// </summary>
    /// <param name="cell">이동할 목표 칸</param>
    public void MoveTo(TileCell cell)
    {
        // 기존 칸과의 연결을 해제합니다.
        if (this.cell != null) {
            this.cell.tile = null;
        }

        // 새로운 칸과 연결합니다.
        this.cell = cell;
        this.cell.tile = this;

        // 목표 위치로 부드럽게 미끄러지듯 이동하는 코루틴을 시작합니다 (merging = false).
        StartCoroutine(Animate(cell.transform.position, false));
    }

    /// <summary>
    /// 다른 타일과 합쳐질 때 호출되는 함수입니다.
    /// 상대방 타일 위치로 이동한 뒤, 이동이 끝나면 이 타일 오브젝트는 파괴(Destroy)됩니다.
    /// </summary>
    /// <param name="cell">합쳐질 대상 타일이 위치한 칸</param>
    public void Merge(TileCell cell)
    {
        // 기존 칸과의 연결을 끊습니다.
        if (this.cell != null) {
            this.cell.tile = null;
        }

        this.cell = null;
        // 합쳐진 상대방 타일은 이번 턴에 더 이상 합쳐지지 않도록 잠금(locked) 처리합니다.
        cell.tile.locked = true;

        // 목표 타일 위치로 이동한 후 스스로 파괴되는 코루틴을 실행합니다 (merging = true).
        StartCoroutine(Animate(cell.transform.position, true));
    }

    /// <summary>
    /// 타일을 현재 위치에서 목표 위치(to)까지 부드럽게 이동시키는 애니메이션 코루틴입니다.
    /// </summary>
    /// <param name="to">이동할 최종 목적지 위치</param>
    /// <param name="merging">합체 여부 (true인 경우 이동 완료 후 해당 타일 게임오브젝트를 파괴함)</param>
    private IEnumerator Animate(Vector3 to, bool merging)
    {
        float elapsed = 0f;      // 이동 경과 시간
        float duration = 0.1f;    // 전체 이동에 걸리는 시간 (0.1초 동안 빠르게 슬라이드)

        Vector3 from = transform.position; // 출발 위치

        // 0.1초 동안 매 프레임 선형 보간(Lerp)을 사용하여 부드럽게 위치를 이동합니다.
        while (elapsed < duration)
        {
            // Vector3.Lerp(출발점, 도착점, 진행비율 0~1): 두 지점 사이의 중간 좌표를 계산
            transform.position = Vector3.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime; // 지난 프레임 시간을 누적
            yield return null;         // 다음 프레임까지 대기
        }

        // 오차를 없애기 위해 마지막에 정확한 도착점 좌표로 보정합니다.
        transform.position = to;

        // 다른 타일 속으로 합쳐져 들어간 경우, 역할이 끝났으므로 이 타일 오브젝트를 삭제합니다.
        if (merging) {
            Destroy(gameObject);
        }
    }
}
