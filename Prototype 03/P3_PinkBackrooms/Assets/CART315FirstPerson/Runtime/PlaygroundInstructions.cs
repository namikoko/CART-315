using UnityEngine;

namespace Cart315.FirstPerson
{
    // Used only by the demonstration scene; the player prefab has no UI dependency.
    public class PlaygroundInstructions : MonoBehaviour
    {
        private GUIStyle _title, _body;

        private void OnGUI()
        {
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
                _body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            }
            GUI.Box(new Rect(16, 16, 440, 155), GUIContent.none);
            GUI.Label(new Rect(32, 26, 410, 38), "CART315 / FIRST PERSON", _title);
            GUI.Label(new Rect(32, 68, 408, 94),
                "WASD  Move     Shift  Sprint     Space  Jump\n" +
                "Click  Capture mouse     Escape  Release\n" +
                "Amber: stairs   Blue: ramp + low ceiling\n" +
                "Footsteps are silent until you add your own clips.", _body);
            if (Cursor.lockState != CursorLockMode.Locked)
                GUI.Label(new Rect(Screen.width / 2f - 120, Screen.height / 2f, 280, 35), "Click to explore", _title);
        }
    }
}
