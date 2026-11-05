using UnityEngine;
using TMPro;

/// <summary>
/// 스크립트명: Timer
/// 기능: 60초의 게임시간을 부여하며 0초가 되면 게임 오버 판정을 내림.
/// 게임오버는 GameManager 스크립트의 GameOver() 함수를 호출하여 바로 사용
/// (GameManager.Instance.GameOver())
/// public 함수 목록:
/// AddTime(float t): 시간을 더함
/// GameStart(): 게임 시작, 즉 60초로 재설정 > GameManager 스크립트의 NewGame 함수에 포함
/// </summary>
public class Timer : MonoBehaviour
{
    /// <summary>
    /// 어디서든 쉽게 접근할 수 있도록 싱글톤 인스턴스 제공
    /// </summary>
    public static Timer Instance { get; private set; }

    [Header("시간 설정")]
    [Tooltip("게임 시작 시 주어지는 기본 제한 시간 (초)")]
    [SerializeField] private float initialTime = 60f;

    [Header("UI 연결 ")]
    [Tooltip("남은 시간을 표시할 TextMeshPro UI (연결하지 않아도 정상 작동)")]
    [SerializeField] private TextMeshProUGUI timerText;

    /// <summary>
    /// 현재 남은 시간 (초)
    /// </summary>
    public float CurrentTime { get; private set; }

    /// <summary>
    /// 타이머 동작 여부 플래그
    /// </summary>
    public bool IsRunning { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 첫 시작 시 타이머 초기화 및 실행
        GameStart();
    }

    private void Update()
    {
        if (!IsRunning) return;

        // 매 프레임 시간 감소
        CurrentTime -= Time.deltaTime;

        // 0초 이하가 되면 게임 오버 처리
        if (CurrentTime <= 0f)
        {
            CurrentTime = 0f;
            IsRunning = false;
            UpdateTimerUI();

            // GameManager의 GameOver() 함수 호출
            if (GameManager.Instance != null)
            {
                GameManager.Instance.GameOver();
            }
            return;
        }

        // 실시간 UI 갱신
        UpdateTimerUI();
    }

    /// <summary>
    /// 시간을 추가합니다 (예: 타일 합체 보너스 시간 부여 등).
    /// </summary>
    /// <param name="t">추가할 시간(초)</param>
    public void AddTime(float t)
    {
        if (!IsRunning) return;

        CurrentTime += t;
        UpdateTimerUI();
    }

    /// <summary>
    /// 게임 시작 및 새 게임 시 60초로 재설정하고 타이머를 가동합니다.
    /// GameManager의 NewGame() 함수에서 호출됩니다.
    /// </summary>
    public void GameStart()
    {
        CurrentTime = initialTime;
        IsRunning = true;
        UpdateTimerUI();
    }

    /// <summary>
    /// 타이머를 멈춥니다 (게임 오버 시 등).
    /// </summary>
    public void StopTimer()
    {
        IsRunning = false;
    }

    /// <summary>
    /// 남은 시간을 UI 텍스트에 갱신합니다.
    /// timerText가 연결되어 있을 때만 실행됩니다.
    /// </summary>
    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            // 남은 시간을 소수점 첫째 자리까지 표시 (예: 60.0, 59.4...)
            timerText.text = CurrentTime.ToString("F1");
        }
    }
}
