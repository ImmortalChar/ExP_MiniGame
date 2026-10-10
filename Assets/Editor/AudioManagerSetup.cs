using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 에디터 상단 메뉴에서 클릭 한 번으로 AudioManager를 씬에 배치하고 오디오 클립을 연결해주는 에디터 도구입니다.
/// </summary>
public static class AudioManagerSetup
{
    [MenuItem("Tools/오디오 매니저 자동 설정 (Setup AudioManager)")]
    public static void Setup()
    {
        // 1. 씬 내의 AudioManager 탐색
        AudioManager audioManager = Object.FindFirstObjectByType<AudioManager>();

        if (audioManager == null)
        {
            // "Game Manager" 오브젝트가 있다면 거기에 추가, 없으면 새 게임오브젝트 생성
            GameObject gmObj = GameObject.Find("Game Manager");
            if (gmObj != null)
            {
                audioManager = gmObj.AddComponent<AudioManager>();
                Debug.Log("[AudioManagerSetup] 'Game Manager' 오브젝트에 AudioManager 컴포넌트를 추가했습니다.");
            }
            else
            {
                GameObject newObj = new GameObject("AudioManager");
                audioManager = newObj.AddComponent<AudioManager>();
                Undo.RegisterCreatedObjectUndo(newObj, "Create AudioManager");
                Debug.Log("[AudioManagerSetup] 새로운 'AudioManager' 오브젝트를 생성했습니다.");
            }
        }

        // 2. AudioSource 2개 확인 및 추가
        AudioSource[] sources = audioManager.GetComponents<AudioSource>();
        AudioSource bgmSource = sources.Length > 0 ? sources[0] : audioManager.gameObject.AddComponent<AudioSource>();
        AudioSource sfxSource = sources.Length > 1 ? sources[1] : audioManager.gameObject.AddComponent<AudioSource>();

        // BGM 소스 기본 설정
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f; // 2D 사운드
        bgmSource.volume = 0.5f;

        // SFX 소스 기본 설정
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f; // 2D 사운드
        sfxSource.volume = 1.0f;

        // 3. 오디오 클립 로드 (BGM: Assets/Audio/bgm, SFX: Assets/Audio/keyboardInput)
        string[] bgmGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/bgm" });
        List<AudioClip> bgmList = new List<AudioClip>();
        foreach (string guid in bgmGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) bgmList.Add(clip);
        }

        string[] sfxGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/keyboardInput" });
        List<AudioClip> sfxList = new List<AudioClip>();
        foreach (string guid in sfxGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) sfxList.Add(clip);
        }

        // 4. SerializedObject를 통해 AudioManager 필드에 값 반영
        SerializedObject so = new SerializedObject(audioManager);
        so.Update();

        SerializedProperty propBgmSource = so.FindProperty("bgmSource");
        if (propBgmSource != null) propBgmSource.objectReferenceValue = bgmSource;

        SerializedProperty propSfxSource = so.FindProperty("sfxSource");
        if (propSfxSource != null) propSfxSource.objectReferenceValue = sfxSource;

        SerializedProperty propBgmClips = so.FindProperty("bgmClips");
        if (propBgmClips != null)
        {
            propBgmClips.arraySize = bgmList.Count;
            for (int i = 0; i < bgmList.Count; i++)
            {
                propBgmClips.GetArrayElementAtIndex(i).objectReferenceValue = bgmList[i];
            }
        }

        SerializedProperty propKeyboardClips = so.FindProperty("keyboardClips");
        if (propKeyboardClips != null)
        {
            propKeyboardClips.arraySize = sfxList.Count;
            for (int i = 0; i < sfxList.Count; i++)
            {
                propKeyboardClips.GetArrayElementAtIndex(i).objectReferenceValue = sfxList[i];
            }
        }

        so.ApplyModifiedProperties();

        // 5. 씬 변경사항 저장 마크
        EditorUtility.SetDirty(audioManager);
        EditorSceneManager.MarkSceneDirty(audioManager.gameObject.scene);

        string message = $"AudioManager 설정 완료!\n- BGM 클립: {bgmList.Count}개 등록\n- 키보드 효과음: {sfxList.Count}개 등록\n- 씬 변경사항이 반영되었습니다. 씬을 저장(Ctrl+S)해주세요!";
        Debug.Log($"[AudioManagerSetup] {message}");
        EditorUtility.DisplayDialog("AudioManager 설정 완료", message, "확인");
    }
}
