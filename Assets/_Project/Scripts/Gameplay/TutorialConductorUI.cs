using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tutorial del conductor (solo en la tablet, Canvas Overlay). Lo crea
/// Fishing Rush > Crear tutorial del conductor y lo abre MenuMismaEscena (boton Tutorial).
///
/// Cada panel explica algo; con "Continuar" empieza su prueba y al cumplirla pasa al siguiente:
///   1. Conducir: avanzar, retroceder, izquierda y derecha.
///   2. Minas: llegar a la meta por un pasillo (la primera fila de minas se choca si o si).
///   3. Tiburones: llegar a la meta por otro pasillo (un tiburon embiste si o si).
///   4. Peces: quedarse unos segundos dentro de cada uno de los 3 circulos.
///   5. Completado: se muestra unos segundos y vuelve al menu.
///
/// La tablet revisa las pruebas (sus botones, la inclinacion y la posicion del bote, que llega por
/// red). La PC solo prepara cada paso (bote, minas, tiburones, peces): ver MenuMismaEscena.TutorialConductor.
/// </summary>
public class TutorialConductorUI : MonoBehaviour
{
    [Header("Slides (Slide_1..5). Los 4 primeros tienen boton Continuar")]
    [SerializeField] private GameObject[] slides;

    [Header("Progreso (se ve durante las pruebas)")]
    [SerializeField] private GameObject panelProgreso;
    [SerializeField] private TMP_Text textoProgreso;

    [Header("Tiempos")]
    [Tooltip("Segundos que se muestra 'Completado!' antes del siguiente panel")]
    [SerializeField] private float segundosEntrePasos = 1.5f;
    [Tooltip("Segundos que se muestra el panel final antes de volver al menu")]
    [SerializeField] private float segundosSlideFinal = 3f;
    [Tooltip("Paso 4: segundos que hay que quedarse dentro de cada circulo")]
    [SerializeField] private float segundosEnCirculo = 5f;

    private const int PASOS_CON_PRUEBA = 4;

    private enum Estado { Slide, Probando, Esperando }
    private Estado estado = Estado.Slide;
    private int paso = 1; // 1..5

    /// <summary>Paso con el que arranca al abrirse (1, o el que iba si se reconecto). Se usa una vez.</summary>
    public int PasoAlAbrir { get; set; } = 1;

    private MenuMismaEscena menu;
    private BoatController bote;
    private ZonaTutorialConductor zona;
    private bool botonesConectados = false;

    // Paso 1
    private bool hizoAvanzar, hizoRetroceder, hizoIzquierda, hizoDerecha;
    // Paso 4
    private float[] tiempoEnCirculo;
    private bool[] circuloHecho;

    // ---------------- CICLO DE VIDA ----------------
    private void Awake()
    {
        ConectarBotones();
    }

    private void OnEnable()
    {
        ConectarBotones();

        menu = FindObjectOfType<MenuMismaEscena>();
        bote = FindObjectOfType<BoatController>();
        zona = menu != null ? menu.zonaTutorialConductor : null;

        if (textoProgreso != null) FuenteSegura.Aplicar(textoProgreso);

        paso = Mathf.Clamp(PasoAlAbrir, 1, PASOS_CON_PRUEBA);
        PasoAlAbrir = 1;
        MostrarSlide();
    }

    /// <summary>
    /// Se cayo la red y volvio (la app siguio abierta): la PC habia terminado el tutorial, se le
    /// vuelve a pedir el paso actual (el bote vuelve al inicio de ese paso).
    /// </summary>
    public void ReenviarPaso()
    {
        if (menu == null) menu = FindObjectOfType<MenuMismaEscena>();
        if (menu == null) return;

        Debug.Log($"[TUTORIAL CONDUCTOR] Reconectado: se vuelve a preparar el paso {paso}.");
        menu.PedirPasoTutorialConductor(paso, estado == Estado.Probando);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        MostrarSoloSlide(-1);
        if (panelProgreso != null) panelProgreso.SetActive(false);
        if (menu != null) menu.SetControlesTutorialConductor(false);
    }

