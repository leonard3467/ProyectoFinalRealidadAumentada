using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEngine;
using TMPro;

// ============================================================
//  Estructuras de datos
// ============================================================
[System.Serializable]
public class PiezaMueble
{
    public string nombre;
    public int paso;
    public string material;

    public Vector3 posicionOriginal;
    public Quaternion rotacionOriginal;
    public Vector3 escalaOriginal;

    public GameObject objeto;

    // Para saber si ya fue colocada en el mueble
    public bool colocada = false;
}

[System.Serializable]
public class GrupoMaterial
{
    public string nombreMaterial;
    public Transform puntoAnclaje;   // donde va la pila de piezas
    public Transform puntoEtiqueta;  // donde va el texto xN

    public List<PiezaMueble> piezasOrdenadas = new List<PiezaMueble>();
}

// ============================================================
//  Comportamiento principal
// ============================================================
public class ArmarMueble : MonoBehaviour
{
    [Header("Referencia al modelo completo del mueble")]
    public GameObject modeloMueble;

    [Header("Prefab etiqueta cantidad (xN)")]
    public GameObject prefabEtiquetaCantidad;  // arrástralo en el Inspector

    // --- Datos de piezas / materiales ---
    public List<PiezaMueble> piezas = new List<PiezaMueble>();
    public Dictionary<string, List<PiezaMueble>> piezasPorMaterial =
        new Dictionary<string, List<PiezaMueble>>();
    public Dictionary<string, GrupoMaterial> gruposMaterial =
        new Dictionary<string, GrupoMaterial>();

    // --- Mapa de pasos -> piezas ---
    private Dictionary<int, List<PiezaMueble>> piezasPorPaso =
        new Dictionary<int, List<PiezaMueble>>();

    // --- Control de pasos ---
    [Header("Control de pasos (solo lectura en Inspector)")]
    public int pasoActual = -1;   // -1 = antes del paso 0 (mueble armado)
    public int ultimoPaso = 0;

    // Para no crear etiquetas duplicadas
    private Dictionary<string, GameObject> etiquetasPorMaterial =
        new Dictionary<string, GameObject>();

    // Referencia a la UI para actualizar el texto
    [HideInInspector] public UIInstruccionesAR ui;
    //[Header("Indicadores visuales de tornillos")]
    public IndicadoresPasos indicadoresPasos;


    // ============================================================
    // INIT
    // ============================================================
    void Start()
    {
        if (modeloMueble == null)
        {
            Debug.LogError("❌ No asignaste el modelo del mueble.");
            return;
        }

        ui = GetComponent<UIInstruccionesAR>();
        //indicadoresPasos = GetComponent<IndicadoresPasos>();

        piezas.Clear();
        piezasPorMaterial.Clear();
        gruposMaterial.Clear();
        piezasPorPaso.Clear();
        etiquetasPorMaterial.Clear();

        GuardarPiezasRecursivo(modeloMueble.transform);
        AgruparPorMaterial();
        CrearGruposDeMaterial();
        ConstruirMapaPasos();

        Debug.Log($"🔍 Piezas Detectadas = {piezas.Count}");
        Debug.Log($"📦 Materiales Detectados = {piezasPorMaterial.Keys.Count}");
        Debug.Log($"🧱 Pasos detectados: 1..{ultimoPaso}");
    }

    // ============================================================
    // 1) DETECTAR Y GUARDAR TODAS LAS PIEZAS DEL MODELO
    // ============================================================
    void GuardarPiezasRecursivo(Transform padre)
    {
        foreach (Transform hijo in padre)
        {
            string nombre = hijo.name;
            Match match = Regex.Match(nombre, @"Paso(\d+)_([\w\d]+)");

            if (match.Success)
            {
                int paso = int.Parse(match.Groups[1].Value);
                string material = Regex.Replace(match.Groups[2].Value, @"\d+$", "");

                PiezaMueble pieza = new PiezaMueble
                {
                    nombre = nombre,
                    paso = paso,
                    material = material,
                    posicionOriginal = hijo.localPosition,
                    rotacionOriginal = hijo.localRotation,
                    escalaOriginal = hijo.localScale,
                    objeto = hijo.gameObject,
                    colocada = false
                };

                piezas.Add(pieza);
            }
            else
            {
                // Hijos que no siguen el patrón PasoX_...
                Debug.Log($"⚠ Hijo sin patrón PasoX_: {nombre} (no se controla en explode)");
            }

            GuardarPiezasRecursivo(hijo);
        }
    }

