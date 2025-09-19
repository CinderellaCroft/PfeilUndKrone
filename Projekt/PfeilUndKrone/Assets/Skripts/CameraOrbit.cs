using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    public Transform target;
    public float speed = 20f;

    void Update()
    {
        if (target != null)
        {
            transform.RotateAround(target.position, Vector3.up, speed * Time.deltaTime);
        }
    }
}