    private void ConectarBotones()
    {
        if (botonesConectados || slides == null) return;
        botonesConectados = true;

        foreach (GameObject slide in slides)
        {
            if (slide == null) continue;
            foreach (Button b in slide.GetComponentsInChildren<Button>(true)) b.onClick.AddListener(Continuar);
            // Fuente que se ve bien en la tablet (la misma que usan los botones del bote)
            foreach (TMP_Text t in slide.GetComponentsInChildren<TMP_Text>(true)) FuenteSegura.Aplicar(t);
        }
    }

    // ---------------- PANELES ----------------
    private void MostrarSlide()
    {
        if (slides == null || paso - 1 >= slides.Length || slides[paso - 1] == null)
        {
            // Sin panel (ej. falta el 5): se termina directamente
            Finalizar(paso > PASOS_CON_PRUEBA);
            return;
        }

        MostrarSoloSlide(paso - 1);
        if (panelProgreso != null) panelProgreso.SetActive(false);
        if (menu != null)
        {
            menu.SetControlesTutorialConductor(false);
            menu.PedirPasoTutorialConductor(paso, false); // la PC prepara el paso (bote quieto)
        }

        if (paso > PASOS_CON_PRUEBA)
        {
            estado = Estado.Esperando;
            StartCoroutine(SlideFinalRoutine());
        }
        else
        {
            estado = Estado.Slide;
        }
    }

    private void MostrarSoloSlide(int indice)
    {
        if (slides == null) return;
        for (int i = 0; i < slides.Length; i++)
        {
            if (slides[i] != null && slides[i].activeSelf != (i == indice)) slides[i].SetActive(i == indice);
        }
    }

    /// <summary>Boton Continuar de cada panel: empieza su prueba.</summary>
    public void Continuar()
    {
        if (estado != Estado.Slide) return;

        estado = Estado.Probando;
        MostrarSoloSlide(-1);
        if (panelProgreso != null) panelProgreso.SetActive(true);

        hizoAvanzar = hizoRetroceder = hizoIzquierda = hizoDerecha = false;
        PrepararCirculos();

        if (menu != null)
        {
            menu.PedirPasoTutorialConductor(paso, true); // la PC deja mover el bote
            menu.SetControlesTutorialConductor(true);
        }

        ActualizarTexto("");
        Debug.Log($"[TUTORIAL CONDUCTOR] Empieza la prueba del paso {paso}.");
    }

    private IEnumerator PasoCompletadoRoutine()
    {
        estado = Estado.Esperando;
        ActualizarTexto("Completado!");
        if (menu != null) menu.SetControlesTutorialConductor(false);
        Debug.Log($"[TUTORIAL CONDUCTOR] Paso {paso} completado.");

        yield return new WaitForSeconds(segundosEntrePasos);

        paso++;
        MostrarSlide();
    }

    private IEnumerator SlideFinalRoutine()
    {
        yield return new WaitForSeconds(segundosSlideFinal);
        Finalizar(true);
    }

    private void CompletarPaso()
    {
        if (estado != Estado.Probando) return;
        StartCoroutine(PasoCompletadoRoutine());
    }

    private void Finalizar(bool completado)
    {
        Debug.Log(completado ? "[TUTORIAL CONDUCTOR] Tutorial completado." : "[TUTORIAL CONDUCTOR] Tutorial cancelado.");
        if (menu != null) menu.TerminarTutorialConductorLocal(completado); // apaga este panel
        else gameObject.SetActive(false);
    }

    // ---------------- PRUEBAS ----------------
    private void Update()
    {
        // Boton atras de Android (= Escape): sale del tutorial sin completarlo
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Finalizar(false);
            return;
        }

        if (estado != Estado.Probando) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        // Pruebas en el editor: N salta la prueba actual
        if (Input.GetKeyDown(KeyCode.N))
        {
            Debug.Log("[TUTORIAL CONDUCTOR] Tecla N: prueba saltada.");
            CompletarPaso();
            return;
        }
#endif

        if (bote == null) bote = FindObjectOfType<BoatController>();

