using UnityEngine;
using TMPro;

/// <summary>
/// NUEVO (minas). Aviso del conductor: pantalla roja + "Conduccion inhabilitada 4.2 s" mientras
/// el bote esta inmovilizado por una mina.
/// Va en un objeto SIEMPRE activo dentro de Canvas_Boat; lo que se enciende y apaga es 'panel'.
/// No usa RPCs: lee BoatController.SegundosInmovilRestantes, que sale de una NetworkVariable.
/// </summary>
public class AvisoBoteInmovil : MonoBehaviour
{
    [Tooltip("Hijo con el fondo rojo y el texto (se enciende solo mientras dure la mina)")]
    public GameObject panel;
    public TextMeshProUGUI texto;
    [Tooltip("Sin tildes ni signos de apertura: la fuente del juego puede no tenerlos")]
    public string mensaje = "MINA ACTIVADA!\nConduccion inhabilitada";

    private BoatController bote;
    private float proximaBusqueda = 0f;

    private void OnEnable()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        if (panel == null) return;

        if (bote == null && Time.unscaledTime >= proximaBusqueda)
        {
            proximaBusqueda = Time.unscaledTime + 1f;
            bote = FindObjectOfType<BoatController>();
        }

        float segundos = bote != null ? bote.SegundosInmovilRestantes : 0f;
        bool mostrar = segundos > 0f;

        if (panel.activeSelf != mostrar) panel.SetActive(mostrar);
        if (mostrar && texto != null) texto.text = $"{mensaje}\n{segundos:0.0} s";
    }
}
