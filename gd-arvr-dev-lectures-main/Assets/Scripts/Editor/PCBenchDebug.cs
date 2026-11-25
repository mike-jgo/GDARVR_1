using UnityEditor;
using UnityEngine;

// Play Mode helpers for testing the PC bench without a headset.
public static class PCBenchDebug
{
    [MenuItem("VR PC Builder/Debug/Install Next Part")]
    static void InstallNext()
    {
        foreach (var part in Object.FindObjectsByType<PCPart>(FindObjectsInactive.Exclude))
            if (!part.IsInstalled)
            {
                part.Install();
                Debug.Log($"PC bench debug: installed {part.partName}");
                return;
            }
        Debug.Log("PC bench debug: every part is already installed.");
    }

    [MenuItem("VR PC Builder/Debug/Reset Build")]
    static void ResetBuild()
    {
        foreach (var controller in Object.FindObjectsByType<PCBuildController>(FindObjectsInactive.Exclude))
            controller.ResetBuild();
    }

    [MenuItem("VR PC Builder/Debug/Install Next Part", true)]
    [MenuItem("VR PC Builder/Debug/Reset Build", true)]
    static bool PlayModeOnly() => EditorApplication.isPlaying;

    // Same regression check vr-pc-builder ran on its RAM snap rule.
    [MenuItem("VR PC Builder/Debug/Check Snap Rules")]
    public static void CheckSnapRules()
    {
        var o = Vector3.zero;
        var r = Quaternion.identity;
        bool ok = PCPart.Fits(o, r, o, r, 0.06f, 30f)
            && !PCPart.Fits(Vector3.right, r, o, r, 0.06f, 30f)
            && !PCPart.Fits(o, Quaternion.Euler(0, 90, 0), o, r, 0.06f, 30f)
            && PCPart.Fits(new Vector3(0.04f, 0, 0), Quaternion.Euler(0, 20, 0), o, r, 0.06f, 30f);
        if (ok) Debug.Log("PC_SNAP_CHECKS_OK: correct pose, too far, wrong rotation, forgiving alignment");
        else Debug.LogError("PC snap rule regression check failed.");
    }
}
