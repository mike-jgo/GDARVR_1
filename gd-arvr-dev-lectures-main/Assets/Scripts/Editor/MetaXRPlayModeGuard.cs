using UnityEditor;

// From vr-pc-builder. Meta's runtime objects cannot survive an assembly reload during Play Mode,
// so automatic imports are queued until the current Play session has finished.
[InitializeOnLoad]
internal static class MetaXRPlayModeGuard
{
    const string PausedKey = "VRPCBuilder.MetaXRPlayModeGuard.AutoRefreshPaused";

    internal static bool IsDeferring => SessionState.GetBool(PausedKey, false);

    static MetaXRPlayModeGuard()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.quitting += ResumeRefresh;
        // SessionState survives assembly reloads, matching the native refresh counter.
        if (EditorApplication.isPlaying) PauseRefresh();
        else if (!EditorApplication.isPlayingOrWillChangePlaymode) ResumeRefresh();
    }

    static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode) PauseRefresh();
        // Keep the guard through teardown so queued scripts cannot reload while
        // Meta's interactors are still releasing their identifiers.
        if (state == PlayModeStateChange.EnteredEditMode) ResumeRefresh();
    }

    static void PauseRefresh()
    {
        if (IsDeferring) return;
        AssetDatabase.DisallowAutoRefresh();
        SessionState.SetBool(PausedKey, true);
    }

    static void ResumeRefresh()
    {
        if (!IsDeferring) return;
        SessionState.SetBool(PausedKey, false);
        AssetDatabase.AllowAutoRefresh();
    }
}
