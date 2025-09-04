using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    // Das Zielobjekt, um das die Kamera kreisen soll.
    // Ziehe dein Objekt im Inspector hier hinein.
    public Transform target;

    // Die Geschwindigkeit der Drehung.
    public float speed = 20f;

    void Update()
    {
        // Prüfe, ob ein Ziel zugewiesen ist.
        if (target != null)
        {
            // Rotiere die Kamera um die Position des Ziels.
            // Parameter:
            // 1. target.position: Der Punkt, um den rotiert wird.
            // 2. Vector3.up: Die Rotationsachse (die y-Achse für eine horizontale Drehung).
            // 3. speed * Time.deltaTime: Der Winkel, um den pro Frame gedreht wird.
            transform.RotateAround(target.position, Vector3.up, speed * Time.deltaTime);
        }
    }
}
