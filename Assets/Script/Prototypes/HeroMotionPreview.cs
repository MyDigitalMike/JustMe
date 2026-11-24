using UnityEngine;

// Visual draft only: never attached to the actual Player or its physics controller.
public sealed class HeroMotionPreview : MonoBehaviour
{
    public Animator animator;
    public GameObject ropeGuide, floorGuide;
    public int Selected { get; private set; }
    static readonly string[] States = { "Run", "Jump", "DoubleJump", "Climb" };
    static readonly string[] Labels = { "Correr", "Salto", "Doble salto", "Trepar" };
    void Start() { Select(0); }
    public void Select(int index)
    {
        if (index < 0 || index >= States.Length || animator == null) return;
        Selected = index; animator.Play("Base Layer." + States[index], 0, 0);
        if (ropeGuide != null) ropeGuide.SetActive(index == 3);
        if (floorGuide != null) floorGuide.SetActive(index != 3);
    }
    void OnGUI()
    {
        var previous = GUI.matrix;
        float scale = Mathf.Clamp(Screen.width / 900f, .65f, 1.2f);
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        GUI.Box(new Rect(10, 10, 610, 66), GUIContent.none);
        GUI.Label(new Rect(20, 15, 590, 22), "BORRADOR VISUAL · Animaciones nuevas; personaje del juego sin sustituir");
        for (int i = 0; i < 4; i++) if (GUI.Button(new Rect(20 + i * 145, 42, 138, 24), Labels[i])) Select(i);
        GUI.matrix = previous;
    }
}
