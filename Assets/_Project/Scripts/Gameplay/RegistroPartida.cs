using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public class ResultadoPartida
{
    public string pescador = "";
    public string conductor = "";
    public string fecha = "";
    public float duracion;

    public int peces;
    public int intentos;
    public int patronesOk;
    public int patronesFallados;
    public int[] gestoOk = new int[5];
    public int[] gestoFallo = new int[5];
    public float segundosPatronesOk;
    public float segundosPrimerPez = -1f;

    public int minas;
    public float segundosInmovil;

    public int mordidas;
    public int embestidas;
    public float segundosBloqueado;
    public float segundosTambaleo;
    public int patronesTiburonOk;
    public int patronesTiburonFallados;

    public float distanciaBote;
    public float segundosQuieto;
    public float segundosGiroInclinacion;
    public float segundosGiroPads;

    public string Equipo => string.IsNullOrEmpty(conductor) ? pescador : pescador + " + " + conductor;

    public float RatioCapturas => intentos > 0 ? (float)peces / intentos : 0f;

    public float PrecisionPatrones
    {
        get
        {
            int total = patronesOk + patronesFallados;
            return total > 0 ? (float)patronesOk / total : 0f;
        }
    }

    public float SegundosPorPatron => patronesOk > 0 ? segundosPatronesOk / patronesOk : 0f;

    public int AtaquesTiburon => mordidas + embestidas;

    public float PrecisionGesto(int gesto)
    {
        if (gesto < 0 || gesto >= gestoOk.Length || gesto >= gestoFallo.Length) return -1f;
        int total = gestoOk[gesto] + gestoFallo[gesto];
        return total > 0 ? (float)gestoOk[gesto] / total : -1f;
    }

    public float PorcentajeInclinacion
    {
        get
        {
            float total = segundosGiroInclinacion + segundosGiroPads;
            return total > 0.01f ? segundosGiroInclinacion / total : -1f;
        }
    }
}

[Serializable]
public class TableroGuardado
{
    public int partidasJugadas;
    public List<ResultadoPartida> equipos = new List<ResultadoPartida>();
}

public static class RegistroPartida
{
    public const int MAX_LETRAS_NOMBRE = 12;

    private static ResultadoPartida actual;
    private static float inicio;
    private static TableroGuardado tablero;
    private static string carpeta;

    public static bool Activo => actual != null;

    public const string NOMBRE_CARPETA = "Resultados";

    public static string Carpeta
    {
        get
        {
            if (carpeta == null) carpeta = ElegirCarpeta();
            return carpeta;
        }
    }

    public static string CarpetaVisible => Carpeta.Replace('\\', '/');

    public static string RutaTablero => Path.Combine(Carpeta, "scoreboard.json");
    public static string RutaSesiones => Path.Combine(Carpeta, "sesiones.csv");

    private static string ElegirCarpeta()
    {
        if (!Application.isMobilePlatform)
        {
            try
            {
                string raiz = Path.GetDirectoryName(Application.dataPath);
                string destino = Path.Combine(raiz, NOMBRE_CARPETA);
                Directory.CreateDirectory(destino);

                string prueba = Path.Combine(destino, "prueba.tmp");
                File.WriteAllText(prueba, "ok");
                File.Delete(prueba);

                CopiarSiFalta(Path.Combine(Application.persistentDataPath, "scoreboard.json"), Path.Combine(destino, "scoreboard.json"));
                CopiarSiFalta(Path.Combine(Application.persistentDataPath, "sesiones.csv"), Path.Combine(destino, "sesiones.csv"));
                return destino;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TABLERO] No se puede escribir junto al ejecutable, se usa la carpeta de datos del usuario: " + e.Message);
            }
        }

