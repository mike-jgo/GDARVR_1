using System.Collections.Generic;
using System.IO;
using Oculus.Interaction;
using Oculus.Interaction.Editor.QuickActions;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds the PC building bench (motherboard, sockets, CPU/RAM/GPU/SSD, component + status panels)
// on a desk in the open scene and saves it as Assets/Prefabs/PCBuilder/PCBuildBench.prefab.
// Parts are set up like the office props: Meta "Add Grab Interaction" for hands/controllers,
// plus a RayInteractable that drives Selectfeedback.
public static class PCBenchSetup
{
    const string RootName = "PC Build Bench";
    const string MaterialFolder = "Assets/Materials/PCBuilder";
    const string PrefabFolder = "Assets/Prefabs/PCBuilder";
    const string PrefabPath = PrefabFolder + "/PCBuildBench.prefab";
    const string ComponentPanelPath = "Assets/Prefabs/UI/SceneUI/ComponentUI_Sample.prefab";

    // The office is modelled about 6x real size, so the player rig and the bench are scaled to match.
    const float WorldScale = 6f;

    // Bench layout, in real-world metres (before WorldScale), relative to the desk top.
    // +z points away from the player.
    const float MatTop = 0.005f;
    const float BoardTop = MatTop + 0.012f;

    [MenuItem("VR PC Builder/Add PC Bench to Open Scene")]
    public static void AddBench()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Stop Play Mode before adding the PC bench.");
            return;
        }
        var rig = Object.FindAnyObjectByType<OVRCameraRig>();
        if (rig == null)
        {
            Debug.LogError("No OVRCameraRig in the open scene.");
            return;
        }
        var existing = GameObject.Find(RootName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        var desk = FindDesk(rig.transform.position, out Bounds deskBounds);
        if (desk == null)
        {
            Debug.LogError("No desk found. Select the table to build on, then run this again.");
            return;
        }
        var whole = deskBounds;
        deskBounds = DesktopBounds(desk);
        Debug.Log($"PC bench desk '{desk.name}': whole bounds {whole.min} to {whole.max}, desktop {deskBounds.min} to {deskBounds.max}, player {rig.transform.position}");
        EnsurePointableCanvasModule();

        // Face the bench along whichever desk axis points most directly away from the player.
        Vector3 toDesk = deskBounds.center - rig.transform.position;
        toDesk.y = 0;
        Vector3 forward = ClosestAxis(desk.transform, toDesk.sqrMagnitude > 0.0001f ? toDesk.normalized : Vector3.forward);
        float halfDepth = Mathf.Abs(forward.x) * deskBounds.extents.x + Mathf.Abs(forward.z) * deskBounds.extents.z;
        Vector3 top = new Vector3(deskBounds.center.x, deskBounds.max.y, deskBounds.center.z);
        // Keep the mat close to the near edge so every part is within reach.
        Vector3 benchPosition = top - forward * Mathf.Max(0f, halfDepth - 0.25f * WorldScale);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Add PC Bench");
        root.transform.SetPositionAndRotation(benchPosition, Quaternion.LookRotation(forward, Vector3.up));

        BuildBench(root.transform);
        root.transform.localScale = Vector3.one * WorldScale;
        ClearMat(root.transform, desk, forward);

        // Scale the player to the room and stand them on the floor at the near edge of the desk.
        Undo.RecordObject(rig.transform, "Move player to PC bench");
        Vector3 player = benchPosition - forward * 0.55f * WorldScale;
        rig.transform.localScale = Vector3.one * WorldScale;
        rig.transform.SetPositionAndRotation(new Vector3(player.x, whole.min.y, player.z),
            Quaternion.LookRotation(forward, Vector3.up));
        ClearStandingSpot(player, forward);

        EnsureFolder(PrefabFolder);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
        var scene = root.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log($"PC_BENCH_OK: built on '{desk.name}', saved {PrefabPath} and {scene.path}");
    }

    static void BuildBench(Transform root)
    {
        var mat = Mat("Mat", new Color(0.12f, 0.13f, 0.14f));
        var board = Mat("Motherboard", new Color(0.11f, 0.32f, 0.2f));
        var dark = Mat("Dark", new Color(0.08f, 0.08f, 0.09f));
        var gold = Mat("Gold", new Color(0.85f, 0.65f, 0.25f));
        var silver = Mat("Silver", new Color(0.75f, 0.77f, 0.8f));
        var substrate = Mat("Substrate", new Color(0.15f, 0.4f, 0.25f));
        var shroud = Mat("Shroud", new Color(0.22f, 0.24f, 0.27f));
        var ssd = Mat("SSD", new Color(0.1f, 0.15f, 0.3f));
        var white = Mat("Label", new Color(0.9f, 0.9f, 0.9f));
        Color cpuColor = new Color(0.9f, 0.55f, 0.15f);
        Color ramColor = new Color(0.6f, 0.4f, 0.9f);
        Color gpuColor = new Color(0.9f, 0.3f, 0.3f);
        Color ssdColor = new Color(0.95f, 0.85f, 0.25f);

        Box(root, "Work Mat", new Vector3(0, MatTop / 2, 0), new Vector3(0.75f, MatTop, 0.4f), mat);

        var mb = new GameObject("Motherboard").transform;
        mb.SetParent(root, false);
        Box(mb, "Board", new Vector3(0.17f, MatTop + 0.006f, 0), new Vector3(0.3f, 0.012f, 0.3f), board);
        Box(mb, "VRM Heatsink", new Vector3(0.1f, BoardTop + 0.008f, 0.125f), new Vector3(0.09f, 0.016f, 0.02f), dark);
        Box(mb, "IO Shroud", new Vector3(0.04f, BoardTop + 0.015f, 0.08f), new Vector3(0.03f, 0.03f, 0.1f), dark);
        Box(mb, "Chipset", new Vector3(0.24f, BoardTop + 0.004f, -0.1f), new Vector3(0.035f, 0.008f, 0.035f), dark);

        // Sockets: an indicator that lights up, plus the pose the part snaps to.
        var cpuSlot = Box(mb, "CPU Socket", new Vector3(0.12f, BoardTop + 0.002f, 0.05f), new Vector3(0.05f, 0.004f, 0.05f), Mat("Socket CPU", cpuColor));
        Box(mb, "CPU Socket Marker", new Vector3(0.1f, BoardTop + 0.0045f, 0.03f), new Vector3(0.006f, 0.001f, 0.006f), gold);
        var ramSlot = Box(mb, "RAM Slot", new Vector3(0.23f, BoardTop + 0.006f, 0), new Vector3(0.012f, 0.012f, 0.14f), Mat("Socket RAM", ramColor));
        var gpuSlot = Box(mb, "PCIe Slot", new Vector3(0.15f, BoardTop + 0.006f, -0.08f), new Vector3(0.09f, 0.012f, 0.012f), Mat("Socket GPU", gpuColor));
        var ssdSlot = Box(mb, "M.2 Slot", new Vector3(0.12f, BoardTop + 0.001f, -0.02f), new Vector3(0.085f, 0.002f, 0.026f), Mat("Socket SSD", ssdColor));
        Box(mb, "M.2 Connector", new Vector3(0.075f, BoardTop + 0.003f, -0.02f), new Vector3(0.006f, 0.006f, 0.024f), dark);

        var parts = new List<PCPart>();

        var cpu = Part(root, "CPU", new Vector3(-0.31f, MatTop + 0.004f, 0.11f), Quaternion.Euler(0, 90, 0),
            new Vector3(0.04f, 0.006f, 0.04f), new Vector3(0, 0.001f, 0),
            ("Substrate", Vector3.zero, new Vector3(0.04f, 0.004f, 0.04f), substrate),
            ("Heat Spreader", new Vector3(0, 0.003f, 0), new Vector3(0.032f, 0.002f, 0.032f), silver),
            ("Pin 1 Marker", new Vector3(-0.016f, 0.0045f, -0.016f), new Vector3(0.005f, 0.001f, 0.005f), gold));
        parts.Add(Install(cpu, Pose(root, "CPU Install Pose", new Vector3(0.12f, BoardTop + 0.006f, 0.05f)), cpuSlot, cpuColor, 0.04f, 25f,
            "6 cores / 12 threads\n3.8 GHz base, 4.6 GHz boost\n65 W",
            "Runs every program's instructions. It is the brain of the PC.",
            "Sits in the CPU socket on the motherboard. Line up the gold corner with the socket's gold marker."));

        var ram = Part(root, "RAM", new Vector3(-0.2f, MatTop + 0.005f, 0.08f), Quaternion.Euler(0, 0, 90),
            new Vector3(0.008f, 0.035f, 0.13f), Vector3.zero,
            ("PCB", Vector3.zero, new Vector3(0.006f, 0.035f, 0.13f), substrate),
            ("Chips", new Vector3(0.004f, 0.002f, 0), new Vector3(0.002f, 0.014f, 0.11f), dark),
            ("Contacts", new Vector3(0, -0.0155f, 0), new Vector3(0.0065f, 0.004f, 0.12f), gold));
        parts.Add(Install(ram, Pose(root, "RAM Install Pose", new Vector3(0.23f, BoardTop + 0.0245f, 0)), ramSlot, ramColor, 0.06f, 30f,
            "16 GB DDR4\n3200 MT/s\n288-pin DIMM",
            "Short-term memory that holds the programs and data in use right now.",
            "Pushes straight down into a DIMM slot on the motherboard, contacts first."));

        var gpu = Part(root, "GPU", new Vector3(-0.19f, MatTop + 0.018f, -0.09f), Quaternion.Euler(90, 0, 0),
            new Vector3(0.22f, 0.1f, 0.035f), Vector3.zero,
            ("Shroud", Vector3.zero, new Vector3(0.22f, 0.1f, 0.035f), shroud),
            ("Fan 1", new Vector3(-0.05f, 0, -0.018f), new Vector3(0.07f, 0.002f, 0.07f), dark),
            ("Fan 2", new Vector3(0.05f, 0, -0.018f), new Vector3(0.07f, 0.002f, 0.07f), dark),
            ("Bracket", new Vector3(-0.111f, 0.005f, 0), new Vector3(0.002f, 0.12f, 0.02f), silver),
            ("PCIe Contacts", new Vector3(0.05f, -0.054f, 0), new Vector3(0.08f, 0.008f, 0.003f), gold));
        parts.Add(Install(gpu, Pose(root, "GPU Install Pose", new Vector3(0.1f, BoardTop + 0.058f, -0.08f)), gpuSlot, gpuColor, 0.06f, 30f,
            "8 GB GDDR6\nPCIe 4.0 x16\nDual fan",
            "Draws the 3D graphics and sends the picture to the monitor.",
            "Plugs into the long PCIe x16 slot on the motherboard. Also needs power from the PSU."));

        var drive = Part(root, "SSD", new Vector3(-0.31f, MatTop + 0.003f, 0.03f), Quaternion.identity,
            new Vector3(0.08f, 0.004f, 0.022f), new Vector3(0, 0.0005f, 0),
            ("PCB", Vector3.zero, new Vector3(0.08f, 0.003f, 0.022f), ssd),
            ("Controller", new Vector3(-0.02f, 0.002f, 0), new Vector3(0.012f, 0.0015f, 0.012f), dark),
            ("Label", new Vector3(0.015f, 0.002f, 0), new Vector3(0.035f, 0.0005f, 0.018f), white));
        parts.Add(Install(drive, Pose(root, "SSD Install Pose", new Vector3(0.12f, BoardTop + 0.0035f, -0.02f)), ssdSlot, ssdColor, 0.04f, 25f,
            "1 TB NVMe\nM.2 2280\n3500 MB/s read",
            "Stores the operating system, programs and files, even when the PC is off.",
            "Slides into the M.2 slot on the motherboard and lies flat against the board."));

        // Panels in the project's UI style: the team's component panel and a matching status panel.
        root.gameObject.AddComponent<PlayerStartFacing>();
        var controller = root.gameObject.AddComponent<PCBuildController>();
        controller.parts = parts.ToArray();
        var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ComponentPanelPath);
        if (panelPrefab != null)
        {
            var panel = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, root);
            panel.name = "Component Panel";
            FitCanvas(panel, root.TransformPoint(new Vector3(0.17f, 0.3f, 0.17f)), 0.5f);
            controller.componentName = FindText(panel, "Canvas/Panel/NamePanel/Text (TMP)");
            controller.componentSpecs = FindText(panel, "Canvas/Panel/SpecsPanel/Specs");
            controller.componentFunctions = FindText(panel, "Canvas/Panel/FunctionsPanel/Scroll View/Viewport/Content/Uses");
            controller.componentConnections = FindText(panel, "Canvas/Panel/ConnectionsPanel/Scroll View/Viewport/Content/Connections");
        }
        else Debug.LogWarning("ComponentUI_Sample prefab not found; the bench has no component panel.");
        controller.status = StatusPanel(root, new Vector3(-0.55f, 0.25f, -0.02f), -30f);
    }

    static GameObject Part(Transform root, string name, Vector3 position, Quaternion rotation, Vector3 colliderSize,
        Vector3 colliderCenter, params (string name, Vector3 position, Vector3 size, Material material)[] boxes)
    {
        var part = new GameObject(name);
        part.transform.SetParent(root, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation;
        foreach (var b in boxes)
        {
            var box = Box(part.transform, b.name, b.position, b.size, b.material);
            if (b.name.StartsWith("Fan"))
            {
                // Fans are discs facing the card's front.
                Object.DestroyImmediate(box);
                box = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(box.GetComponent<Collider>());
                box.name = b.name;
                box.transform.SetParent(part.transform, false);
                box.transform.localPosition = b.position;
                box.transform.localRotation = Quaternion.Euler(90, 0, 0);
                box.transform.localScale = b.size;
                box.GetComponent<Renderer>().sharedMaterial = b.material;
            }
        }
        var collider = part.AddComponent<BoxCollider>();
        collider.size = colliderSize;
        collider.center = colliderCenter;

        // Same setup as the office props: Meta's grab quick action, then ray hover/select -> Selectfeedback.
        QuickActionsAPI.AddGrabInteraction(part);
        var surface = part.AddComponent<ColliderSurface>();
        surface.InjectCollider(collider);
        var ray = part.AddComponent<RayInteractable>();
        ray.InjectSurface(surface);
        var events = part.AddComponent<InteractableUnityEventWrapper>();
        events.InjectInteractableView(ray);
        InitializeEvents(events);
        var feedback = part.AddComponent<Selectfeedback>();
        UnityEventTools.AddPersistentListener(events.WhenHover, feedback.Hover);
        UnityEventTools.AddPersistentListener(events.WhenUnhover, feedback.Normal);
        UnityEventTools.AddPersistentListener(events.WhenSelect, feedback.Selected);
        UnityEventTools.AddPersistentListener(events.WhenUnselect, feedback.Hover);
        return part;
    }

    // A freshly added component's serialized UnityEvents are null until Unity serializes it.
    static void InitializeEvents(Object component)
    {
        var fields = component.GetType().GetFields(System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        foreach (var field in fields)
            if (field.FieldType == typeof(UnityEngine.Events.UnityEvent) && field.GetValue(component) == null)
                field.SetValue(component, new UnityEngine.Events.UnityEvent());
    }

    static PCPart Install(GameObject part, Transform pose, GameObject indicator, Color color, float distance, float angle,
        string specs, string functions, string connections)
    {
        var p = part.AddComponent<PCPart>();
        p.partName = part.name;
        p.specs = specs;
        p.functions = functions;
        p.connections = connections;
        p.socket = pose;
        p.socketIndicator = indicator.GetComponent<Renderer>();
        p.socketColor = color;
        p.snapDistance = distance;
        p.snapAngle = angle;
        return p;
    }

    static Transform Pose(Transform root, string name, Vector3 position)
    {
        var pose = new GameObject(name).transform;
        pose.SetParent(root, false);
        pose.localPosition = position;
        pose.localRotation = Quaternion.identity;
        return pose;
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(box.GetComponent<Collider>());
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    static TMP_Text StatusPanel(Transform root, Vector3 localCenter, float yaw)
    {
        var go = new GameObject("Build Status Panel", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(root, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(300, 260);
        rect.localScale = Vector3.one * 0.0012f;
        rect.localPosition = localCenter;
        // Turned toward the player, who stands in front of the mat.
        rect.localRotation = Quaternion.Euler(0, yaw, 0);

        // Same look as ComponentUI_Sample's panels: translucent black with white TMP text.
        var background = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(go.transform, false);
        Stretch((RectTransform)background.transform, 0);
        var image = background.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0, 0, 0, 0.392f);

        var label = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        Stretch((RectTransform)label.transform, 16);
        var text = label.GetComponent<TextMeshProUGUI>();
        text.fontSize = 22;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.richText = true;
        text.text = "BUILD THE PC";
        return text;
    }

    static void Stretch(RectTransform rect, float margin)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(margin, margin);
        rect.offsetMax = new Vector2(-margin, -margin);
    }

    // Scales and moves a world-space UI prefab so its visible panel is `width` metres wide, centred on `center`.
    static void FitCanvas(GameObject panel, Vector3 center, float width)
    {
        var visible = panel.transform.Find("Canvas/Panel") as RectTransform;
        if (visible == null)
        {
            var canvas = panel.GetComponentInChildren<Canvas>(true);
            if (canvas == null) return;
            visible = (RectTransform)canvas.transform;
        }
        panel.transform.localRotation = Quaternion.identity;
        float current = visible.rect.width * visible.lossyScale.x;
        if (current > 0.0001f) panel.transform.localScale *= width / current;
        Vector3 visibleCenter = visible.TransformPoint(visible.rect.center);
        panel.transform.position += center - visibleCenter;
    }

    static TMP_Text FindText(GameObject panel, string path)
    {
        var t = panel.transform.Find(path);
        var text = t != null ? t.GetComponent<TMP_Text>() : null;
        if (text == null) Debug.LogWarning($"Component panel text not found: {path}");
        return text;
    }

    // The selected object if it has renderers, otherwise the "Table..." object nearest the player.
    static GameObject FindDesk(Vector3 player, out Bounds bounds)
    {
        bounds = default;
        var selected = Selection.activeGameObject;
        if (selected != null && selected.GetComponentInChildren<Renderer>() != null && selected.GetComponentInParent<OVRCameraRig>() == null)
        {
            // Only table-sized selections count, so a stray selected prop is ignored.
            var b = RendererBounds(selected);
            if (b.size.x >= 0.5f && b.size.z >= 0.5f)
            {
                bounds = b;
                return selected;
            }
        }
        GameObject best = null;
        float bestDistance = float.MaxValue;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (!t.name.StartsWith("Table") && !t.name.StartsWith("Desk")) continue;
            if (t.parent != null && (t.parent.name.StartsWith("Table") || t.parent.name.StartsWith("Desk"))) continue;
            if (t.GetComponentInChildren<Renderer>() == null) continue;
            var b = RendererBounds(t.gameObject);
            Vector3 offset = b.center - player;
            offset.y = 0;
            if (offset.magnitude < bestDistance)
            {
                bestDistance = offset.magnitude;
                best = t.gameObject;
                bounds = b;
            }
        }
        return best;
    }

    static Bounds RendererBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    // The desktop is the desk's flattest, widest mesh; its top is where the bench goes.
    static Bounds DesktopBounds(GameObject desk)
    {
        Bounds best = RendererBounds(desk);
        float bestScore = 0;
        foreach (var r in desk.GetComponentsInChildren<MeshRenderer>())
        {
            var b = r.bounds;
            float area = b.size.x * b.size.z;
            if (b.size.y > 0.25f * Mathf.Sqrt(area)) continue; // not slab-like
            if (area > bestScore) { bestScore = area; best = b; }
        }
        return best;
    }

    // Slides desk props (keyboard, mousepad, ...) that sit on the work mat out to the side.
    static void ClearMat(Transform bench, GameObject desk, Vector3 forward)
    {
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        // The mat is 0.75 x 0.4 m; keep a small margin around it.
        float halfWidth = 0.4f * WorldScale, halfDepth = 0.22f * WorldScale;
        float top = bench.position.y;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (t == bench || t.IsChildOf(bench) || t.gameObject == desk || t.IsChildOf(desk.transform)) continue;
            if (t.GetComponentInParent<OVRCameraRig>() != null) continue;
            // Only move whole props: objects with renderers whose parent is a plain grouping object.
            if (t.GetComponent<Renderer>() == null && t.GetComponentInChildren<Renderer>() == null) continue;
            if (t.parent == null || t.parent.GetComponent<Renderer>() != null || t.parent.GetComponent<Rigidbody>() != null) continue;
            if (t.GetComponentsInParent<Renderer>().Length > t.GetComponentsInChildren<Renderer>().Length) continue;
            var b = RendererBounds(t.gameObject);
            if (b.min.y > top + 0.3f * WorldScale || b.max.y < top - 0.02f * WorldScale) continue;
            if (b.size.x > 2 * halfWidth && b.size.z > 2 * halfWidth) continue; // furniture, not a prop
            if (b.size.y > 0.12f * WorldScale) continue; // tall things like monitors stay put
            Vector3 local = b.center - bench.position;
            float along = Vector3.Dot(local, forward), across = Vector3.Dot(local, side);
            float extentAcross = Mathf.Abs(side.x) * b.extents.x + Mathf.Abs(side.z) * b.extents.z;
            float extentAlong = Mathf.Abs(forward.x) * b.extents.x + Mathf.Abs(forward.z) * b.extents.z;
            if (Mathf.Abs(along) >= halfDepth + extentAlong - 0.01f || Mathf.Abs(across) >= halfWidth + extentAcross - 0.01f) continue;
            float push = halfWidth + extentAcross - Mathf.Abs(across);
            Undo.RecordObject(t, "Move prop off the PC bench");
            t.position += side * (across >= 0 ? push : -push);
            Debug.Log($"PC bench: moved '{t.name}' off the work mat.");
        }
    }

    // Meta's locomotor grounds the player on whatever is below them at start, so slide any chair
    // that covers the standing spot out to the side.
    static void ClearStandingSpot(Vector3 spot, Vector3 forward)
    {
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        float clearance = 0.35f * WorldScale;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (!t.name.Contains("Chair") || (t.parent != null && t.parent.name.Contains("Chair"))) continue;
            if (t.GetComponentInChildren<Renderer>() == null) continue;
            var b = RendererBounds(t.gameObject);
            Vector3 closest = b.ClosestPoint(new Vector3(spot.x, b.center.y, spot.z));
            Vector3 gap = closest - spot;
            gap.y = 0;
            if (gap.magnitude >= clearance - 0.001f) continue;
            float sideExtent = Mathf.Abs(side.x) * b.extents.x + Mathf.Abs(side.z) * b.extents.z;
            float offset = Vector3.Dot(b.center - spot, side);
            float push = sideExtent + clearance - Mathf.Abs(offset);
            Undo.RecordObject(t, "Move chair out of the way");
            t.position += side * (offset >= 0 ? push : -push);
            Debug.Log($"PC bench: moved '{t.name}' aside so the player can stand at the desk.");
        }
    }

    static void EnsurePointableCanvasModule()
    {
        if (Object.FindAnyObjectByType<PointableCanvasModule>() != null) return;
        var eventSystem = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            Undo.RegisterCreatedObjectUndo(go, "Add EventSystem");
            eventSystem = go.GetComponent<UnityEngine.EventSystems.EventSystem>();
        }
        Undo.AddComponent<PointableCanvasModule>(eventSystem.gameObject);
    }

    static Vector3 ClosestAxis(Transform t, Vector3 direction)
    {
        Vector3 best = Vector3.forward;
        float bestDot = float.MinValue;
        foreach (var axis in new[] { t.forward, -t.forward, t.right, -t.right })
        {
            var flat = new Vector3(axis.x, 0, axis.z);
            if (flat.sqrMagnitude < 0.0001f) continue;
            flat.Normalize();
            float dot = Vector3.Dot(flat, direction);
            if (dot > bestDot) { bestDot = dot; best = flat; }
        }
        return best;
    }

    static Material Mat(string name, Color color)
    {
        EnsureFolder(MaterialFolder);
        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Smoothness", 0.2f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
