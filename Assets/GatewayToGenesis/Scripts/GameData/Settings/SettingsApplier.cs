using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Pushes <see cref="GameSettings"/> into Unity when they change and when the game starts: the window (mode,
/// resolution; not in the Editor), VSync and the frame cap, the listener's volume (silent in the background when asked),
/// running in the background, and brightness (a global URP volume's exposure over the scene's cameras; the menus and
/// HUD on overlay canvases are not touched). Lives on the persistent menu object (<see cref="SaveMenu"/>).
/// </summary>
public class SettingsApplier : MonoBehaviour
{
    private Volume _volume;
    private VolumeProfile _profile;
    private ColorAdjustments _adjust;
    private bool _focused = true;
    private float _camerasAt;

    private void Start()
    {
        GameSettings.Changed += OnChanged;
        ApplyDisplay();
        ApplyAudio();
        ApplyGeneral();
    }

    private void OnDestroy()
    {
        GameSettings.Changed -= OnChanged;
        if (_profile != null) Destroy(_profile);
    }

    private void OnChanged(string section)
    {
        if (section == "display") ApplyDisplay();
        else if (section == "audio") ApplyAudio();
        else if (section == "general") ApplyGeneral();
    }

    private void OnApplicationFocus(bool focused)
    {
        _focused = focused;
        ApplyAudio();
    }

    private void ApplyDisplay()
    {
        // The Editor's game view and batch runs keep their own window.
        if (!Application.isEditor && !Application.isBatchMode)
        {
            var mode = GameSettings.Window == WindowMode.Fullscreen ? FullScreenMode.ExclusiveFullScreen
                : GameSettings.Window == WindowMode.Borderless ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            var size = GameSettings.Resolution;
            if (size.x <= 0 || size.y <= 0) size = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
            if (Screen.width != size.x || Screen.height != size.y || Screen.fullScreenMode != mode) Screen.SetResolution(size.x, size.y, mode);
        }
        QualitySettings.vSyncCount = GameSettings.VSync ? 1 : 0;
        Application.targetFrameRate = GameSettings.VSync || GameSettings.FrameCap <= 0 ? -1 : GameSettings.FrameCap;
        ApplyBrightness();
    }

    private void ApplyAudio() => AudioListener.volume = _focused || !GameSettings.MuteInBackground ? GameSettings.MasterVolume : 0f;

    private static void ApplyGeneral() => Application.runInBackground = !GameSettings.PauseInBackground;

    private void ApplyBrightness()
    {
        float brightness = GameSettings.Brightness;
        if (_volume == null)
        {
            if (Mathf.Approximately(brightness, 0f)) return;
            var go = new GameObject("Brightness");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 1000f;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _adjust = _profile.Add<ColorAdjustments>(true);
            _volume.sharedProfile = _profile;
        }
        // One stop and a half either way at the slider's ends.
        _adjust.postExposure.Override(brightness * 1.5f);
        _volume.enabled = !Mathf.Approximately(brightness, 0f);
        _camerasAt = 0f;
    }

    private void Update()
    {
        // Cameras come and go with scenes and the world map: those that draw the scene render post-processing while
        // the brightness is moved off its default.
        if (_volume == null || !_volume.enabled || Time.unscaledTime < _camerasAt) return;
        _camerasAt = Time.unscaledTime + 1f;
        foreach (var cam in Camera.allCameras)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null && data.renderType == CameraRenderType.Base && !data.renderPostProcessing) data.renderPostProcessing = true;
        }
    }
}
