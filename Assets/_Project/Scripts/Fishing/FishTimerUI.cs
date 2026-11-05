using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FishTimerUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Image iconImage;

    [Header("Color de la Barra")]
    [SerializeField] private Gradient colorGradient;

    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;

        if (colorGradient == null || colorGradient.colorKeys.Length == 0)
        {
            colorGradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[3];
            colorKeys[0] = new GradientColorKey(Color.red, 0.0f);
            colorKeys[1] = new GradientColorKey(Color.yellow, 0.5f);
            colorKeys[2] = new GradientColorKey(Color.green, 1.0f);

            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);

            colorGradient.SetKeys(colorKeys, alphaKeys);
        }
    }

    void LateUpdate()
    {
        Camera activeCam = Camera.main;

        if (activeCam == null || !activeCam.enabled || !activeCam.gameObject.activeInHierarchy)
        {
            foreach (Camera c in Camera.allCameras)
            {
                if (c != null && c.enabled && c.gameObject.activeInHierarchy)
                {
                    activeCam = c;
                    break;
                }
            }
        }

        if (activeCam != null)
        {
            transform.rotation = activeCam.transform.rotation;
        }
    }

    public void UpdateTimer(float currentLifeTime, float maxLifeTime)
    {
        if (fillImage == null || maxLifeTime <= 0) return;

        float remainingRatio = Mathf.Clamp01(1.0f - (currentLifeTime / maxLifeTime));

        fillImage.fillAmount = remainingRatio;
        fillImage.color = colorGradient.Evaluate(remainingRatio);
    }

    public void SetFishIcon(Sprite icon)
    {
        if (iconImage != null && icon != null)
        {
            iconImage.sprite = icon;
        }
    }
}