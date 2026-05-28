using System.Collections;
using System.Reflection;
using TMPro;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Level2ObjectiveManager : MonoBehaviour
{
    public enum ObjectiveStep
    {
        FirstPath,
        Keycard,
        MainDepot,
        Exit
    }

    [Header("HUD")]
    public CombatHUD combatHUD;
    public int totalObjectives = 3;

    [Header("Objective Text")]
    public string firstPathTitle = "CLEAR OUTER DEPOT PATH";
    [TextArea(2, 4)]
    public string firstPathDescription = "Push through the first guarded route and open the way deeper into the vehicle depot.";
    public string keycardTitle = "COLLECT DEPOT KEYCARD";
    [TextArea(2, 4)]
    public string keycardDescription = "Reach the watchtower area and pick up the keycard placed on the crate.";
    public string mainDepotTitle = "CLEAR MAIN VEHICLE DEPOT";
    [TextArea(2, 4)]
    public string mainDepotDescription = "Move into the heavily guarded vehicle yard and clear the path forward.";
    public string exitTitle = "REACH LEVEL 3 ENTRANCE";
    [TextArea(2, 4)]
    public string exitDescription = "Stand on the green entrance pad and press E once the depot route is secure.";

    [Header("Interaction")]
    public Key interactKey = Key.E;
    public float keycardInteractDistance = 3f;
    public float exitInteractDistance = 5f;

    [Header("Keycard")]
    public string keycardID = "blue_keycard";
    public string gateKeycardID = "door_1";
    public Vector3 keycardPosition = new Vector3(48.8f, 2.1f, 39.2f);

    [Header("Objective Trigger Zones")]
    public Vector3 firstPathTriggerPosition = new Vector3(25f, 1f, 27f);
    public Vector3 firstPathTriggerSize = new Vector3(20f, 3f, 8f);
    public Vector3 mainDepotTriggerPosition = new Vector3(25f, 1f, -94f);
    public Vector3 mainDepotTriggerSize = new Vector3(22f, 3f, 12f);
    public Vector3 exitTriggerPosition = new Vector3(25.8f, 1f, -123.7f);
    public Vector3 exitTriggerSize = new Vector3(14f, 3f, 9f);

    [Header("Mission Complete")]
    public string nextSceneName = "Level-3";
    public string completeTitle = "LEVEL 2 COMPLETE";
    public string completeStatus = "VEHICLE DEPOT PATH CLEARED";

    bool _firstPathDone;
    bool _keycardDone;
    bool _mainDepotDone;
    bool _missionShown;
    bool _showKeycardPrompt;
    bool _showExitPrompt;

    CanvasGroup _objectivePanelGroup;
    CanvasGroup _objectiveCounterGroup;
    CanvasGroup _notificationGroup;
    TextMeshProUGUI _objectiveCounterText;
    TextMeshProUGUI _objectiveTitleText;
    TextMeshProUGUI _objectiveDescriptionText;
    TextMeshProUGUI _notificationText;
    Image _objectiveAccentBar;
    Image _objectiveTopLine;
    Coroutine _objectiveSequence;
    GameObject _keycardObject;
    PlayerWeaponsManager _playerWeapons;
    Transform _playerTransform;
    static GUIStyle s_PromptStyle;
    static readonly FieldInfo s_DoorHolderField = typeof(ChainDoorController).GetField("_holder",
        BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo s_DoorPlayerField = typeof(ChainDoorController).GetField("_player",
        BindingFlags.Instance | BindingFlags.NonPublic);

    void Start()
    {
        if (combatHUD == null)
            combatHUD = FindAnyObjectByType<CombatHUD>(FindObjectsInactive.Include);

        BuildObjectiveTextUI();
        ShowObjective(firstPathTitle, firstPathDescription);
        RefreshHUD();
        CreateTrigger("L2_Objective_ClearOuterPath_Trigger", firstPathTriggerPosition, firstPathTriggerSize, ObjectiveStep.FirstPath);
        CreateTrigger("L2_Objective_ClearMainDepot_Trigger", mainDepotTriggerPosition, mainDepotTriggerSize, ObjectiveStep.MainDepot);
        CreateTrigger("L2_MissionComplete_Trigger", exitTriggerPosition, exitTriggerSize, ObjectiveStep.Exit);
        EnsureKeycardPickup();
    }

    void Update()
    {
        CachePlayer();
        HandleKeycardInteraction();
        HandleExitInteraction();
    }

    public void CompleteObjective(ObjectiveStep step)
    {
        switch (step)
        {
            case ObjectiveStep.FirstPath:
                MarkObjective(ref _firstPathDone, "Outer depot path cleared.");
                break;
            case ObjectiveStep.Keycard:
                MarkObjective(ref _keycardDone, "Depot keycard collected.");
                break;
            case ObjectiveStep.MainDepot:
                MarkObjective(ref _mainDepotDone, "Main depot path cleared.");
                break;
            case ObjectiveStep.Exit:
                TryShowMissionComplete();
                break;
        }
    }

    void MarkObjective(ref bool flag, string logMessage)
    {
        if (flag) return;
        flag = true;
        RefreshHUD();
        ShowObjectiveCompleteAndNext();
        Debug.Log("[Level2] " + logMessage);
    }

    void RefreshHUD()
    {
        if (combatHUD != null)
            combatHUD.SetObjectives(CompletedObjectiveCount(), totalObjectives);

        if (_objectiveCounterText != null)
            _objectiveCounterText.text = $"{CompletedObjectiveCount()} / {totalObjectives}";
    }

    int CompletedObjectiveCount()
    {
        int count = 0;
        if (_firstPathDone) count++;
        if (_keycardDone) count++;
        if (_mainDepotDone) count++;
        return count;
    }

    void TryShowMissionComplete()
    {
        if (_missionShown) return;

        if (!_firstPathDone || !_keycardDone || !_mainDepotDone)
        {
            ShowTemporaryNotification("FINISH CURRENT OBJECTIVE FIRST");
            ShowCurrentObjective();
            Debug.Log("[Level2] Exit reached, but objectives are not complete yet.");
            return;
        }

        _missionShown = true;
        RefreshHUD();
        ShowObjective(completeTitle, completeStatus);

        int kills = 0;
        var scoreManager = FindAnyObjectByType<ScoreManager>(FindObjectsInactive.Include);
        if (scoreManager != null)
            kills = scoreManager.Kills;

        string stats =
            "<color=#00C8FF>OBJECTIVES</color>\n3 / 3 COMPLETE\n\n" +
            $"<color=#00C8FF>KILLS</color>\n{kills}\n\n" +
            "<color=#00C8FF>STATUS</color>\n" + completeStatus;

        MissionCompleteScreen.Show(completeTitle, stats, nextSceneName, fadeDuration: 1.8f);
        Debug.Log("[Level2] Mission complete shown. Next scene = " + nextSceneName);
    }

    void EnsureKeycardPickup()
    {
        _keycardObject = FindKeycardVisualCandidate();
        if (_keycardObject == null)
        {
            _keycardObject = new GameObject("L2_BlueKeycard_Pickup");
            _keycardObject.transform.position = keycardPosition;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Keycard_Visual";
            visual.transform.SetParent(_keycardObject.transform, false);
            visual.transform.localScale = new Vector3(0.45f, 0.08f, 0.3f);

            var collider = visual.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateKeycardMaterial();
        }

        foreach (var trigger in _keycardObject.GetComponentsInChildren<KeycardPickupTrigger>(true))
            trigger.enabled = false;
    }

    GameObject FindKeycardVisualCandidate()
    {
        GameObject fallback = null;
        var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var candidate in transforms)
        {
            if (candidate == null) continue;
            if (candidate.gameObject.scene != gameObject.scene) continue;

            string objectName = candidate.name.ToLowerInvariant();
            if (!objectName.Contains("keycard")) continue;
            if (objectName.Contains("pad") || objectName.Contains("road")) continue;
            if (objectName.Contains("arrow") || objectName.Contains("line") || objectName.Contains("spur")) continue;

            if (objectName == "keycard" || objectName.Contains("bluekeycard"))
                return candidate.gameObject;

            if (fallback == null)
                fallback = candidate.gameObject;
        }

        return fallback;
    }

    void OnKeycardPickedUp()
    {
        CompleteObjective(ObjectiveStep.Keycard);
    }

    void CachePlayer()
    {
        if (_playerWeapons == null)
            _playerWeapons = FindAnyObjectByType<PlayerWeaponsManager>(FindObjectsInactive.Include);

        if (_playerWeapons == null) return;

        _playerTransform = _playerWeapons.transform;
    }

    void HandleKeycardInteraction()
    {
        _showKeycardPrompt = false;

        if (_keycardDone || _keycardObject == null || !_keycardObject.activeInHierarchy) return;
        if (_playerTransform == null) return;

        float distance = Vector3.Distance(_playerTransform.position, _keycardObject.transform.position);
        _showKeycardPrompt = distance <= keycardInteractDistance;

        if (_showKeycardPrompt && WasInteractPressed())
            CollectLevel2Keycard();
    }

    void CollectLevel2Keycard()
    {
        if (_keycardDone || _playerTransform == null) return;

        var holder = _playerTransform.GetComponent<ChainDoorKeycardHolder>()
                  ?? _playerTransform.GetComponentInParent<ChainDoorKeycardHolder>();
        if (holder == null)
            holder = _playerTransform.gameObject.AddComponent<ChainDoorKeycardHolder>();

        GrantLevel2Keycards(holder);

        var inventory = _playerTransform.GetComponent<KeycardInventory>()
                     ?? _playerTransform.GetComponentInParent<KeycardInventory>();
        if (inventory == null)
            inventory = _playerTransform.gameObject.AddComponent<KeycardInventory>();
        inventory.AddKeycard(KeycardColor.Blue);

        if (_keycardObject != null)
            _keycardObject.SetActive(false);

        CompleteObjective(ObjectiveStep.Keycard);
    }

    void GrantLevel2Keycards(ChainDoorKeycardHolder playerHolder)
    {
        GrantKeycardID(playerHolder, keycardID);
        GrantKeycardID(playerHolder, gateKeycardID);

        var doors = FindObjectsByType<ChainDoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var door in doors)
        {
            if (door == null) continue;
            GrantKeycardID(playerHolder, door.keycardID);
            s_DoorHolderField?.SetValue(door, playerHolder);
            s_DoorPlayerField?.SetValue(door, playerHolder.transform);
        }

        var holders = FindObjectsByType<ChainDoorKeycardHolder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var holder in holders)
        {
            if (holder == null || holder == playerHolder) continue;
            GrantKeycardID(holder, keycardID);
            GrantKeycardID(holder, gateKeycardID);
            foreach (var door in doors)
            {
                if (door != null)
                    GrantKeycardID(holder, door.keycardID);
            }
        }
    }

    void GrantKeycardID(ChainDoorKeycardHolder holder, string id)
    {
        if (holder == null || string.IsNullOrWhiteSpace(id)) return;
        holder.Collect(id);
    }

    void HandleExitInteraction()
    {
        _showExitPrompt = false;

        if (_missionShown) return;
        if (!_firstPathDone || !_keycardDone || !_mainDepotDone) return;
        if (_playerTransform == null) return;

        _showExitPrompt = IsPlayerInExitArea();
        if (_showExitPrompt && WasInteractPressed())
            TryShowMissionComplete();
    }

    bool IsPlayerInExitArea()
    {
        Vector3 delta = _playerTransform.position - exitTriggerPosition;
        bool insideTriggerFootprint =
            Mathf.Abs(delta.x) <= exitTriggerSize.x * 0.5f + 1f &&
            Mathf.Abs(delta.z) <= exitTriggerSize.z * 0.5f + 1f;

        if (insideTriggerFootprint)
            return true;

        Vector2 playerXZ = new Vector2(_playerTransform.position.x, _playerTransform.position.z);
        Vector2 exitXZ = new Vector2(exitTriggerPosition.x, exitTriggerPosition.z);
        return Vector2.Distance(playerXZ, exitXZ) <= exitInteractDistance;
    }

    bool WasInteractPressed()
    {
        return Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame;
    }

    Material CreateKeycardMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader);
        material.name = "L2_Runtime_BlueKeycard";
        material.color = new Color(0f, 0.55f, 1f, 1f);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0f, 0.35f, 1f, 1f));
        }

        return material;
    }

    void CreateTrigger(string triggerName, Vector3 position, Vector3 size, ObjectiveStep step)
    {
        if (GameObject.Find(triggerName) != null) return;

        var triggerObject = new GameObject(triggerName);
        triggerObject.transform.SetParent(transform);
        triggerObject.transform.position = position;

        var box = triggerObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;

        var trigger = triggerObject.AddComponent<Level2ObjectiveTrigger>();
        trigger.manager = this;
        trigger.step = step;
    }

    void ShowObjectiveCompleteAndNext()
    {
        if (_objectiveSequence != null)
            StopCoroutine(_objectiveSequence);

        _objectiveSequence = StartCoroutine(ObjectiveCompleteSequence());
    }

    IEnumerator ObjectiveCompleteSequence()
    {
        ShowNotification("OBJECTIVE COMPLETE");
        yield return StartCoroutine(FadeGroup(_notificationGroup, 0f, 1f, 0.2f));
        yield return new WaitForSeconds(1.4f);
        yield return StartCoroutine(FadeGroup(_notificationGroup, 1f, 0f, 0.3f));

        yield return StartCoroutine(FadeGroup(_objectivePanelGroup, 1f, 0f, 0.2f));
        ShowCurrentObjective();
        yield return StartCoroutine(FadeGroup(_objectivePanelGroup, 0f, 1f, 0.25f));
    }

    void ShowCurrentObjective()
    {
        if (!_firstPathDone)
        {
            ShowObjective(firstPathTitle, firstPathDescription);
            return;
        }

        if (!_keycardDone)
        {
            ShowObjective(keycardTitle, keycardDescription);
            return;
        }

        if (!_mainDepotDone)
        {
            ShowObjective(mainDepotTitle, mainDepotDescription);
            return;
        }

        ShowObjective(exitTitle, exitDescription);
    }

    void ShowObjective(string title, string description)
    {
        if (_objectiveTitleText != null)
            _objectiveTitleText.text = title;

        if (_objectiveDescriptionText != null)
            _objectiveDescriptionText.text = description;

        RefreshHUD();
    }

    void ShowNotification(string message)
    {
        if (_notificationText == null) return;
        _notificationText.text = message;
    }

    void ShowTemporaryNotification(string message)
    {
        if (_objectiveSequence != null)
            StopCoroutine(_objectiveSequence);

        _objectiveSequence = StartCoroutine(TemporaryNotificationSequence(message));
    }

    IEnumerator TemporaryNotificationSequence(string message)
    {
        ShowNotification(message);
        yield return StartCoroutine(FadeGroup(_notificationGroup, 0f, 1f, 0.2f));
        yield return new WaitForSeconds(1.2f);
        yield return StartCoroutine(FadeGroup(_notificationGroup, 1f, 0f, 0.3f));
    }

    void BuildObjectiveTextUI()
    {
        if (_objectivePanelGroup != null) return;

        var canvas = FindOrCreateUICanvas();
        HideDefaultObjectiveTracker(canvas);

        var counter = MakeRect("Level2ObjectiveCounter", canvas.transform);
        _objectiveCounterGroup = counter.gameObject.AddComponent<CanvasGroup>();
        _objectiveCounterGroup.blocksRaycasts = false;
        counter.anchorMin = new Vector2(0f, 1f);
        counter.anchorMax = new Vector2(0f, 1f);
        counter.pivot = new Vector2(0f, 1f);
        counter.anchoredPosition = new Vector2(10f, -10f);
        counter.sizeDelta = new Vector2(155f, 56f);
        counter.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.035f, 0.055f, 0.82f);

        Color accent = new Color(0f, 0.78f, 1f, 1f);
        AddImage("CounterTopLine", counter, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2f), new Color(accent.r, accent.g, accent.b, 0.75f));
        AddImage("CounterAccentBar", counter, new Vector2(0f, 0.12f), new Vector2(0f, 0.88f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 0f), accent);

        var counterLabel = AddText("CounterLabel", counter, "OBJECTIVE", 8f,
            new Color(accent.r, accent.g, accent.b, 0.95f), FontStyles.Bold, TextAlignmentOptions.BottomLeft);
        counterLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        counterLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        counterLabel.rectTransform.pivot = new Vector2(0f, 1f);
        counterLabel.rectTransform.anchoredPosition = new Vector2(12f, -5f);
        counterLabel.rectTransform.sizeDelta = new Vector2(-18f, 13f);
        counterLabel.characterSpacing = 3f;

        _objectiveCounterText = AddText("ObjectiveCounterText", counter, "", 24f, Color.white,
            FontStyles.Bold, TextAlignmentOptions.BottomLeft);
        _objectiveCounterText.rectTransform.anchorMin = new Vector2(0f, 1f);
        _objectiveCounterText.rectTransform.anchorMax = new Vector2(1f, 1f);
        _objectiveCounterText.rectTransform.pivot = new Vector2(0f, 1f);
        _objectiveCounterText.rectTransform.anchoredPosition = new Vector2(12f, -20f);
        _objectiveCounterText.rectTransform.sizeDelta = new Vector2(-18f, 30f);

        var panel = MakeRect("Level2ObjectivePanel", canvas.transform);
        _objectivePanelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        _objectivePanelGroup.blocksRaycasts = false;
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(10f, -78f);
        panel.sizeDelta = new Vector2(355f, 74f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.035f, 0.055f, 0.82f);

        _objectiveTopLine = AddImage("TopLine", panel, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 2f), new Color(accent.r, accent.g, accent.b, 0.75f));
        _objectiveAccentBar = AddImage("AccentBar", panel, new Vector2(0f, 0.1f), new Vector2(0f, 0.9f),
            new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 0f), accent);

        var label = AddText("ObjectiveLabel", panel, "OBJECTIVE", 8f, new Color(accent.r, accent.g, accent.b, 0.95f),
            FontStyles.Bold, TextAlignmentOptions.BottomLeft);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(1f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.anchoredPosition = new Vector2(12f, -4f);
        label.rectTransform.sizeDelta = new Vector2(-18f, 13f);
        label.characterSpacing = 3f;

        _objectiveTitleText = AddText("ObjectiveTitle", panel, "", 13f, Color.white,
            FontStyles.Bold, TextAlignmentOptions.BottomLeft);
        _objectiveTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        _objectiveTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        _objectiveTitleText.rectTransform.pivot = new Vector2(0f, 1f);
        _objectiveTitleText.rectTransform.anchoredPosition = new Vector2(12f, -19f);
        _objectiveTitleText.rectTransform.sizeDelta = new Vector2(-18f, 20f);

        _objectiveDescriptionText = AddText("ObjectiveDescription", panel, "", 9.5f,
            new Color(0.82f, 0.88f, 0.92f, 1f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _objectiveDescriptionText.rectTransform.anchorMin = new Vector2(0f, 1f);
        _objectiveDescriptionText.rectTransform.anchorMax = new Vector2(1f, 1f);
        _objectiveDescriptionText.rectTransform.pivot = new Vector2(0f, 1f);
        _objectiveDescriptionText.rectTransform.anchoredPosition = new Vector2(12f, -40f);
        _objectiveDescriptionText.rectTransform.sizeDelta = new Vector2(-18f, 28f);
        _objectiveDescriptionText.enableWordWrapping = true;
        _objectiveDescriptionText.overflowMode = TextOverflowModes.Ellipsis;

        var notification = MakeRect("Level2ObjectiveNotification", canvas.transform);
        _notificationGroup = notification.gameObject.AddComponent<CanvasGroup>();
        _notificationGroup.alpha = 0f;
        _notificationGroup.blocksRaycasts = false;
        notification.anchorMin = new Vector2(0.5f, 0f);
        notification.anchorMax = new Vector2(0.5f, 0f);
        notification.pivot = new Vector2(0.5f, 0f);
        notification.anchoredPosition = new Vector2(0f, 225f);
        notification.sizeDelta = new Vector2(420f, 48f);
        notification.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.08f, 0.045f, 0.9f);

        _notificationText = AddText("NotificationText", notification, "", 18f,
            new Color(0f, 1f, 0.55f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);
        _notificationText.rectTransform.anchorMin = Vector2.zero;
        _notificationText.rectTransform.anchorMax = Vector2.one;
        _notificationText.rectTransform.offsetMin = Vector2.zero;
        _notificationText.rectTransform.offsetMax = Vector2.zero;
        _notificationText.characterSpacing = 2f;
    }

    void HideDefaultObjectiveTracker(Canvas canvas)
    {
        if (canvas == null) return;

        var rects = canvas.GetComponentsInChildren<RectTransform>(true);
        foreach (var rect in rects)
        {
            if (rect == null || rect.transform == canvas.transform) continue;
            if (rect.name == "Objectives" || rect.name == "ObjectivesRect")
                rect.gameObject.SetActive(false);
        }
    }

    Canvas FindOrCreateUICanvas()
    {
        var existingHUD = GameObject.Find("CombatHUDCanvas");
        if (existingHUD != null)
        {
            var canvas = existingHUD.GetComponent<Canvas>();
            if (canvas != null)
                return canvas;
        }

        var canvasObject = new GameObject("Level2RuntimeObjectiveCanvas");
        var runtimeCanvas = canvasObject.AddComponent<Canvas>();
        runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeCanvas.sortingOrder = 52;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return runtimeCanvas;
    }

    static RectTransform MakeRect(string name, Transform parent)
    {
        var rectObject = new GameObject(name);
        rectObject.transform.SetParent(parent, false);
        return rectObject.AddComponent<RectTransform>();
    }

    static Image AddImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        var rect = MakeRect(name, parent);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    static TextMeshProUGUI AddText(string name, RectTransform parent, string text, float fontSize,
        Color color, FontStyles style, TextAlignmentOptions alignment)
    {
        var rect = MakeRect(name, parent);
        var textComponent = rect.gameObject.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.color = color;
        textComponent.fontStyle = style;
        textComponent.alignment = alignment;
        return textComponent;
    }

    IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;

        float elapsed = 0f;
        group.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        group.alpha = to;
    }

    void OnGUI()
    {
        if (_showKeycardPrompt)
            DrawCenteredPrompt("PRESS E TO PICK UP KEYCARD", 0.62f);
        else if (_showExitPrompt)
            DrawCenteredPrompt("PRESS E TO ENTER LEVEL 3", 0.62f);
    }

    static void DrawCenteredPrompt(string message, float screenHeightRatio)
    {
        if (s_PromptStyle == null)
        {
            s_PromptStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            s_PromptStyle.normal.textColor = new Color(0f, 0.85f, 1f, 1f);
        }

        float width = 420f;
        float height = 46f;
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height * screenHeightRatio;

        GUI.Box(new Rect(x, y, width, height), message, s_PromptStyle);
    }
}
