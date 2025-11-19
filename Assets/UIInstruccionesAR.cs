using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Vuforia;

public class UIInstruccionesAR : MonoBehaviour
{
    [Header("Referencias de UI")]
    public CanvasGroup panelMensajes;
    public TextMeshProUGUI textoMensaje;
    public CanvasGroup imagenParpadeo;
    public GameObject botonSiguientePaso;
    public GameObject botonReload;
    public GameObject botonPasoAnterior;

    [Header("Textos base")]
    [TextArea]
    public string mensajeInicial =
        "Apunte la cámara a la imagen del mueble que viene en la caja.";

    [TextArea]
    public string mensajeMuebleDetectado =
        "Este es su mueble a tamaño real.\nPresione el botón 'Siguiente Paso' para ver cómo armarlo.";

    [Header("Textos por paso (opcional)")]
    [Tooltip("Index 0 = Paso 0 (vista explotada), 1 = Paso 1, etc.")]
    public string[] textosPorPaso;

    private ObserverBehaviour observer;
    private bool targetDetectado = false;

    void Start()
    {
        observer = GetComponent<ObserverBehaviour>();
        if (observer != null)
        {
            observer.OnTargetStatusChanged += OnTargetStatusChanged;
        }
        else
        {
            Debug.LogError("❌ Este script debe estar en un GameObject con ObserverBehaviour.");
        }

        if (panelMensajes != null)
            panelMensajes.alpha = 1f;

        if (textoMensaje != null)
            textoMensaje.text = mensajeInicial;

        if (imagenParpadeo != null)
        {
            imagenParpadeo.alpha = 0.5f;
            StartCoroutine(ParpadearImagen());
        }

        if (botonSiguientePaso != null)
            botonSiguientePaso.SetActive(false);

        if (botonPasoAnterior != null)
            botonPasoAnterior.SetActive(false);

        if (botonReload != null)
            botonReload.SetActive(false);
    }

    void OnDestroy()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= OnTargetStatusChanged;
    }

    private void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        if (!targetDetectado && status.Status != Status.NO_POSE)
        {
            targetDetectado = true;
            MostrarMensajeMuebleDetectado();
        }
    }

    private void MostrarMensajeMuebleDetectado()
    {
        if (textoMensaje != null)
            textoMensaje.text = "Este es tu mueble ya armado.\nPulsa 'Siguiente Paso' para ver cómo armarlo.";

        if (imagenParpadeo != null)
        {
            StopAllCoroutines();
            imagenParpadeo.alpha = 0f;
        }

        // ✅ Solo mostramos "Siguiente Paso" aquí
        if (botonSiguientePaso != null)
            botonSiguientePaso.SetActive(true);

        if (botonPasoAnterior != null)
            botonPasoAnterior.SetActive(false);

        if (botonReload != null)
            botonReload.SetActive(false);
    }
    private System.Collections.IEnumerator ParpadearImagen()
    {
        float t = 0f;
        bool subiendo = true;

        while (true)
        {
            t += (subiendo ? 1 : -1) * Time.deltaTime * 2f;
            t = Mathf.Clamp01(t);

            float alpha = Mathf.Lerp(0.2f, 0.8f, t);
            imagenParpadeo.alpha = alpha;

            if (t == 0f || t == 1f)
                subiendo = !subiendo;

            yield return null;
        }
    }

    private System.Collections.IEnumerator FadeOutPanel(CanvasGroup cg, float duracion)
    {
        float tiempo = 0f;
        float inicio = cg.alpha;
        float fin = 0f;

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            cg.alpha = Mathf.Lerp(inicio, fin, tiempo / duracion);
            yield return null;
        }

        cg.alpha = 0f;
        cg.gameObject.SetActive(false);
    }

    // ==========================
    // 🔹 LLAMADO DESDE ArmarMueble
    // ==========================
    public void ActualizarTextoPaso(int pasoActual, int ultimoPaso)
    {
        if (textoMensaje == null) return;

        // 🔹 Control de visibilidad de botones según el paso
        // Antes de iniciar (pasoActual = -1): solo "Siguiente"
        if (pasoActual < 0)
        {
            if (botonSiguientePaso != null) botonSiguientePaso.SetActive(true);
            if (botonPasoAnterior != null) botonPasoAnterior.SetActive(false);
            if (botonReload != null) botonReload.SetActive(false);

            textoMensaje.text = mensajeMuebleDetectado;
            return;
        }
        else
        {
            // Desde paso 0 en adelante: mostramos los tres
            if (botonSiguientePaso != null) botonSiguientePaso.SetActive(true);
            if (botonPasoAnterior != null) botonPasoAnterior.SetActive(true);
            if (botonReload != null) botonReload.SetActive(true);
        }

        // 🔹 Texto por paso (lo que ya tenías)
        if (pasoActual >= 0 &&
            textosPorPaso != null &&
            pasoActual < textosPorPaso.Length &&
            !string.IsNullOrWhiteSpace(textosPorPaso[pasoActual]))
        {
            textoMensaje.text = textosPorPaso[pasoActual];
        }
        else
        {
            if (pasoActual == 0)
                textoMensaje.text = "Paso 0:\nRevisa las piezas agrupadas por tipo antes de comenzar el armado.";
            else
                textoMensaje.text = $"Paso {pasoActual} de {ultimoPaso}.";
        }
    }

}
