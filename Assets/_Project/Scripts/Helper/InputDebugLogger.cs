using UnityEngine;

/// <summary>
/// Script temporal de debug. Ponlo en cualquier GameObject de la escena
/// (activo desde el inicio) y presiona los botones del control Shinecon.
/// Cada botón que detecte, lo imprime con Debug.Log — deberías verlo en
/// la consola in-game que ya tienes armada en el celular.
///
/// Bórralo o desactívalo cuando ya sepas qué KeyCode usa cada botón.
/// </summary>
public class InputDebugLogger : MonoBehaviour
{
    private void Update()
    {
        // Detecta cualquier tecla/botón presionado este frame.
        if (Input.anyKeyDown)
        {
            foreach (KeyCode code in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(code))
                {
                    Debug.Log($"[InputDebug] KeyCode detectado: {code}");
                }
            }
        }

        // Por si acaso también manda texto/caracteres imprimibles.
        if (!string.IsNullOrEmpty(Input.inputString))
        {
            Debug.Log($"[InputDebug] inputString: '{Input.inputString}'");
        }

        // Algunos controles Bluetooth se reportan como joystick/gamepad
        // en vez de teclado. Esto revisa ejes de joystick conectados.
        string[] joystickNames = Input.GetJoystickNames();
        if (joystickNames.Length > 0)
        {
            foreach (var jName in joystickNames)
            {
                if (!string.IsNullOrEmpty(jName))
                {
                    Debug.Log($"[InputDebug] Joystick detectado: {jName}");
                }
            }
        }
    }
}