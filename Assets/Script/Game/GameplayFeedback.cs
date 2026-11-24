using UnityEngine;

// Reuses the former timer/enemy text. Normal gameplay has no permanent counters.
public sealed class GameplayFeedback : MonoBehaviour
{
    [SerializeField] bool showDeveloperDiagnostics;
    UnityEngine.UI.Text diagnostics, transient;
    Player player;
    Libro level;
    float feedbackUntil, nextDiagnosticUpdate;

    public bool DiagnosticsEnabled
    {
        get { return showDeveloperDiagnostics && Debug.isDebugBuild; }
        set { showDeveloperDiagnostics = value && Debug.isDebugBuild; }
    }

    public void Initialize(Player actor, Libro owner, UnityEngine.UI.Text diagnosticText, UnityEngine.UI.Text feedbackText)
    {
        player = actor; level = owner;
        diagnostics = diagnosticText != null && diagnosticText.gameObject.scene.IsValid() ? diagnosticText : null;
        transient = feedbackText != null && feedbackText.gameObject.scene.IsValid() ? feedbackText : null;
        if (diagnostics != null) Configure(diagnostics, new Vector2(1, 1), new Vector2(-12, -12), new Vector2(500, 65), TextAnchor.UpperRight, 14);
        if (transient != null)
        {
            transient.gameObject.name = "Objective feedback";
            Configure(transient, new Vector2(.5f, 1), new Vector2(0, -70), new Vector2(400, 45), TextAnchor.MiddleCenter, 20);
        }
        SetVisible(diagnostics, false); SetVisible(transient, false);
    }

    static void Configure(UnityEngine.UI.Text text, Vector2 anchor, Vector2 offset, Vector2 size, TextAnchor alignment, int fontSize)
    {
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = anchor; text.rectTransform.pivot = anchor;
        text.rectTransform.anchoredPosition = offset; text.rectTransform.sizeDelta = size; text.rectTransform.localScale = Vector3.one;
        text.alignment = alignment; text.fontSize = fontSize; text.color = Color.white; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; text.text = "";
    }

    static void SetVisible(UnityEngine.UI.Text text, bool visible)
    { if (text != null && text.gameObject.activeSelf != visible) text.gameObject.SetActive(visible); }

    public void ShowMessage(string message)
    {
        if (transient == null) return;
        transient.text = message; feedbackUntil = Time.unscaledTime + 1.5f; SetVisible(transient, true);
    }

    public void ClearMessage()
    {
        feedbackUntil = 0f;
        if (transient != null) transient.text = "";
        SetVisible(transient, false);
    }

    void Update()
    {
        bool visible = DiagnosticsEnabled && player != null && player.Stamina != null && level != null;
        SetVisible(diagnostics, visible);
        if (visible && diagnostics != null && Time.unscaledTime >= nextDiagnosticUpdate)
        {
            diagnostics.text = "Stamina " + player.Stamina.Current.ToString("0") + "/" + player.Stamina.Maximum.ToString("0") +
                " · " + player.State + " · Suelo " + player.Grounded + "\nNivel " + level.Nivel + " · Semilla " + level.CurrentSeed +
                " · Trozos " + level.CollectedFragments + "/" + level.RequiredFragments;
            nextDiagnosticUpdate = Time.unscaledTime + .2f;
        }
        if (Time.unscaledTime >= feedbackUntil) SetVisible(transient, false);
    }
}
