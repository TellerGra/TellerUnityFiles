using UnityEngine;
using TMPro;
using System.Linq;

public class AdvancedFPSCounter : MonoBehaviour
{
    [Header("Set output text field")]
    public TextMeshProUGUI text;

    [Header("FPS settings")]
    public float updateInterval = 0.5f; // Seconds between FPS UI updates

    private float timer;
    private const int bufferSize = 600;
    private float[] frameTimes = new float[bufferSize];
    private int frameIndex = 0;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        frameTimes[frameIndex] = dt;
        frameIndex = (frameIndex + 1) % bufferSize;

        timer += Time.unscaledDeltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;

            int samples = frameTimes.Count(x => x > 0);
            if (samples > 0)
            {
                float avgFPS = samples / frameTimes.Where(x => x > 0).Sum();
                var sorted = frameTimes.Where(x => x > 0).OrderByDescending(x => x).ToArray();
                int onePercentCount = Mathf.Max(1, samples / 100);
                var onePercentAvgDT = sorted.Take(onePercentCount).Average();
                float onePercentLowFPS = 1f / onePercentAvgDT;
                text.text = $"FPS: {1f/dt:F1}\nAvg: {avgFPS:F1}\n1% Low: {onePercentLowFPS:F1}";
            }
        }
    }
}

