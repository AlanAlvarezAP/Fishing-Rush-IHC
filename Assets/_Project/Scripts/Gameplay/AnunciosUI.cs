using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AnunciosUI : MonoBehaviour
{
    public static AnunciosUI Instance;

    [Header("Referencia al Tile de Mensajes (Solo Victoria/Derrota)")]
    public GameObject tileAnuncios;
    public TextMeshProUGUI txtAnuncio;

    [Header("Imagen Central del Gesto (Independiente)")]
    public Image imgGestoCentral;
    public Sprite iconHorizontal;
    public Sprite iconVertical;
    public Sprite iconCircle;
    public Sprite iconCross;

    [Header("Colores del Texto")]
    public Color colorVictoria = Color.green;
    public Color colorDerrota = Color.red;

    private Coroutine rutinaAnuncio;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (tileAnuncios != null) tileAnuncios.SetActive(false);
        if (imgGestoCentral != null) imgGestoCentral.gameObject.SetActive(false);
    }

    // --- 1. MOSTRAR SOLO LA IMAGEN DEL GESTO (El Tile/Recuadro se queda APAGADO) ---
    public void MostrarGestoRequerido(FishCube.FishingGesture gesto, float duracion = 1.5f)
    {
        Sprite spriteGesto = ObtenerSpriteGesto(gesto);
        if (spriteGesto == null || imgGestoCentral == null) return;

        if (rutinaAnuncio != null) StopCoroutine(rutinaAnuncio);
        rutinaAnuncio = StartCoroutine(RutinaMostrarGesto(spriteGesto, duracion));
    }

    private IEnumerator RutinaMostrarGesto(Sprite spriteGesto, float duracion)
    {
        // Garantizar que el recuadro de texto esté apagado
        if (tileAnuncios != null) tileAnuncios.SetActive(false);

        // Encender ÚNICAMENTE la imagen del gesto
        imgGestoCentral.sprite = spriteGesto;
        imgGestoCentral.transform.SetAsLastSibling();
        imgGestoCentral.gameObject.SetActive(true);

        yield return new WaitForSeconds(duracion);

        // Apagar la imagen
        imgGestoCentral.gameObject.SetActive(false);
    }

    // --- 2. MOSTRAR MENSAJES DE TEXTO (AQUÍ SÍ SE ENCIENDE EL RECUADRO) ---
    public void MostrarMensaje(string texto, Color colorTexto, float duracion = 1.5f)
    {
        if (tileAnuncios == null) return;

        if (rutinaAnuncio != null) StopCoroutine(rutinaAnuncio);
        rutinaAnuncio = StartCoroutine(RutinaMostrarTexto(texto, colorTexto, duracion));
    }

    private IEnumerator RutinaMostrarTexto(string texto, Color colorTexto, float duracion)
    {
        // Apagar la imagen del gesto
        if (imgGestoCentral != null) imgGestoCentral.gameObject.SetActive(false);

        if (txtAnuncio != null)
        {
            txtAnuncio.text = texto;
            txtAnuncio.color = colorTexto;
            txtAnuncio.gameObject.SetActive(true);
        }

        // Encender el cuadro con el mensaje final
        tileAnuncios.transform.SetAsLastSibling();
        tileAnuncios.SetActive(true);

        yield return new WaitForSeconds(duracion);

        tileAnuncios.SetActive(false);
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

    public void MostrarVictoria() => MostrarMensaje("¡PEZ CAPTURADO!", colorVictoria, 2.0f);
    public void MostrarDerrota() => MostrarMensaje("¡EL PEZ ESCAPÓ!", colorDerrota, 2.0f);
}