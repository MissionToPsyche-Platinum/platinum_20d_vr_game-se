using UnityEngine;

public class CoffeeLiquidController : MonoBehaviour
{
    public Transform mug;
    public ParticleSystem coffeePour;

    public float pourAngle = 60f;

    private Vector3 uprightDirection;

    public float pourSpeed = 0.1f;

    private Vector3 initialScale;
    private float coffeeRemaining = 1f;

    void Start()
    {
        uprightDirection = mug.forward;

        coffeePour.Stop();
        initialScale = transform.localScale;
    }

    void Update()
    {
        if (mug == null || coffeePour == null)
            return;

        // Measure rotation relative to the starting orientation.
        float tilt = Vector3.Angle(mug.forward, Vector3.up);

        if (tilt >= pourAngle && coffeeRemaining > 0f)
        {
            if (!coffeePour.isPlaying)
                coffeePour.Play();

            coffeeRemaining -= pourSpeed * Time.deltaTime;

            coffeeRemaining = Mathf.Clamp01(coffeeRemaining);

            Vector3 newScale = initialScale;
            newScale.y = initialScale.y * coffeeRemaining;

            transform.localScale = newScale;
        }
        else
        {
            if (coffeePour.isPlaying)
                coffeePour.Stop();
        }
    }
}
