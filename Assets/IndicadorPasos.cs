using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class IndicadoresPasos : MonoBehaviour
{
    // Paso -> GameObject (Paso2, Paso3, etc)
    private Dictionary<int, GameObject> indicadores = new Dictionary<int, GameObject>();
    private Dictionary<int, Coroutine> coroutines = new Dictionary<int, Coroutine>();

    void Awake()
    {
        // ESTE objeto ES el contenedor: (el empty "IndicadoresPasos")
        Transform contenedor = this.transform;

        indicadores.Clear();
        coroutines.Clear();

        foreach (Transform hijo in contenedor)
        {
            // Nombre tipo "Paso4", "Paso10"
            Match m = Regex.Match(hijo.name, @"Paso(\d+)");
            if (!m.Success) continue;

            if (int.TryParse(m.Groups[1].Value, out int numPaso))
            {
                indicadores[numPaso] = hijo.gameObject;
                hijo.gameObject.SetActive(false);   // siempre arrancan apagados
            }
        }

        Debug.Log($"🔵 Indicadores encontrados: {indicadores.Count}");
    }

    // Oculta todos y detiene parpadeos
    public void OcultarTodos()
    {
        foreach (var kv in indicadores)
        {
            if (coroutines.TryGetValue(kv.Key, out Coroutine c) && c != null)
                StopCoroutine(c);

            kv.Value.SetActive(false);
        }

        coroutines.Clear();
    }

    // Muestra SOLO el indicador del paso actual (parpadeando)
    public void MostrarSoloPaso(int paso)
    {
        // Paso <= 0 → no mostrar ninguno
        if (paso <= 0)
        {
            OcultarTodos();
            return;
        }

        // Primero apago todo lo demás
        OcultarTodos();

        if (!indicadores.TryGetValue(paso, out GameObject go) || go == null)
        {
            Debug.LogWarning($"⚠ No existe indicador visual para el paso {paso}");
            return;
        }

        go.SetActive(true);
        coroutines[paso] = StartCoroutine(Parpadear(go));
    }

    // Parpadeo modificando la Emission de los materiales de las esferas
    private IEnumerator Parpadear(GameObject pasoGO)
    {
        Renderer[] renders = pasoGO.GetComponentsInChildren<Renderer>();

        // Guardamos el color base de emission
        Color[] baseColors = new Color[renders.Length];
        for (int i = 0; i < renders.Length; i++)
        {
            if (renders[i].material.HasProperty("_EmissionColor"))
                baseColors[i] = renders[i].material.GetColor("_EmissionColor");
            else
                baseColors[i] = Color.black;
        }

        float t = 0f;

        while (true)
        {
            t += Time.deltaTime * 4f; // velocidad del parpadeo
            float intensidad = 0.5f + 0.5f * Mathf.Sin(t); // entre 0 y 1

            for (int i = 0; i < renders.Length; i++)
            {
                if (!renders[i].material.HasProperty("_EmissionColor")) continue;

                // multiplicamos el color base para que suba/baje
                renders[i].material.SetColor("_EmissionColor",
                    baseColors[i] * (1f + intensidad * 3f));
            }

            yield return null;
        }
    }
}