        return Application.persistentDataPath;
    }

    private static void CopiarSiFalta(string origen, string destino)
    {
        if (File.Exists(destino) || !File.Exists(origen)) return;
        File.Copy(origen, destino);
    }

    public static TableroGuardado Tablero
    {
        get
        {
            if (tablero == null) Cargar();
            return tablero;
        }
    }

    public static void Cargar()
    {
        tablero = null;
        try
        {
            if (File.Exists(RutaTablero))
            {
                tablero = JsonUtility.FromJson<TableroGuardado>(File.ReadAllText(RutaTablero, Encoding.UTF8));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[TABLERO] No se pudo leer scoreboard.json: " + e.Message);
        }

        if (tablero == null) tablero = new TableroGuardado();
        if (tablero.equipos == null) tablero.equipos = new List<ResultadoPartida>();

        foreach (ResultadoPartida r in tablero.equipos)
        {
            if (r.gestoOk == null || r.gestoOk.Length < 5) r.gestoOk = new int[5];
            if (r.gestoFallo == null || r.gestoFallo.Length < 5) r.gestoFallo = new int[5];
        }

        Ordenar();
        Debug.Log($"[TABLERO] {tablero.equipos.Count} equipos cargados. Carpeta: {Carpeta}");
    }

    public static string LimpiarNombre(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return "";

        StringBuilder sb = new StringBuilder();
        foreach (char c in nombre)
        {
            if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '.') sb.Append(c);
        }

        string limpio = sb.ToString().Trim();
        if (limpio.Length > MAX_LETRAS_NOMBRE) limpio = limpio.Substring(0, MAX_LETRAS_NOMBRE).Trim();
        return limpio;
    }

    public static void Iniciar(string pescador, string conductor, float duracion)
    {
        pescador = LimpiarNombre(pescador);
        conductor = LimpiarNombre(conductor);

        if (pescador.Length == 0 && conductor.Length == 0)
        {
            pescador = "Equipo " + (Tablero.partidasJugadas + 1);
        }
        else if (pescador.Length == 0)
        {
            pescador = "Pescador";
        }

        actual = new ResultadoPartida
        {
            pescador = pescador,
            conductor = conductor,
            duracion = duracion
        };

        inicio = Time.time;

        Debug.Log($"[REGISTRO] Empieza la partida de {actual.Equipo}.");
    }

    public static void Cancelar()
    {
        actual = null;
    }

    public static void IntentoPez()
    {
        if (actual == null) return;
        actual.intentos++;
    }

    public static void Patron(int gesto, bool correcto, float segundos)
    {
        if (actual == null) return;

        if (correcto)
        {
            actual.patronesOk++;
            actual.segundosPatronesOk += Mathf.Max(0f, segundos);
        }
        else
        {
            actual.patronesFallados++;
        }

        SumarGesto(gesto, correcto);
    }

    public static void PatronTiburon(int gesto, bool correcto)
    {
        if (actual == null) return;

        if (correcto) actual.patronesTiburonOk++;
        else actual.patronesTiburonFallados++;

        SumarGesto(gesto, correcto);
    }

    private static void SumarGesto(int gesto, bool correcto)
    {
        if (gesto < 0 || gesto >= actual.gestoOk.Length) return;

        if (correcto) actual.gestoOk[gesto]++;
        else actual.gestoFallo[gesto]++;
    }

    public static void PezCapturado()
    {
        if (actual == null) return;

        actual.peces++;
        if (actual.segundosPrimerPez < 0f) actual.segundosPrimerPez = Time.time - inicio;
    }

    public static void MinaChocada(float segundos)
    {
        if (actual == null) return;
        actual.minas++;
        actual.segundosInmovil += Mathf.Max(0f, segundos);
    }

    public static void MordidaTiburon()
    {
        if (actual == null) return;
        actual.mordidas++;
    }

    public static void TiempoBloqueado(float segundos)
    {
        if (actual == null) return;
        actual.segundosBloqueado += Mathf.Max(0f, segundos);
    }

    public static void EmbestidaTiburon(float segundos)
    {
        if (actual == null) return;
        actual.embestidas++;
        actual.segundosTambaleo += Mathf.Max(0f, segundos);
    }

    public static void Bote(float distancia, float dt, bool conInput, bool girando, bool giroPorInclinacion)
    {
        if (actual == null) return;

        actual.distanciaBote += Mathf.Max(0f, distancia);
        if (!conInput) actual.segundosQuieto += dt;

        if (girando)
        {
            if (giroPorInclinacion) actual.segundosGiroInclinacion += dt;
            else actual.segundosGiroPads += dt;
        }
    }

    public static ResultadoPartida Terminar(int pecesContador)
    {
        if (actual == null) return null;

        ResultadoPartida r = actual;
        actual = null;

        r.peces = Mathf.Max(r.peces, pecesContador);
        r.intentos = Mathf.Max(r.intentos, r.peces);
        r.fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        TableroGuardado t = Tablero;
        t.partidasJugadas++;

        GuardarSesion(r, t.partidasJugadas);

        int previo = t.equipos.FindIndex(e => Clave(e) == Clave(r));
        if (previo < 0) t.equipos.Add(r);
        else if (Comparar(r, t.equipos[previo]) < 0) t.equipos[previo] = r;

        Ordenar();
        GuardarTablero();

        Debug.Log($"[REGISTRO] {r.Equipo}: {r.peces} peces, capturas {r.RatioCapturas:P0}, patrones {r.PrecisionPatrones:P0}.");
        return r;
    }

    private static string Clave(ResultadoPartida r)
    {
        return (r.pescador ?? "").Trim().ToLowerInvariant() + "|" + (r.conductor ?? "").Trim().ToLowerInvariant();
    }

    private static int Comparar(ResultadoPartida a, ResultadoPartida b)
    {
        if (a.peces != b.peces) return b.peces.CompareTo(a.peces);

        int precision = b.PrecisionPatrones.CompareTo(a.PrecisionPatrones);
        if (precision != 0) return precision;

        return b.RatioCapturas.CompareTo(a.RatioCapturas);
    }

    private static void Ordenar()
    {
        if (tablero != null && tablero.equipos != null) tablero.equipos.Sort(Comparar);
    }

    public static List<ResultadoPartida> Mejores(int cantidad)
    {
        List<ResultadoPartida> todos = Tablero.equipos;
        return todos.GetRange(0, Mathf.Min(cantidad, todos.Count));
    }

    public static string TextoRed(int cantidad)
    {
        StringBuilder sb = new StringBuilder();
        foreach (ResultadoPartida r in Mejores(cantidad))
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(r.Equipo).Append('|')
              .Append(r.peces).Append('|')
              .Append(Porcentaje(r.RatioCapturas)).Append('|')
              .Append(Porcentaje(r.PrecisionPatrones)).Append('|')
              .Append(r.minas).Append('|')
              .Append(r.AtaquesTiburon);
        }
        return sb.ToString();
    }

    public static string Porcentaje(float valor)
    {
        if (valor < 0f) return "-";
        return Mathf.RoundToInt(valor * 100f) + "%";
    }

    private static void GuardarTablero()
    {
        try
        {
            File.WriteAllText(RutaTablero, JsonUtility.ToJson(tablero, true), Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[TABLERO] No se pudo guardar scoreboard.json: " + e.Message);
        }
    }

    private static string N(float valor, string formato = "0.##")
    {
        return valor.ToString(formato, CultureInfo.InvariantCulture);
    }

    private static string P(float valor)
    {
        return valor < 0f ? "" : N(valor * 100f, "0.#");
    }

    private static readonly string[] columnasSesion =
    {
        "partida", "fecha", "pescador", "conductor", "duracion_s",
        "peces", "intentos", "ratio_capturas_pct",
        "patrones_ok", "patrones_fallados", "precision_patrones_pct", "segundos_por_patron",
        "horizontal_ok", "horizontal_fallo", "vertical_ok", "vertical_fallo",
        "circulo_ok", "circulo_fallo", "cruz_ok", "cruz_fallo",
        "segundos_primer_pez",
        "minas", "segundos_inmovil",
        "mordidas_tiburon", "embestidas_tiburon", "segundos_bloqueado", "segundos_tambaleo",
        "patrones_tiburon_ok", "patrones_tiburon_fallados",
        "distancia_bote_m", "segundos_bote_quieto",
        "segundos_giro_inclinacion", "segundos_giro_pads", "giro_inclinacion_pct"
    };

    private static void GuardarSesion(ResultadoPartida r, int numero)
    {
        try
        {
            string cabecera = string.Join(",", columnasSesion);

            if (File.Exists(RutaSesiones))
            {
                string primera = "";
                using (StreamReader lector = new StreamReader(RutaSesiones, Encoding.UTF8))
                {
                    primera = lector.ReadLine() ?? "";
                }

                if (primera.Trim() != cabecera)
                {
                    string sello = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                    File.Move(RutaSesiones, Path.Combine(Carpeta, "sesiones_anterior_" + sello + ".csv"));
                }
            }

            StringBuilder sb = new StringBuilder();
            if (!File.Exists(RutaSesiones)) sb.AppendLine(cabecera);

            sb.AppendLine(string.Join(",", new[]
            {
                numero.ToString(CultureInfo.InvariantCulture), r.fecha, r.pescador, r.conductor, N(r.duracion),
                r.peces.ToString(), r.intentos.ToString(), P(r.RatioCapturas),
                r.patronesOk.ToString(), r.patronesFallados.ToString(), P(r.PrecisionPatrones), N(r.SegundosPorPatron),
                r.gestoOk[1].ToString(), r.gestoFallo[1].ToString(), r.gestoOk[2].ToString(), r.gestoFallo[2].ToString(),
                r.gestoOk[3].ToString(), r.gestoFallo[3].ToString(), r.gestoOk[4].ToString(), r.gestoFallo[4].ToString(),
                r.segundosPrimerPez < 0f ? "" : N(r.segundosPrimerPez, "0.#"),
                r.minas.ToString(), N(r.segundosInmovil, "0.#"),
                r.mordidas.ToString(), r.embestidas.ToString(), N(r.segundosBloqueado, "0.#"), N(r.segundosTambaleo, "0.#"),
                r.patronesTiburonOk.ToString(), r.patronesTiburonFallados.ToString(),
                N(r.distanciaBote, "0.#"), N(r.segundosQuieto, "0.#"),
                N(r.segundosGiroInclinacion, "0.#"), N(r.segundosGiroPads, "0.#"), P(r.PorcentajeInclinacion)
            }));

            File.AppendAllText(RutaSesiones, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[REGISTRO] No se pudo guardar sesiones.csv: " + e.Message);
        }
    }
}
