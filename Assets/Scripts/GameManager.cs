using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 게임의 전체적인 흐름과 상태(점수, 최고점수, 게임 시작, 게임 오버 등)를 총괄하는 관리자 클래스입니다.
/// 싱글톤(Singleton) 패턴을 사용하여 어디서든 GameManager.Instance를 통해 접근할 수 있습니다.
/// </summary>
[DefaultExecutionOrder(-1)]// 다른 일반 스크립트보다 Awake 먼저 실행 위해 우선순위 높임
public class GameManager : MonoBehaviour
{
    /// <summary>
    /// 전역에서 접근 가능한 유일한 GameManager 인스턴스 (싱글톤)
    /// </summary>
    public static GameManager Instance { get; private set; }

    [Header("참조 컴포넌트")]
    [Tooltip("게임 보드를 제어하는 TileBoard 컴포넌트")]
    [SerializeField] private TileBoard board;

    [Tooltip("게임 오버 시 나타날 UI 화면 (투명도 조절용 CanvasGroup)")]
    [SerializeField] private CanvasGroup gameOver;

    [Tooltip("현재 점수를 표시하는 텍스트 UI")]
    [SerializeField] private TextMeshProUGUI scoreText;

    [Tooltip("최고 점수를 표시하는 텍스트 UI")]
    [SerializeField] private TextMeshProUGUI hiscoreText;

    /// <summary>
    /// 현재 게임의 획득 점수입니다.
    /// </summary>
    public int score { get; private set; } = 0;

    /// <summary>
    /// 싱글톤 패턴 초기화: 씬에 단 하나의 GameManager만 존재하도록 보장합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null) {
            // 이미 다른 GameManager가 존재한다면 중복된 자신을 즉시 제거합니다.
            DestroyImmediate(gameObject);
        } else {
            // 최초 생성된 인스턴스를 static 변수에 보관합니다.
            Instance = this;
        }
    }

    /// <summary>
    /// 게임오브젝트가 파괴될 때 싱글톤 참조를 안전하게 비워줍니다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this) {
            Instance = null;
        }
    }

    /// <summary>
    /// 첫 프레임 업데이트 전에 호출되어 새 게임을 자동으로 시작합니다.
    /// </summary>
    private void Start()
    {
        NewGame();
    }

    /// <summary>
    /// 새 게임을 초기화하고 시작합니다 (재시작 버튼 클릭 시에도 호출 가능).
    /// </summary>
    public void NewGame()
    {
        // 1. 점수를 0으로 리셋하고, 저장된 최고 점수를 불러와 UI에 표시합니다.
        SetScore(0);
        hiscoreText.text = LoadHiscore().ToString();

        // 2. 게임 오버 UI 창을 숨기고 터치/클릭을 비활성화합니다.
        gameOver.alpha = 0f;
        gameOver.interactable = false;

        // 3. 보드를 초기화하고 시작 타일 2개를 생성한 뒤 입력을 활성화합니다.
        board.ClearBoard();
        board.CreateTile();
        board.CreateTile();
        board.enabled = true;
    }

    /// <summary>
    /// 더 이상 타일을 움직일 수 없을 때 호출되는 게임 오버 처리 함수입니다.
    /// </summary>
    public void GameOver()
    {
        // 보드의 타일 조작 입력을 비활성화합니다.
        board.enabled = false;
        // 게임 오버 UI의 버튼 클릭 등을 활성화합니다.
        gameOver.interactable = true;

        // 게임 오버 창이 서서히 나타나도록 페이드 인 애니메이션을 실행합니다 (1초 지연 후 0.5초 동안 서서히 등장).
        StartCoroutine(Fade(gameOver, 1f, 1f));
    }

    /// <summary>
    /// CanvasGroup의 알파(투명도) 값을 서서히 변화시키는 부드러운 페이드(Fade) 코루틴입니다.
    /// </summary>
    /// <param name="canvasGroup">페이드 효과를 줄 대상 UI 그룹</param>
    /// <param name="to">목표 알파 값 (0: 완전 투명, 1: 완전 불투명)</param>
    /// <param name="delay">페이드 시작 전 대기할 지연 시간(초)</param>
    private IEnumerator Fade(CanvasGroup canvasGroup, float to, float delay = 0f)
    {
        // 시작 전 대기 시간이 있다면 기다립니다.
        yield return new WaitForSeconds(delay);

        float elapsed = 0f;
        float duration = 0.5f; // 페이드에 걸리는 시간 (0.5초)
        float from = canvasGroup.alpha; // 시작 투명도

        // duration 동안 매 프레임 선형 보간(Mathf.Lerp)으로 투명도를 부드럽게 바꿉니다.
        while (elapsed < duration)
        {
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 정확한 목표 값으로 설정합니다.
        canvasGroup.alpha = to;
    }

    /// <summary>
    /// 타일이 합쳐졌을 때 점수를 가산하는 함수입니다.
    /// </summary>
    /// <param name="points">추가할 점수 (새로 합쳐진 타일의 숫자)</param>
    public void IncreaseScore(int points)
    {
        SetScore(score + points);
    }

    /// <summary>
    /// 점수를 갱신하고 점수 UI 텍스트를 업데이트하며, 최고 점수 갱신 여부를 체크합니다.
    /// </summary>
    /// <param name="score">설정할 새 점수</param>
    private void SetScore(int score)
    {
        this.score = score;
        scoreText.text = score.ToString();

        SaveHiscore();
    }

    /// <summary>
    /// 현재 점수가 최고 점수보다 높다면 Unity의 PlayerPrefs를 이용해 로컬 저장소에 최고 점수를 저장합니다.
    /// </summary>
    private void SaveHiscore()
    {
        int hiscore = LoadHiscore();

        if (score > hiscore) {
            PlayerPrefs.SetInt("hiscore", score);
        }
    }

    /// <summary>
    /// 기기 저장소(PlayerPrefs)에 저장된 이전 최고 점수를 불러옵니다.
    /// 저장된 값이 없으면 기본값 0을 반환합니다.
    /// </summary>
    /// <returns>불러온 최고 점수</returns>
    private int LoadHiscore()
    {
        return PlayerPrefs.GetInt("hiscore", 0);
    }
}