    // ============================================================
    // 2) AGRUPAR POR MATERIAL
    // ============================================================
    void AgruparPorMaterial()
    {
        piezasPorMaterial.Clear();

        foreach (PiezaMueble p in piezas)
        {
            if (!piezasPorMaterial.ContainsKey(p.material))
                piezasPorMaterial[p.material] = new List<PiezaMueble>();

            piezasPorMaterial[p.material].Add(p);
        }

        foreach (var kvp in piezasPorMaterial)
            Debug.Log($"🔹 Material {kvp.Key}: {kvp.Value.Count} piezas");
    }

    // ============================================================
    // 3) GRUPOS DE MATERIALES + VINCULAR ANCLAJES
    // ============================================================
    void CrearGruposDeMaterial()
    {
        gruposMaterial.Clear();

        Transform root = modeloMueble.transform.parent; // ImageTarget

        foreach (var kvp in piezasPorMaterial)
        {
            string material = kvp.Key;
            List<PiezaMueble> lista = kvp.Value;

            string rutaAnclaje = "Anclajes/Anclaje_" + material;
            Transform anclaje = root.Find(rutaAnclaje);

            if (anclaje == null)
            {
                Debug.LogError(
                    $"❌ No se encontró el anclaje para el material '{material}': ruta esperada = {rutaAnclaje}");
                continue;
            }

            string nombreEtiquetaHijo = "Etiqueta_" + material;
            Transform puntoEtiqueta = anclaje.Find(nombreEtiquetaHijo);

            GrupoMaterial gm = new GrupoMaterial();
            gm.nombreMaterial = material;
            gm.puntoAnclaje = anclaje;
            gm.puntoEtiqueta = puntoEtiqueta != null ? puntoEtiqueta : anclaje;

            lista.Sort((a, b) => a.paso.CompareTo(b.paso));
            gm.piezasOrdenadas.AddRange(lista);

            gruposMaterial.Add(material, gm);

            Debug.Log(
                $"✔ Grupo '{material}' vinculado → {lista.Count} piezas → " +
                $"anclaje = {rutaAnclaje}, etiqueta = {(puntoEtiqueta ? puntoEtiqueta.name : anclaje.name)}"
            );
        }

        Debug.Log($"📦 Grupos con anclaje: {gruposMaterial.Count} / materiales totales: {piezasPorMaterial.Keys.Count}");
    }

    // ============================================================
    // 4) MAPA DE PASOS
    // ============================================================
    void ConstruirMapaPasos()
    {
        piezasPorPaso.Clear();
        ultimoPaso = 0;

        foreach (var p in piezas)
        {
            if (!piezasPorPaso.ContainsKey(p.paso))
                piezasPorPaso[p.paso] = new List<PiezaMueble>();

            piezasPorPaso[p.paso].Add(p);

            if (p.paso > ultimoPaso)
                ultimoPaso = p.paso;
        }
    }

