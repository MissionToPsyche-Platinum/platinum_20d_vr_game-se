using UnityEngine;

public class ShootBall : MonoBehaviour
{
    void Start()
    {
        GetComponent<Rigidbody>().AddForce(new Vector3(0, 0, -4), ForceMode.VelocityChange);
    }
}
