using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// Tracks the parts on the PC bench, fills the component panel (ComponentUI_Sample) with the
// part being pointed at or held, and shows build progress on the status panel.
[DefaultExecutionOrder(1001)]
public class PCBuildController : MonoBehaviour
{
    public PCPart[] parts;

    [Header("Component panel")]
    public TMP_Text componentName;
    public TMP_Text componentSpecs;
    public TMP_Text componentFunctions;
    public TMP_Text componentConnections;

    [Header("Status panel")]
    public TMP_Text status;

    public UnityEvent onBuildComplete;

    public bool IsComplete { get; private set; }

    private PCPart shownPart;
    private string lastStatus;
    private string lastName;

    private void Start()
    {
        ShowPart(parts.Length > 0 ? parts[0] : null);
    }

    private void LateUpdate()
    {
        PCPart focus = null;
        foreach (var part in parts)
            if (part.IsHeld) { focus = part; break; }
        if (focus == null)
            foreach (var part in parts)
                if (part.IsHovered) { focus = part; break; }
        if (focus != null && focus != shownPart) ShowPart(focus);
        UpdateName();

        int installed = 0;
        foreach (var part in parts)
            if (part.IsInstalled) installed++;
        bool complete = installed == parts.Length;
        if (complete && !IsComplete) onBuildComplete?.Invoke();
        IsComplete = complete;

        UpdateStatus(focus, installed);
    }

    private void ShowPart(PCPart part)
    {
        shownPart = part;
        if (part == null) return;
        if (componentSpecs != null) componentSpecs.text = part.specs;
        if (componentFunctions != null) componentFunctions.text = part.functions;
        if (componentConnections != null) componentConnections.text = part.connections;
        lastName = null;
    }

    private void UpdateName()
    {
        if (componentName == null || shownPart == null) return;
        string text = shownPart.partName.ToUpperInvariant() + (shownPart.IsInstalled ? "  (INSTALLED)" : "");
        if (text == lastName) return;
        lastName = text;
        componentName.text = text;
    }

    private void UpdateStatus(PCPart focus, int installed)
    {
        if (status == null) return;
        var text = new StringBuilder();
        text.Append(IsComplete ? "<b>BUILD COMPLETE!</b>" : $"<b>BUILD THE PC</b>  {installed}/{parts.Length}").Append('\n');
        foreach (var part in parts)
            text.Append(part.IsInstalled ? "<color=#4CFF73>[x]</color> " : "[  ] ").Append(part.partName).Append('\n');
        text.Append('\n');
        if (focus != null && focus.IsHeld)
            text.Append(focus.IsAligned
                ? $"{focus.partName} lined up - let go to install"
                : $"Move the {focus.partName} to its slot and match the angle");
        else if (focus != null)
            text.Append(focus.IsInstalled ? $"Grab the {focus.partName} to remove it" : $"Grab the {focus.partName} to pick it up");
        else
            text.Append(IsComplete ? "All parts installed. Grab any part to take it out." : "Pick up a part from the mat");

        string result = text.ToString();
        if (result == lastStatus) return;
        lastStatus = result;
        status.text = result;
    }

    // Puts every part back on the mat. Can be wired to a UI button.
    public void ResetBuild()
    {
        foreach (var part in parts) part.ResetToStart();
    }
}
