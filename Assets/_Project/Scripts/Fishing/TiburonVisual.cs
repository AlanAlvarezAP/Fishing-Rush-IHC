using UnityEngine;

/// <summary>
/// NUEVO (tiburon). Controla las animaciones del modelo del tiburon (asset "3D shark").
/// Va en el objeto del modelo, dentro del prefab Tiburon.
/// - Normalmente reproduce la animacion de nado (es el estado por defecto del Animator).
/// - SetAtacando(true): repite la animacion de ataque (mientras muerde el anzuelo).
/// - AtacarUnaVez(): un solo ataque (cuando choca el bote).
/// No usa los parametros del Animator: cambia de estado por nombre, que es mas directo.
/// Solo es visual: cada dispositivo lo ejecuta por su cuenta.
/// </summary>
public class TiburonVisual : MonoBehaviour
{
    public Animator animator;
    [Tooltip("Nombre del estado de nado en el Animator")]
    public string estadoNadar = "shark_moving";
    [Tooltip("Nombre del estado de ataque en el Animator")]
    public string estadoAtacar = "shark_attackanim";
    [Tooltip("Duracion de la animacion de ataque (segundos)")]
    public float duracionAtaque = 1f;

    private bool atacandoContinuo = false;
    private float finAtaqueUnico = 0f;
    private float proximoCiclo = 0f;
    private bool enAtaque = false;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
    }

    public void SetAtacando(bool atacando)
    {
        atacandoContinuo = atacando;
    }

    public void AtacarUnaVez()
    {
        finAtaqueUnico = Time.time + duracionAtaque;
        proximoCiclo = 0f; // empieza ya
    }

    private void Update()
    {
        if (animator == null) return;

        bool debeAtacar = atacandoContinuo || Time.time < finAtaqueUnico;

        if (debeAtacar)
        {
            if (Time.time >= proximoCiclo)
            {
                animator.CrossFadeInFixedTime(estadoAtacar, 0.1f, 0, 0f);
                proximoCiclo = Time.time + duracionAtaque;
            }
            enAtaque = true;
        }
        else if (enAtaque)
        {
            animator.CrossFadeInFixedTime(estadoNadar, 0.25f, 0);
            enAtaque = false;
            proximoCiclo = 0f;
        }
    }
}