    // ============================================================
    // 5) ETIQUETAS xN
    // ============================================================
    void CrearOActualizarEtiqueta(string material, Transform padre, int cantidad)
    {
        if (prefabEtiquetaCantidad == null)
        {
            Debug.LogWarning("⚠ No se ha asignado el prefabEtiquetaCantidad en el Inspector.");
            return;
        }

        GameObject etiquetaGO;

        // Reutilizar si ya existe
        if (!etiquetasPorMaterial.TryGetValue(material, out etiquetaGO) || etiquetaGO == null)
        {
            etiquetaGO = Instantiate(prefabEtiquetaCantidad, padre);
            etiquetasPorMaterial[material] = etiquetaGO;
        }

        etiquetaGO.transform.SetParent(padre, false);
        etiquetaGO.transform.localPosition = Vector3.zero;
        etiquetaGO.transform.localRotation = Quaternion.identity;

        var tmp = etiquetaGO.GetComponent<TextMeshPro>();
        if (tmp != null)
            tmp.text = "x" + cantidad;      // 🔹 Aquí puede ser x0, x1, x2...
        else
            Debug.LogWarning($"⚠ El prefabEtiquetaCantidad no tiene TextMeshPro. Material: {material}");

        etiquetaGO.SetActive(true);         // 🔹 Nunca la ocultamos por cantidad 0
    }

    void OcultarTodasLasEtiquetas()
    {
        foreach (var kvp in etiquetasPorMaterial)
        {
            if (kvp.Value != null)
                kvp.Value.SetActive(false);
        }
    }

    void ActualizarEtiquetaGrupo(GrupoMaterial g)
    {
        int restantes = g.piezasOrdenadas.Count(p => !p.colocada);
        CrearOActualizarEtiqueta(g.nombreMaterial, g.puntoEtiqueta, restantes);
    }

    // ============================================================
    // 6) EXPLODED (PASO 0): TODAS LAS PIEZAS A SUS ANCLAJES
    // ============================================================

    void ActualizarPiezaMuestraGrupo(GrupoMaterial g)
    {
        // 1) Elegimos la ÚLTIMA pieza NO colocada (la cola)
        PiezaMueble muestra = null;

        // Recorremos la lista de atrás hacia adelante
        for (int i = g.piezasOrdenadas.Count - 1; i >= 0; i--)
        {
            var pieza = g.piezasOrdenadas[i];
            if (!pieza.colocada && pieza.objeto != null)
            {
                muestra = pieza;
                break;
            }
        }

        // 2) Recorremos todas las piezas del grupo
        foreach (var pieza in g.piezasOrdenadas)
        {
            if (pieza.objeto == null) continue;

            if (pieza.colocada)
            {
                // Ya está en su posición final → no la tocamos
                continue;
            }

            if (pieza == muestra)
            {
                // Esta es la pieza muestra → se va al anclaje
                pieza.objeto.SetActive(true);
                pieza.objeto.transform.position = g.puntoAnclaje.position;
                pieza.objeto.transform.rotation = g.puntoAnclaje.rotation;
            }
            else
            {
                // No colocada pero NO es la muestra → se oculta
                pieza.objeto.SetActive(false);
            }
        }

        // Si muestra == null → ya no quedan piezas sin colocar de ese material.
        // La etiqueta se pondrá en 0 y no se mostrará más en ActualizarEtiquetaGrupo.
    }
    public void ExplodeHaciaAnclajes()
    {
        // Todas las piezas vuelven a estado "no colocada"
        foreach (var p in piezas)
            p.colocada = false;

        // Para cada grupo de material, elegimos su pieza muestra
        foreach (var kvp in gruposMaterial)
        {
            GrupoMaterial g = kvp.Value;

            // Pone la primera NO colocada en el anclaje y oculta las demás NO colocadas
            ActualizarPiezaMuestraGrupo(g);

            // Etiqueta xN con cuántas faltan por colocar
            ActualizarEtiquetaGrupo(g);
        }

        Debug.Log("💥 Paso 0: Exploded hacia anclajes (1 muestra por material) COMPLETO.");
    }
    // ============================================================
    // 7) EJECUTAR UN PASO > 0  (mover piezas a su posición final)
    // ============================================================
    void EjecutarPaso(int paso, bool actualizarUiInterno = false)
    {
        if (!piezasPorPaso.ContainsKey(paso))
        {
            Debug.LogWarning($"⚠ No hay piezas registradas para el paso {paso}");
            return;
        }

        // Para saber qué materiales se modificaron en este paso
        HashSet<string> materialesAfectados = new HashSet<string>();

        foreach (var pieza in piezasPorPaso[paso])
        {
            if (pieza.objeto == null) continue;

            // 1) La pieza se coloca en su posición final del mueble
            pieza.objeto.SetActive(true);
            pieza.objeto.transform.localPosition = pieza.posicionOriginal;
            pieza.objeto.transform.localRotation = pieza.rotacionOriginal;
            pieza.objeto.transform.localScale = pieza.escalaOriginal;

            // 2) Marcamos que ya está colocada
            pieza.colocada = true;
            materialesAfectados.Add(pieza.material);
        }

        // 3) Para cada material afectado, actualizar muestra + etiqueta
        foreach (var mat in materialesAfectados)
        {
            if (gruposMaterial.TryGetValue(mat, out GrupoMaterial g))
            {
                // Nueva pieza muestra (siguiente no colocada) en el anclaje
                ActualizarPiezaMuestraGrupo(g);

                // Actualizar xN restantes
                ActualizarEtiquetaGrupo(g);
            }
        }

        Debug.Log($"➡ Paso {paso} ejecutado ({piezasPorPaso[paso].Count} pieza(s)).");

        if (actualizarUiInterno)
            ActualizarUI();
    }
    // ============================================================
    // 8) CONTROL DE SECUENCIA (BOTONES)
    // ============================================================


