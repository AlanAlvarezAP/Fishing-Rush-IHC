using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using WiimoteApi;
#endif

public class WiimoteCalibrationUI : MonoBehaviour
{
    [Header("Elementos de UI")]
    [SerializeField] private GameObject confirmationPopup;
    [SerializeField] private float displayDuration = 1.5f;

    private bool isShowingPopup = false;
    private bool wasWiiA = false;

    void Start()
    {
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

            if (currentWiiA && !wasWiiA)
            {
                StartCoroutine(CalibrateAndCloseRoutine());
            }
            wasWiiA = currentWiiA;
        }
#endif

        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(CalibrateAndCloseRoutine());
        }
    }

    private IEnumerator CalibrateAndCloseRoutine()
    {
        isShowingPopup = true;

        Debug.Log("[CALIBRACION] Wiimote calibrado exitosamente");

        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(true);
        }

        yield return new WaitForSeconds(displayDuration);

        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(false);
        }

        MenuMismaEscena menuManager = FindObjectOfType<MenuMismaEscena>();
        if (menuManager != null)
        {
            menuManager.CerrarPanelWiimote();
        }
        else
        {
            gameObject.SetActive(false);
        }

        isShowingPopup = false;
    }
}