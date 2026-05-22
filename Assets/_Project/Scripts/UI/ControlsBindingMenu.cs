using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Controls Binding Menu
//
//  Click a row's key button → start listening → press any key/mouse to bind.
//  ESC cancels.
//
//  Listeners are wired up in Start() because Unity discards runtime
//  Button.onClick.AddListener calls on scene save. Rows are serialised
//  (action + button + label) so the builder can populate them at edit time.
// ─────────────────────────────────────────────────────────────────────────────
public class ControlsBindingMenu : MonoBehaviour
{
    [System.Serializable]
    public class SerializedRow
    {
        public string action;
        public Button button;
        public TextMeshProUGUI label;
    }

    [Tooltip("Populated by MainMenuBuilder. Wire-up happens at runtime in Start().")]
    public List<SerializedRow> rows = new List<SerializedRow>();

    SerializedRow _listening;
    bool _waitingForMouseRelease;

    void Start()
    {
        // Wire each row's button at runtime so the click handler survives scene save.
        foreach (var r in rows)
        {
            if (r == null || r.button == null) continue;
            var row = r;   // capture for closure
            row.button.onClick.AddListener(() => StartListening(row));
            Refresh(row);
        }
    }

    /// <summary>Called by the editor builder (MainMenuBuilder) at edit time.</summary>
    public void AddRow(string action, Button button, TextMeshProUGUI label)
    {
        rows.Add(new SerializedRow { action = action, button = button, label = label });
    }

    public void RefreshAll() { foreach (var r in rows) Refresh(r); }

    void Refresh(SerializedRow r)
    {
        if (r == null || r.label == null) return;
        r.label.text = KeyBindings.Pretty(KeyBindings.Get(r.action));
    }

    void StartListening(SerializedRow r)
    {
        if (_listening != null) return;
        _listening = r;
        if (r.label) r.label.text = "PRESS ANY KEY...";
        _waitingForMouseRelease = true;
        Debug.Log("[Controls] Listening for binding to action: " + r.action);
    }

    void Update()
    {
        if (_listening == null) return;

        if (_waitingForMouseRelease)
        {
            if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
                _waitingForMouseRelease = false;
            return;
        }

        // Mouse buttons
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)   { Bind(KeyCode.Mouse0); return; }
            if (Mouse.current.rightButton.wasPressedThisFrame)  { Bind(KeyCode.Mouse1); return; }
            if (Mouse.current.middleButton.wasPressedThisFrame) { Bind(KeyCode.Mouse2); return; }
        }

        // Keyboard via Input System
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame) { CancelListening(); return; }

            foreach (var keyControl in Keyboard.current.allKeys)
            {
                if (keyControl == null || !keyControl.wasPressedThisFrame) continue;
                KeyCode kc = KeyToKeyCode(keyControl.keyCode);
                if (kc == KeyCode.Escape) { CancelListening(); return; }
                if (kc != KeyCode.None)
                {
                    Debug.Log("[Controls] InputSystem caught key: " + kc);
                    Bind(kc);
                    return;
                }
            }
        }
    }

    // Fallback: OnGUI catches IMGUI keyboard events too
    void OnGUI()
    {
        if (_listening == null || _waitingForMouseRelease) return;
        var e = Event.current;
        if (e == null) return;
        if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
        {
            Debug.Log("[Controls] OnGUI caught key: " + e.keyCode);
            if (e.keyCode == KeyCode.Escape) CancelListening();
            else Bind(e.keyCode);
            e.Use();
        }
    }

    void Bind(KeyCode k)
    {
        if (_listening == null) return;
        KeyBindings.Set(_listening.action, k);
        Refresh(_listening);
        Debug.Log("[Controls] Bound " + _listening.action + " → " + k);
        _listening = null;
    }

    void CancelListening()
    {
        if (_listening == null) return;
        Refresh(_listening);
        Debug.Log("[Controls] Cancelled binding for " + _listening.action);
        _listening = null;
    }

    public void ResetControls()
    {
        KeyBindings.ResetAll();
        RefreshAll();
    }

    static KeyCode KeyToKeyCode(Key key)
    {
        switch (key)
        {
            case Key.Digit0: return KeyCode.Alpha0;
            case Key.Digit1: return KeyCode.Alpha1;
            case Key.Digit2: return KeyCode.Alpha2;
            case Key.Digit3: return KeyCode.Alpha3;
            case Key.Digit4: return KeyCode.Alpha4;
            case Key.Digit5: return KeyCode.Alpha5;
            case Key.Digit6: return KeyCode.Alpha6;
            case Key.Digit7: return KeyCode.Alpha7;
            case Key.Digit8: return KeyCode.Alpha8;
            case Key.Digit9: return KeyCode.Alpha9;
            case Key.LeftCtrl:    return KeyCode.LeftControl;
            case Key.RightCtrl:   return KeyCode.RightControl;
            case Key.Enter:       return KeyCode.Return;
            case Key.NumpadEnter: return KeyCode.KeypadEnter;
            case Key.Backquote:   return KeyCode.BackQuote;
            case Key.LeftMeta:    return KeyCode.LeftWindows;
            case Key.RightMeta:   return KeyCode.RightWindows;
            case Key.ContextMenu: return KeyCode.Menu;
        }
        if (System.Enum.TryParse<KeyCode>(key.ToString(), out var kc)) return kc;
        return KeyCode.None;
    }
}
