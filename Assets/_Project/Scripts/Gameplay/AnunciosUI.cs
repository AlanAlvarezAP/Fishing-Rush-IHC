using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AnunciosUI : MonoBehaviour
{
    public static AnunciosUI Instance;

    [Header("Referencia al Tile de Anuncios")]
    public GameObject tileAnuncios;
    public TextMeshProUGUI txtAnuncio;

    [Header("Imagen Central del Gesto")]
    public Image imgGestoCentral;
    public Sprite iconHorizontal;
    public Sprite iconVertical;
    public Sprite iconCircle;
    public Sprite iconCross;


    [Header("Colores del Texto")]
    public Color colorInicio = Color.yellow;
    public Color colorVictoria = Color.green;
    public Color colorDerrota = Color.red;

    private Coroutine rutinaAnuncio;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (tileAnuncios != null) tileAnuncios.SetActive(false);
        if (imgGestoCentral != null) imgGestoCentral.gameObject.SetActive(false);
    }

    public void MostrarMensaje(string texto, Color colorTexto, float duracion = 1.5f)
    {
        if (tileAnuncios == null || txtAnuncio == null) return;

        if (rutinaAnuncio != null) StopCoroutine(rutinaAnuncio);
        rutinaAnuncio = StartCoroutine(RutinaMostrar(texto, colorTexto, duracion));
    }

    private IEnumerator RutinaMostrar(string texto, Color colorTexto, float duracion)
    {
        txtAnuncio.text = texto;
        txtAnuncio.color = colorTexto;

        tileAnuncios.transform.SetAsLastSibling();
        tileAnuncios.SetActive(true);

        yield return new WaitForSeconds(duracion);

        tileAnuncios.SetActive(false);
        if (imgGestoCentral != null) imgGestoCentral.gameObject.SetActive(false);
    }

    public void MostrarGestoRequerido(FishCube.FishingGesture gesto, float duracion = 1.5f)
    {
        // 1. Asignar la foto/sprite del movimiento
        Sprite spriteGesto = ObtenerSpriteGesto(gesto);
        if (imgGestoCentral != null && spriteGesto != null)
        {
            imgGestoCentral.sprite = spriteGesto;
            imgGestoCentral.gameObject.SetActive(true);
        }


        // 3. Mostrar texto descriptivo
        MostrarMensaje($"¡HAZ EL PATRÓN!", colorInicio, duracion);
    }

    private Sprite ObtenerSpriteGesto(FishCube.FishingGesture gesto)
    {
        switch (gesto)
        {
            case FishCube.FishingGesture.Horizontal: return iconHorizontal;
            case FishCube.FishingGesture.Vertical: return iconVertical;
            case FishCube.FishingGesture.Circle: return iconCircle;
            case FishCube.FishingGesture.Cross: return iconCross;
            default: return null;
        }
    }


    // Métodos de disparo rápido
    public void MostrarHazPatron() => MostrarMensaje("¡HAZ EL PATRON!", colorInicio, 1.5f);
    public void MostrarVictoria() => MostrarMensaje("¡PEZ CAPTURADO!", colorVictoria, 2.0f);
    public void MostrarDerrota() => MostrarMensaje("¡EL PEZ ESCAPÓ!", colorDerrota, 2.0f);
}