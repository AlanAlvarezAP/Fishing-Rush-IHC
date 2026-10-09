using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FishMinigameUI : MonoBehaviour
{
    [Header("Componentes UI")]
    [SerializeField] private Image arrowImage;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private TextMeshProUGUI finalStatusText;

    [Header("Configuracion de Colores")]
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color failColor = Color.red;

    private Coroutine arrowAnimationCoroutine;

    private void Awake()
    {
        HideAll();
    }

    public void HideAll()
    {
        if (arrowImage != null) arrowImage.gameObject.SetActive(false);
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
        if (finalStatusText != null) finalStatusText.gameObject.SetActive(false);
        
        if (arrowAnimationCoroutine != null)
        {
            StopCoroutine(arrowAnimationCoroutine);
        }
    }

    public void ShowDirection(int direction)
    {
        HideAll();

        if (arrowImage == null) return;

        arrowImage.gameObject.SetActive(true);

        Vector3 scale = arrowImage.transform.localScale;
        scale.x = direction > 0 ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
        arrowImage.transform.localScale = scale;

        arrowAnimationCoroutine = StartCoroutine(AnimateArrowRoutine());
    }

    public void ShowFeedback(bool success)
    {
        HideAll();

        if (feedbackText == null) return;

        feedbackText.gameObject.SetActive(true);
        feedbackText.text = success ? "ACIERTO" : "FALLO";
        feedbackText.color = success ? successColor : failColor;

        StartCoroutine(HideAfterSeconds(feedbackText.gameObject, 0.8f));
    }

    public void ShowFinalResult(bool caught)
    {
        HideAll();

        if (finalStatusText == null) return;

        finalStatusText.gameObject.SetActive(true);
        finalStatusText.text = caught ? "PEZ ATRAPADO" : "EL PEZ ESCAPO";
        finalStatusText.color = caught ? successColor : failColor;

        StartCoroutine(HideAfterSeconds(finalStatusText.gameObject, 2.0f));
    }

    private IEnumerator AnimateArrowRoutine()
    {
        Vector3 initialPos = arrowImage.transform.localPosition;
        float time = 0f;

        while (true)
        {
            time += Time.deltaTime * 6f;

            Color c = arrowImage.color;
            c.a = Mathf.PingPong(time, 1f);
            arrowImage.color = c;

            float offsetX = Mathf.Sin(time * 2f) * 10f;
            arrowImage.transform.localPosition = initialPos + new Vector3(offsetX, 0f, 0f);

            yield return null;
        }
    }

    private IEnumerator HideAfterSeconds(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null) obj.SetActive(false);
    }
}