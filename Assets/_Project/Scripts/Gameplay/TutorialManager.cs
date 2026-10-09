using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

// El archivo debe llamarse TutorialSlidesUI.cs (igual que la clase).
// Ponlo en el mismo objeto que 'panelTutorial'.
public class TutorialSlidesUI : MonoBehaviour
{
    [Header("UI del Visor")]
    [SerializeField] private Image displayImage;                 // Image donde se muestra la slide
    [SerializeField] private TextMeshProUGUI slideCounterText;   // Opcional: "1 / 3"

    [Header("Las 3 Slides")]
    [SerializeField] private Sprite[] tutorialSlides;            // Tamano 3, arrastra los sprites

    [Header("Proteccion anti doble clic")]
    [SerializeField] private float enableCooldown = 0.3f;

    private int currentSlideIndex = 0;
    private bool wasWiiA = true;   // true al abrir: ignora la A que abrio el panel
    private float enableTime = 0f;

    void OnEnable()
    {
        currentSlideIndex = 0;
        wasWiiA = true;
        enableTime = Time.unscaledTime;
        MostrarSlide();
    }

    void Update()
    {
        if (Time.unscaledTime - enableTime < enableCooldown) return;

        bool avanzar = false;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        if (WiimoteManager.HasWiimote() && WiimoteManager.Wiimotes.Count > 0)
        {
            Wiimote wiimote = WiimoteManager.Wiimotes[0];

            int ret;
            do
            {
                ret = wiimote.ReadWiimoteData();
            } while (ret > 0);

            bool currentWiiA = wiimote.Button.a;
            if (currentWiiA && !wasWiiA) avanzar = true;
            wasWiiA = currentWiiA;
        }
#endif

        // Respaldo para probar en el editor sin el mando
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            avanzar = true;
        }

        if (avanzar) SiguienteSlide();
    }

    private void MostrarSlide()
    {
        if (tutorialSlides == null || tutorialSlides.Length == 0) return;

        if (displayImage != null && currentSlideIndex < tutorialSlides.Length)
        {
            displayImage.sprite = tutorialSlides[currentSlideIndex];
        }

        if (slideCounterText != null)
        {
            slideCounterText.text = (currentSlideIndex + 1) + " / " + tutorialSlides.Length;
        }
    }

    private void SiguienteSlide()
    {
        currentSlideIndex++;

        if (tutorialSlides == null || currentSlideIndex >= tutorialSlides.Length)
        {
            CerrarTutorial();
        }
        else
        {
            MostrarSlide();
        }
    }

    private void CerrarTutorial()
    {
        // Mismo patron que WiimoteCalibrationUI
        MenuMismaEscena menuManager = FindObjectOfType<MenuMismaEscena>();
        if (menuManager != null)
        {
            menuManager.CerrarPanelTutorial();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}