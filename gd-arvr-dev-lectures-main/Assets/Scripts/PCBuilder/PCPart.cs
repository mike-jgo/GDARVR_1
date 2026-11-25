using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

// A PC component that is grabbed like the office props and installed by letting go of it
// close to, and roughly lined up with, its socket. Ported from vr-pc-builder's RamInstallation.
// Run after the SDK's grab updates so snapping wins on the release frame.
[DefaultExecutionOrder(1000)]
public class PCPart : MonoBehaviour
{
    [Header("Info (shown on the component panel)")]
    public string partName = "Part";
    [TextArea] public string specs;
    [TextArea] public string functions;
    [TextArea] public string connections;

    [Header("Installation")]
    public Transform socket;
    public Renderer socketIndicator;
    public Color socketColor = new Color(0.25f, 0.35f, 0.45f);
    public float snapDistance = 0.06f;
    public float snapAngle = 30f;

    public bool IsInstalled { get; private set; }
    public bool IsHeld { get; private set; }
    public bool IsHovered { get; private set; }
    public bool IsAligned { get; private set; }

    static readonly Color AlignedColor = new Color(0.1f, 0.85f, 1f);
    static readonly Color InstalledColor = new Color(0.2f, 1f, 0.45f);

    private Grabbable grabbable;
    private readonly List<IInteractableView> views = new List<IInteractableView>();
    private Material indicator;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool wasHeld;
    private int socketState = -1;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void OnEnable()
    {
        // OnEnable runs again after a Play Mode script reload; Awake does not.
        grabbable = GetComponent<Grabbable>();
        views.Clear();
        foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour is IInteractableView view) views.Add(view);
        if (grabbable == null || socket == null || socketIndicator == null)
        {
            Debug.LogError($"{name}: PCPart needs a Grabbable, a socket and a socket indicator.", this);
            enabled = false;
            return;
        }
        if (indicator == null) indicator = socketIndicator.material;
        socketState = -1;
    }

    public static bool Fits(Vector3 position, Quaternion rotation, Vector3 target, Quaternion orientation,
        float distance, float angle) => Vector3.Distance(position, target) <= distance
            && Quaternion.Angle(rotation, orientation) <= angle;

    private void LateUpdate()
    {
        IsHeld = grabbable.GrabPoints.Count > 0;
        IsHovered = false;
        if (!IsHeld)
            foreach (var view in views)
                if (view.State == InteractableState.Hover) { IsHovered = true; break; }
        if (IsHeld && !wasHeld) IsInstalled = false;

        // snapDistance is in the bench's own units, so it follows the bench's scale.
        IsAligned = Fits(transform.position, transform.rotation, socket.position, socket.rotation,
            snapDistance * socket.lossyScale.x, snapAngle);
        if (wasHeld && !IsHeld && IsAligned) Install();

        SetSocketState(IsInstalled ? 3 : IsHeld && IsAligned ? 2 : IsHeld ? 1 : 0);
        wasHeld = IsHeld;
    }

    // Snaps the part into its socket. Called when it is let go of while lined up.
    public void Install()
    {
        IsInstalled = true;
        transform.SetPositionAndRotation(socket.position, socket.rotation);
    }

    // Puts the part back where it started, uninstalled.
    public void ResetToStart()
    {
        IsInstalled = false;
        transform.SetPositionAndRotation(startPosition, startRotation);
    }

    private void SetSocketState(int state)
    {
        if (state == socketState) return;
        socketState = state;
        // 0 idle, 1 part held ("goes here"), 2 lined up, 3 installed.
        Color color = state == 3 ? InstalledColor
            : state == 2 ? AlignedColor
            : state == 1 ? Color.Lerp(socketColor, Color.white, 0.5f)
            : socketColor;
        indicator.color = color;
    }

    private void OnDestroy()
    {
        if (indicator != null) Destroy(indicator);
    }
}
