using UnityEngine;

/// <summary>
/// 각 타일의 숫자와 시각적 스타일(배경색, 글자색) 정보를 저장하는 ScriptableObject 데이터 클래스입니다.
/// 프로젝트 창에서 우클릭 -> Create -> Tile State 메뉴를 통해 에셋 형태로 생성할 수 있습니다.
/// (예: 숫자 2용 TileState 에셋, 숫자 4용 TileState 에셋 등)
/// </summary>
[CreateAssetMenu(menuName = "Tile State")]
public class TileState : ScriptableObject
{
    [Tooltip("타일에 표시될 숫자 (예: 2, 4, 8, 16, ... 2048)")]
    public int number;

    [Tooltip("타일의 배경 색상")]
    public Color backgroundColor;

    [Tooltip("타일 위 숫자의 텍스트 색상")]
    public Color textColor;
}