    void ActualizarIndicadoresVisuales()
    {
        if (indicadoresPasos == null) return;

        // Solo mostrar indicadores para pasos > 0
        indicadoresPasos.MostrarSoloPaso(pasoActual);
    }
    // Botón "Siguiente paso"
    public void SiguientePaso()
    {
        // Primera vez: del mueble armado pasamos a la vista explotada (paso 0)
        if (pasoActual < 0)
        {
            ExplodeHaciaAnclajes();
            pasoActual = 0;
        }
        else if (pasoActual < ultimoPaso)
        {
            pasoActual++;
            EjecutarPaso(pasoActual);
        }
        else
        {
            Debug.Log("✅ Ya estás en el último paso.");
        }

        // 🔹 Si YA estamos en el último paso, ocultamos las etiquetas (ya no hacen falta)
        if (pasoActual == ultimoPaso)
        {
            OcultarTodasLasEtiquetas();
        }

        ActualizarUI();
        ActualizarIndicadoresVisuales();
    }

    // Botón "Paso anterior"
    public void PasoAnterior()
    {
        if (pasoActual <= 0)
        {
            pasoActual = 0;
            ExplodeHaciaAnclajes();
        }
        else
        {
            pasoActual--;

            // Reconstruimos estado: empezamos de exploded (paso 0)
            ExplodeHaciaAnclajes();

            // Re-aplicamos pasos 1..pasoActual
            for (int s = 1; s <= pasoActual; s++)
                EjecutarPaso(s, false);
        }

        ActualizarUI();
        ActualizarIndicadoresVisuales();
    }

    // Botón "Repetir / Reiniciar"
    public void ReiniciarSecuencia()
    {
        pasoActual = -1;
        ResetModelo();
        ActualizarUI();
        if (indicadoresPasos != null)
            indicadoresPasos.OcultarTodos();
    }

    // ============================================================
    // 9) RESET DEL MODELO ARMADO ORIGINAL
    // ============================================================
    public void ResetModelo()
    {
        foreach (var p in piezas)
        {
            if (p.objeto == null) continue;

            p.colocada = false;
            p.objeto.SetActive(true);
            p.objeto.transform.localPosition = p.posicionOriginal;
            p.objeto.transform.localRotation = p.rotacionOriginal;
            p.objeto.transform.localScale = p.escalaOriginal;
        }

        // Ocultar etiquetas
        foreach (var kvp in etiquetasPorMaterial)
        {
            if (kvp.Value != null)
                kvp.Value.SetActive(false);
        }

        Debug.Log("🔄 Modelo reseteado a posiciones originales.");
    }

    // ============================================================
    // 10) AVISAR A LA UI
    // ============================================================
    void ActualizarUI()
    {
        if (ui != null)
            ui.ActualizarTextoPaso(pasoActual, ultimoPaso);
    }
}
