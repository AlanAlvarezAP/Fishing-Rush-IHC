using Unity.Netcode.Components;
using UnityEngine;

namespace Unity.Netcode.Components
{
    /// <summary>
    /// Versión de NetworkTransform donde el DUEÑO (owner) del objeto,
    /// no el servidor, es quien tiene autoridad para mover el transform.
    /// Útil en prototipos donde cada cliente controla su propio Player
    /// directamente con input local (teclado, Wii remote, etc.).
    ///
    /// Nota: para un juego final, la forma "correcta"/anti-cheat es que
    /// el cliente mande su input al servidor vía ServerRpc y el servidor
    /// mueva el objeto (dejando NetworkTransform normal). Pero para
    /// prototipar rápido, esto es la solución estándar recomendada por
    /// Unity (ver Boss Room sample / documentación de Netcode).
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}