        switch (paso)
        {
            case 1: ActualizarPasoConducir(); break;
            case 2: ActualizarPasoMeta(zona != null ? zona.metaPaso2 : null, "Esquiva las minas"); break;
            case 3: ActualizarPasoMeta(zona != null ? zona.metaPaso3 : null, "Esquiva los tiburones"); break;
            case 4: ActualizarPasoCirculos(); break;
        }
    }

    private void ActualizarPasoConducir()
    {
        if (bote != null)
        {
            Vector2 input = bote.LeerInputConductor();
            if (input.y > 0.5f) hizoAvanzar = true;
            if (input.y < -0.5f) hizoRetroceder = true;
            if (input.x < -0.3f) hizoIzquierda = true;
            if (input.x > 0.3f) hizoDerecha = true;
        }

        int hechos = (hizoAvanzar ? 1 : 0) + (hizoRetroceder ? 1 : 0) + (hizoIzquierda ? 1 : 0) + (hizoDerecha ? 1 : 0);

        ActualizarTexto(
            $"Movimientos {hechos}/4\n" +
            $"{Marca(hizoAvanzar)} Avanzar    {Marca(hizoRetroceder)} Retroceder\n" +
            $"{Marca(hizoIzquierda)} Girar a la izquierda    {Marca(hizoDerecha)} Girar a la derecha");

        if (hechos >= 4) CompletarPaso();
    }

    private void ActualizarPasoMeta(Transform meta, string consejo)
    {
        if (bote == null || meta == null)
        {
            ActualizarTexto("Llega a la meta");
            return;
        }

        float dist = DistanciaPlana(bote.transform.position, meta.position);
        float radio = zona != null ? zona.radioMeta : 6f;

        if (dist <= radio)
        {
            CompletarPaso();
            return;
        }

        string texto = $"Llega a la meta (verde): {dist:0} m\n{consejo}";
        string efecto = TextoEfecto();
        if (efecto.Length > 0) texto += "\n" + efecto;
        ActualizarTexto(texto);
    }

    private void PrepararCirculos()
    {
        int n = zona != null && zona.circulosPaso4 != null ? zona.circulosPaso4.Length : 0;
        tiempoEnCirculo = new float[n];
        circuloHecho = new bool[n];

        // Los circulos que se ocultaron la vez anterior vuelven a verse
        for (int i = 0; i < n; i++)
        {
            if (zona.circulosPaso4[i] != null) zona.circulosPaso4[i].gameObject.SetActive(true);
        }
    }

    private void ActualizarPasoCirculos()
    {
        if (zona == null || zona.circulosPaso4 == null || zona.circulosPaso4.Length == 0 || circuloHecho == null)
        {
            ActualizarTexto("Acerca el bote a los peces");
            return;
        }

        int total = zona.circulosPaso4.Length;
        int hechos = 0;
        string dentro = "";

        for (int i = 0; i < total; i++)
        {
            if (circuloHecho[i]) { hechos++; continue; }

            Transform c = zona.circulosPaso4[i];
            if (c == null || bote == null) continue;

            bool adentro = DistanciaPlana(bote.transform.position, c.position) <= zona.radioCirculo;
            if (adentro)
            {
                tiempoEnCirculo[i] += Time.deltaTime;
                dentro = $"Quedate en el circulo: {tiempoEnCirculo[i]:0.0} / {segundosEnCirculo:0} s";

                if (tiempoEnCirculo[i] >= segundosEnCirculo)
                {
                    circuloHecho[i] = true;
                    hechos++;
                    c.gameObject.SetActive(false); // solo en la tablet: ese pez ya esta
                }
            }
            else
            {
                tiempoEnCirculo[i] = 0f; // hay que quedarse seguido
            }
        }

        if (hechos >= total)
        {
            CompletarPaso();
            return;
        }

        string texto = $"Peces {hechos}/{total}\n" + (dentro.Length > 0 ? dentro : "Lleva el bote dentro de un circulo");
        string efecto = TextoEfecto();
        if (efecto.Length > 0) texto += "\n" + efecto;
        ActualizarTexto(texto);
    }

    /// <summary>Lo que le esta pasando al bote (mina / tiburon), para que se entienda el efecto.</summary>
    private string TextoEfecto()
    {
        if (bote == null) return "";
        if (bote.EstaInmovilizado) return $"Chocaste una mina! Bote inmovilizado: {bote.SegundosInmovilRestantes:0} s";
        if (bote.EstaTambaleando) return "Un tiburon te embistio! El bote se tambalea";
        return "";
    }

    // ---------------- UTILIDADES ----------------
    private void ActualizarTexto(string texto)
    {
        if (textoProgreso != null && textoProgreso.text != texto) textoProgreso.text = texto;
    }

    private static string Marca(bool hecho) => hecho ? "[OK]" : "[  ]";

    private static float DistanciaPlana(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
