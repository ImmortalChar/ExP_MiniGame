using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 배경음악(BGM)과 효과음(SFX)을 총괄 관리하는 오디오 매니저 클래스입니다.
/// 싱글톤(Singleton) 패턴으로 어디서든 AudioManager.Instance를 통해 접근할 수 있습니다.
/// </summary>
[DefaultExecutionOrder(-2)] // GameManager(-1)보다 먼저 Awake가 호출되도록 설정
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("오디오 소스 (비어있으면 자동 생성)")]
    [Tooltip("배경음악(BGM) 재생용 AudioSource 컴포넌트")]
    [SerializeField] private AudioSource bgmSource;

    [Tooltip("효과음(SFX) 재생용 AudioSource 컴포넌트")]
    [SerializeField] private AudioSource sfxSource;

    [Header("오디오 클립 목록")]
    [Tooltip("배경음악으로 사용할 오디오 클립 배열 (Assets/Audio/bgm)")]
    [SerializeField] private AudioClip[] bgmClips;

    [Tooltip("타일 결합 및 버튼 클릭 시 재생할 키보드 효과음 배열 (Assets/Audio/keyboardInput)")]
    [SerializeField] private AudioClip[] keyboardClips;

    [Header("볼륨 설정")]
    [Range(0f, 1f)]
    [Tooltip("BGM 볼륨")]
    [SerializeField] private float bgmVolume = 0.5f;

    [Range(0f, 1f)]
    [Tooltip("SFX 볼륨")]
    [SerializeField] private float sfxVolume = 1f;

    [Header("옵션")]
    [Tooltip("씬에 존재하는 모든 Button 컴포넌트에 클릭 효과음 리스너를 자동으로 등록할지 여부")]
    [SerializeField] private bool autoBindButtonClicks = true;

    // 직전에 재생된 BGM 인덱스 (같은 곡이 연속으로 나오는 것 방지)
    private int lastBgmIndex = -1;

    // 이미 리스너가 등록된 버튼 목록 (중복 등록 방지)
    private readonly HashSet<Button> registeredButtons = new HashSet<Button>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // AudioSource 컴포넌트가 없으면 자동으로 생성 및 2D 사운드로 설정
        EnsureAudioSources();

#if UNITY_EDITOR
        // 에디터에서 재생 시 클립 목록이 비어있다면 에셋 폴더에서 자동 로드
        if ((bgmClips == null || bgmClips.Length == 0) || (keyboardClips == null || keyboardClips.Length == 0))
        {
            AutoLoadAudioClips();
        }
#endif
    }

    private void Start()
    {
        // 씬 내의 모든 버튼에 클릭 사운드 자동 바인딩
        if (autoBindButtonClicks)
        {
            RegisterAllButtons();
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
    /// AudioSource 컴포넌트의 존재를 확인하고, 없으면 생성하며 2D 설정(spatialBlend = 0)을 적용합니다.
    /// </summary>
    private void EnsureAudioSources()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.spatialBlend = 0f; // 2D 사운드 보장
        }
        bgmSource.volume = bgmVolume;

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f; // 2D 사운드 보장
        }
        sfxSource.volume = sfxVolume;
    }

    /// <summary>
    /// BGM 목록 중 하나를 무작위로 선택하여 루프 재생합니다.
    /// 곡이 2곡 이상이면 직전에 재생된 곡과 겹치지 않도록 선택합니다.
    /// </summary>
    public void PlayRandomBGM()
    {
        if (bgmClips == null || bgmClips.Length == 0)
        {
            Debug.LogWarning("[AudioManager] 재생할 BGM 클립이 없습니다.");
            return;
        }

        EnsureAudioSources();

        int selectedIndex = 0;
        if (bgmClips.Length > 1)
        {
            // 직전 곡과 다른 곡이 나올 때까지 난수 생성
            do
            {
                selectedIndex = Random.Range(0, bgmClips.Length);
            } while (selectedIndex == lastBgmIndex);
        }

        lastBgmIndex = selectedIndex;
        AudioClip clipToPlay = bgmClips[selectedIndex];

        bgmSource.clip = clipToPlay;
        bgmSource.loop = true;
        bgmSource.volume = bgmVolume;
        bgmSource.Play();
    }

    /// <summary>
    /// 재생 중인 배경음악(BGM)을 정지합니다.
    /// </summary>
    public void StopBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Stop();
        }
    }

    /// <summary>
    /// keyboardInput 목록의 효과음 중 하나를 무작위로 선택하여 1회 재생합니다 (반복 X).
    /// PlayOneShot을 사용하므로 소리가 겹쳐도 자연스럽게 재생됩니다.
    /// </summary>
    public void PlayRandomKeyboardSFX()
    {
        if (keyboardClips == null || keyboardClips.Length == 0)
        {
            Debug.LogWarning("[AudioManager] 재생할 키보드 효과음 클립이 없습니다.");
            return;
        }

        EnsureAudioSources();

        int randomIndex = Random.Range(0, keyboardClips.Length);
        AudioClip clipToPlay = keyboardClips[randomIndex];

        sfxSource.PlayOneShot(clipToPlay, sfxVolume);
    }

    /// <summary>
    /// 씬 내의 모든 Button 컴포넌트(비활성화된 오브젝트 포함)를 찾아 OnClick 시 키보드 효과음이 나도록 등록합니다.
    /// </summary>
    public void RegisterAllButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var button in buttons)
        {
            RegisterButton(button);
        }
    }

    /// <summary>
    /// 개별 Button 컴포넌트에 키보드 효과음 OnClick 리스너를 등록합니다.
    /// 이미 등록된 버튼은 중복 등록되지 않습니다.
    /// </summary>
    /// <param name="button">등록할 Button 컴포넌트</param>
    public void RegisterButton(Button button)
    {
        if (button != null && registeredButtons.Add(button))
        {
            button.onClick.AddListener(PlayRandomKeyboardSFX);
        }
    }

    /// <summary>
    /// BGM 볼륨을 동적으로 조절합니다.
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (bgmSource != null)
        {
            bgmSource.volume = bgmVolume;
        }
    }

    /// <summary>
    /// SFX 볼륨을 동적으로 조절합니다.
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터 환경에서 Assets/Audio/bgm 및 Assets/Audio/keyboardInput 폴더의 오디오 파일을 자동으로 배열에 채워줍니다.
    /// </summary>
    [ContextMenu("오디오 클립 자동 로드 (Auto Load)")]
    public void AutoLoadAudioClips()
    {
        // 1. Assets/Audio/bgm 내의 오디오 파일 검색
        string[] bgmGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/bgm" });
        List<AudioClip> loadedBgm = new List<AudioClip>();
        foreach (string guid in bgmGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) loadedBgm.Add(clip);
        }
        bgmClips = loadedBgm.ToArray();

        // 2. Assets/Audio/keyboardInput 내의 오디오 파일 검색
        string[] sfxGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/keyboardInput" });
        List<AudioClip> loadedSfx = new List<AudioClip>();
        foreach (string guid in sfxGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) loadedSfx.Add(clip);
        }
        keyboardClips = loadedSfx.ToArray();

        EditorUtility.SetDirty(this);
        Debug.Log($"[AudioManager] 오디오 클립 로드 완료! BGM: {bgmClips.Length}개, 키보드 SFX: {keyboardClips.Length}개");
    }
#endif
}
