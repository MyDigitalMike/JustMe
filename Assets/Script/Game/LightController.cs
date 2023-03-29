using System.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
public class LightController : MonoBehaviour
{
    public float delayTime = 30f;
    public float flickerTime = 5f;
    public float minFlickerIntensity = 0.1f;
    public float maxFlickerIntensity = 0.8f;
    public float lightRadius = 5f;
    private Light2D pointLight;
    private bool lightOn;
    private bool canTurnOnLight = false;
    void Start()
    {
        pointLight = GetComponent<Light2D>();
        lightOn = false;
    }
    void Update()
    {
    }
    void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Jugador") && !lightOn)
        {
            SetVisible();
            pointLight.pointLightOuterRadius = lightRadius;
            StartCoroutine(TurnOnLight());
        }
    }
    void SetVisible()
    {
        this.GetComponent<Renderer>().enabled = false;
        this.GetComponent<Collider2D>().enabled = false;
    }
    IEnumerator TurnOnLight()
    {
        lightOn = true;
        pointLight.enabled = true;
        float timer = 0f;
        float blinkTimer = 0f;
        Color originalColor = pointLight.color;
        Color blinkColor = Color.red;
        while (timer < delayTime)
        {
            if (timer >= delayTime - 5f)
            {
                blinkTimer += Time.deltaTime;
                if (blinkTimer >= 0.1f)
                {
                    pointLight.color = (pointLight.color == originalColor) ? blinkColor : originalColor;
                    blinkTimer = 0f;
                }
            }
            timer += Time.deltaTime;
            yield return null;
        }
        pointLight.enabled = false;
        lightOn = false;
    }
}

