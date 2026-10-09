using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

public class WiimoteCalibrationUI : MonoBehaviour
{
    [Header("Elementos de UI")]
    [SerializeField] private GameObject confirmationPopup; // Arrastra aquí la cajita/texto que dice "Calibración Confirmada"
    [SerializeField] private float displayDuration = 1.5f; // Tiempo que se queda visible el aviso antes de cerrar

    private bool isShowingPopup = false;
    private bool wasWiiA = false;

    void Start()
    {
        // Asegurarse de que el popup empiece apagado al abrir el panel
        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(false);
        }
    }

    void Update()
    {
        if (isShowingPopup) return;

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

            // Detecta cuando presionas el botón A del Wiimote
            if (currentWiiA && !wasWiiA)
            {
                StartCoroutine(CalibrateAndCloseRoutine());
            }
            wasWiiA = currentWiiA;
        }
#endif

        // Botón de respaldo (Barra Espaciadora) para probar en el editor sin el mando físico
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(CalibrateAndCloseRoutine());
        }
    }

    private IEnumerator CalibrateAndCloseRoutine()
    {
        isShowingPopup = true;

        Debug.Log("[CALIBRACIÓN] ¡Wiimote calibrado exitosamente!");

        // 1. Mostrar el aviso temporal de confirmación
        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(true);
        }

        // 2. Esperar el tiempo configurado (1.5 segundos)
        yield return new WaitForSeconds(displayDuration);

        // 3. Ocultar el popup
        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(false);
        }

        // 4. Buscar el menú en la escena y ordenar que se cierre el panel de la imagen de Wiimote
        MenuMismaEscena menuManager = FindObjectOfType<MenuMismaEscena>();
        if (menuManager != null)
        {
            menuManager.CerrarPanelWiimote();
        }
        else
        {
            // Plan de respaldo: apagar este mismo objeto si no encuentra el gestor
            gameObject.SetActive(false);
        }

        isShowingPopup = false;
    }
}