using System.Collections;
using UnityEngine;

// Turns the player to face this object once the XR session has started. Meta's rig resets its
// yaw when the session begins, so the facing set in the editor isn't kept.
public class PlayerStartFacing : MonoBehaviour
{
    [Tooltip("Seconds to wait after Start for the rig and locomotor to settle.")]
    public float delay = 0.5f;

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(delay);
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig == null || rig.centerEyeAnchor == null) yield break;

        Transform head = rig.centerEyeAnchor;
        Vector3 toTarget = Vector3.ProjectOnPlane(transform.position - head.position, Vector3.up);
        Vector3 looking = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (toTarget.sqrMagnitude < 0.0001f || looking.sqrMagnitude < 0.0001f) yield break;
        float turn = Vector3.SignedAngle(looking, toTarget, Vector3.up);
        rig.transform.RotateAround(head.position, Vector3.up, turn);
    }
}
