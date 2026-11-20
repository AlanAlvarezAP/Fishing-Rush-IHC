using UnityEngine;

/// <summary>
/// Datos de la zona del tutorial del conductor (en su mitad del lago). Va en la raiz
/// Tutorial_Conductor, que crea Fishing Rush > Crear tutorial del conductor.
/// La raiz solo se enciende durante el tutorial del conductor (en todos los dispositivos), y de
/// sus hijos Paso1..Paso4 solo se ve el del paso actual.
///
/// Los indices de los arreglos por paso son 0..3 = pasos 1..4.
/// </summary>
public class ZonaTutorialConductor : MonoBehaviour
{
    [Header("Por paso (indice 0 = paso 1 ... 3 = paso 4)")]
    [Tooltip("Donde se coloca el bote al empezar cada paso (su rotacion Y es hacia donde mira)")]
    public Transform[] iniciosBote = new Transform[4];
    [Tooltip("Caja de la que el bote no puede salir en cada paso (pasillo / zona abierta)")]
    public BoxCollider[] areasBote = new BoxCollider[4];
    [Tooltip("Objetos visibles de cada paso (Paso1..Paso4)")]
    public GameObject[] visualesPaso = new GameObject[4];

    [Header("Paso 2: minas")]
    [Tooltip("Fila de minas que tapa todo el pasillo: el bote choca si o si. Al explotar una, se quitan las demas")]
    public Transform[] minasObligatoriasPaso2;
    [Tooltip("Minas que si se pueden esquivar")]
    public Transform[] minasPaso2;
    public Transform metaPaso2;

    [Header("Paso 3: tiburones")]
    public Transform[] tiburonesPaso3;
    public Transform metaPaso3;

    [Header("Paso 4: acercar al pescador a los peces")]
    [Tooltip("Centro de cada circulo (ahi aparece un pez)")]
    public Transform[] circulosPaso4;
    public Transform[] minasPaso4;
    public Transform[] tiburonesPaso4;

    [Header("Medidas")]
    [Tooltip("Metros: el bote cuenta como dentro del circulo si su centro esta a menos de esto")]
    public float radioCirculo = 5f;
    [Tooltip("Metros: el bote llego a la meta si su centro esta a menos de esto")]
    public float radioMeta = 6f;

    public Transform InicioDe(int paso) => Elemento(iniciosBote, paso - 1);
    public BoxCollider AreaDe(int paso) => Elemento(areasBote, paso - 1);

    /// <summary>Enciende la zona y solo los objetos del paso (1..4). 0 o 5 = todo apagado.</summary>
    public void MostrarPaso(int paso)
    {
        bool activa = paso >= 1 && paso <= 4;
        if (gameObject.activeSelf != activa) gameObject.SetActive(activa);
        if (!activa || visualesPaso == null) return;

        for (int i = 0; i < visualesPaso.Length; i++)
        {
            if (visualesPaso[i] == null) continue;
            bool este = i == paso - 1;
            if (visualesPaso[i].activeSelf != este) visualesPaso[i].SetActive(este);
        }
    }

    private static T Elemento<T>(T[] arreglo, int i) where T : class
    {
        if (arreglo == null || i < 0 || i >= arreglo.Length) return null;
        return arreglo[i];
    }
}
