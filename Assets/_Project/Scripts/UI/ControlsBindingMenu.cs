using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Controls Binding Menu
//  One row per action, click the key button to rebind.
//  The MainMenuBuilder constructs the rows and registers them via AddRow().
// ─────────────────────────────────────────────────────────────────────────────
public class ControlsBindingMenu : MonoBehaviour
{
    public class Row
    {
        public string action;
        public TextMeshProUGUI label;
        public Button button;
    }

    readonly List<Row> _rows = new();
    Row _listening;
    bool _skipFirstFrame;

    public void AddRow(string action, Button button, TextMeshProUGUI label)
    {
        var row = new Row { action = action, button = button, label = label };
        _rows.Add(row);
        button.onClick.AddListener(() => StartListening(row));
        Refresh(row);
    }

    public void RefreshAll()
    {
        foreach (var r in _rows) Refresh(r);
    }

    void Refresh(Row r)
    {
        if (r.label != null)
            r.label.text = KeyBindings.Pretty(KeyBindings.Get(r.action));
    }

    void StartListening(Row r)
    {
        if (_listening != null) return;          // ignore — already listening
        _listening = r;
        if (r.label) r.label.text = "PRESS ANY KEY...";
        _skipFirstFrame = true;
    }

    void Update()
    {
        if (_listening == null) return;
        if (_skipFirstFrame) { _skipFirstFrame = false; return; }

        // Cancel with Escape (but only if Escape isn't the key being bound)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelListening();
            return;
        }

        // Mouse buttons (Mouse0 = Left, Mouse1 = Right, Mouse2 = Middle)
        for (int i = 0; i < 3; i++)
        {
            if (Input.GetMouseButtonDown(i))
            {
                Bind(KeyCode.Mouse0 + i);
                return;
            }
        }

        // Any keyboard key
        if (Input.anyKeyDown)
        {
            foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
            {
                // Skip mouse codes (handled above), skip None
                if (k == KeyCode.None) continue;
                if (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) continue;
                if (Input.GetKeyDown(k))
                {
                    Bind(k);
                    return;
                }
            }
        }
    }

    void Bind(KeyCode k)
    {
        if (_listening == null) return;
        KeyBindings.Set(_listening.action, k);
        Refresh(_listening);
        _listening = null;
    }

    void CancelListening()
    {
        if (_listening == null) return;
        Refresh(_listening);
        _listening = null;
    }

    public void ResetControls()
    {
        KeyBindings.ResetAll();
        RefreshAll();
    }
}
