using UnityEngine;

[DisallowMultipleComponent]
public class ApplyPlayerSettings : MonoBehaviour
{
    // ---- Gameplay keys ----
    private const string FOV_KEY       = "player_fov";
    private const string SENS_KEY      = "player_sensitivity";

    // ---- Graphics keys (must match MainMenuUI) ----
    private const string QUALITY_KEY    = "gfx_quality";
    private const string FULLSCREEN_KEY = "gfx_fullscreen";
    private const string RES_INDEX_KEY  = "gfx_resolution_index";
    private const string VSYNC_KEY      = "gfx_vsync";

    [Header("Gameplay")]
    [Tooltip("Camera to apply FOV to. If left empty, will try to get Camera on this object/children, then Camera.main.")]
    public Camera targetCamera;

    [Tooltip("Look / controller script to push sensitivity into (e.g. FirstPersonController).")]
    public MonoBehaviour lookScript;

    private void Start()
    {
        ApplyFov();
        ApplySensitivity();
        ApplyGraphicsSettings();
    }

    // ================== FOV ==================
    private void ApplyFov()
    {
        float savedFOV = PlayerPrefs.GetFloat(FOV_KEY, 90f);

        Camera cam = targetCamera ? targetCamera : GetComponent<Camera>();
        if (!cam)
            cam = GetComponentInChildren<Camera>();
        if (!cam)
            cam = Camera.main;

        if (cam)
        {
            cam.fieldOfView = savedFOV;
            Debug.Log($"🎥 Applied saved FOV: {savedFOV}");
        }
        else
        {
            Debug.LogWarning("ApplyPlayerSettings: No camera found to apply FOV.");
        }
    }

    // ================== SENSITIVITY ==================
    private void ApplySensitivity()
    {
        float savedSens = PlayerPrefs.GetFloat(SENS_KEY, 1f);

        // 1) if something is dragged in via Inspector
        if (lookScript)
        {
            TrySetSensitivityOn(lookScript, savedSens);
            return;
        }

        // 2) try this object
        FirstPersonController fpc = GetComponent<FirstPersonController>();
        if (!fpc)
        {
            // 3) last resort: search scene
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
            fpc = FindFirstObjectByType<FirstPersonController>();
#else
            fpc = FindObjectOfType<FirstPersonController>();
#endif
        }

        if (fpc)
        {
            fpc.SetSensitivity(savedSens, false);
            Debug.Log($"🖱 Applied saved Sensitivity to FirstPersonController: {savedSens}");
        }
        else
        {
            Debug.Log($"🖱 Loaded sensitivity = {savedSens}, but no FirstPersonController was found in the scene.");
        }
    }

    private void TrySetSensitivityOn(MonoBehaviour mb, float value)
    {
        var fpc = mb as FirstPersonController;
        if (fpc != null)
        {
            fpc.SetSensitivity(value, false);
            Debug.Log($"🖱 Applied saved Sensitivity to {nameof(FirstPersonController)}: {value}");
            return;
        }

        Debug.Log($"🖱 Saved sensitivity = {value} (drag a script that has a SetSensitivity method to apply).");
    }

    // ================== GRAPHICS ==================
    private void ApplyGraphicsSettings()
    {
        ApplyQualityFromPrefs();
        ApplyVSyncFromPrefs();
        ApplyFullscreenFromPrefs();
        ApplyResolutionFromPrefs();
    }

    private void ApplyQualityFromPrefs()
    {
        int currentQuality = QualitySettings.GetQualityLevel();
        int savedQuality = PlayerPrefs.GetInt(QUALITY_KEY, currentQuality);

        savedQuality = Mathf.Clamp(savedQuality, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(savedQuality, true);

        Debug.Log($"🎨 Quality level applied: {QualitySettings.names[savedQuality]} (index {savedQuality})");
    }

    private void ApplyFullscreenFromPrefs()
    {
        // default to whatever the app is currently doing
        bool defaultFullscreen = Screen.fullScreen;
        bool fullscreen = PlayerPrefs.GetInt(FULLSCREEN_KEY, defaultFullscreen ? 1 : 0) == 1;

        Screen.fullScreen = fullscreen;
        // Optional: if you want to force a mode:
        // Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        Debug.Log($"🖥 Fullscreen applied: {fullscreen}");
    }

    private void ApplyResolutionFromPrefs()
    {
        Resolution[] resolutions = Screen.resolutions;
        if (resolutions == null || resolutions.Length == 0)
            return;

        // default: find current resolution index
        Resolution current = Screen.currentResolution;
        int defaultIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width  == current.width &&
                resolutions[i].height == current.height &&
                Mathf.Approximately((float)resolutions[i].refreshRateRatio.value,
                                    (float)current.refreshRateRatio.value))
            {
                defaultIndex = i;
                break;
            }
        }

        int savedIndex = PlayerPrefs.GetInt(RES_INDEX_KEY, defaultIndex);
        savedIndex = Mathf.Clamp(savedIndex, 0, resolutions.Length - 1);

        Resolution r = resolutions[savedIndex];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode, r.refreshRateRatio);

        Debug.Log($"📺 Resolution applied: {r.width}x{r.height} @ {r.refreshRateRatio.value:0}Hz (index {savedIndex})");
    }

    private void ApplyVSyncFromPrefs()
    {
        int defaultVsync = QualitySettings.vSyncCount > 0 ? 1 : 0;
        bool vsyncOn = PlayerPrefs.GetInt(VSYNC_KEY, defaultVsync) == 1;

        QualitySettings.vSyncCount = vsyncOn ? 1 : 0;

        // Optional: if vsync off, set a high frame cap
        if (!vsyncOn)
            Application.targetFrameRate = 240;
        else
            Application.targetFrameRate = -1; // use default / vsync

        Debug.Log($"🔁 VSync applied: {vsyncOn}");
    }
}
