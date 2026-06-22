using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Aurora.RouteProgress;

namespace Aurora.RouteProgress.EditorTools
{
    /// <summary>
    /// Herramienta de Editor que crea y cablea TODO el sistema de progreso de ruta dentro de la
    /// escena abierta, con un solo clic de menú: <b>Aurora ▸ Route Progress ▸ Setup In Scene</b>.
    ///
    /// Es idempotente: si los objetos ya existen los reutiliza en vez de duplicarlos, así que se
    /// puede ejecutar varias veces sin ensuciar la escena.
    ///
    /// Qué crea:
    ///   • RouteProgressManager  (GameObject con el manager, height source = HMD)
    ///   • RouteStartCheckpoint  (trigger box cerca del spawn del jugador)
    ///   • RouteFinishCheckpoint (trigger box más arriba en la montaña)
    ///   • RouteCompletionPanel  (canvas world-space oculto, con textos TMP y botón Reset)
    ///
    /// Detección automática del rig: busca por nombre CenterEyeAnchor (HMD) y PlayerController
    /// para cablear el height source, el player transform y posicionar los checkpoints respecto
    /// al spawn. Si no los encuentra, usa valores por defecto y avisa por consola.
    /// </summary>
    public static class RouteProgressSetup
    {
        private const string MenuRoot = "Aurora/Route Progress/";

        // Nombres de los GameObjects creados (también sirven para idempotencia).
        private const string ManagerName = "RouteProgressManager";
        private const string StartName = "RouteStartCheckpoint";
        private const string FinishName = "RouteFinishCheckpoint";
        private const string PanelName = "RouteCompletionPanel";

        [MenuItem(MenuRoot + "Setup In Scene", false, 0)]
        public static void SetupInScene()
        {
            // --- 1. Localizar el rig para auto-cablear y posicionar ---
            Transform hmd = FindByName("CenterEyeAnchor");
            Transform player = FindByName("PlayerController");

            Vector3 spawn = player != null ? player.position
                          : (hmd != null ? hmd.position : new Vector3(84.9f, 42.22f, -24.3f));

            Vector3 startPos = spawn;
            Vector3 finishPos = spawn + new Vector3(0f, 0f, 5f);
            Vector3 panelPos = spawn + new Vector3(0f, 2f, 6.5f);

            // --- 2. Manager ---
            RouteProgressManager manager = Object.FindFirstObjectByType<RouteProgressManager>();
            if (manager == null)
            {
                var managerGo = new GameObject(ManagerName);
                Undo.RegisterCreatedObjectUndo(managerGo, "Create RouteProgressManager");
                manager = managerGo.AddComponent<RouteProgressManager>();
            }
            // height source = HMD
            if (hmd != null)
            {
                SetObjectRef(manager, "_heightSource", hmd);
            }

            // Tester opcional de teclado (1/2/3) para validar el flujo en Play Mode sin VR.
            if (manager.GetComponent<RouteProgressDebugTester>() == null)
            {
                manager.gameObject.AddComponent<RouteProgressDebugTester>();
            }

            // --- 3. Checkpoint de inicio ---
            RouteStartCheckpoint start = Object.FindFirstObjectByType<RouteStartCheckpoint>();
            if (start == null)
            {
                var go = new GameObject(StartName);
                Undo.RegisterCreatedObjectUndo(go, "Create RouteStartCheckpoint");
                go.transform.position = startPos;
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(3f, 3f, 3f);
                start = go.AddComponent<RouteStartCheckpoint>();
            }
            else
            {
                start.transform.position = startPos;
            }
            WireCheckpoint(start, manager, player);

            // --- 4. Checkpoint final ---
            RouteFinishCheckpoint finish = Object.FindFirstObjectByType<RouteFinishCheckpoint>();
            if (finish == null)
            {
                var go = new GameObject(FinishName);
                Undo.RegisterCreatedObjectUndo(go, "Create RouteFinishCheckpoint");
                go.transform.position = finishPos;
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(3f, 3f, 3f);
                finish = go.AddComponent<RouteFinishCheckpoint>();
            }
            else
            {
                finish.transform.position = finishPos;
            }
            WireCheckpoint(finish, manager, player);

            // --- 4b. Cartel de feedback de INICIO (mensaje flotante + sonido) ---
            EnsureFeedback(start, "RouteStartFeedback", "¡Ruta iniciada!",
                new Color(1f, 0.9f, 0.2f), startPos + new Vector3(0f, 2.5f, 0f), hmd);

            // --- 4c. Marcadores visuales (haz de luz) para ubicar los checkpoints en VR ---
            // Amarillo para el inicio, verde para el fin.
            EnsureBeacon(start.gameObject, new Color(1f, 0.9f, 0.2f));
            EnsureBeacon(finish.gameObject, new Color(0.3f, 1f, 0.4f));

            // --- 5. Panel de compleción (world-space) ---
            RouteCompletionPanel panel = Object.FindFirstObjectByType<RouteCompletionPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                panel = BuildCompletionPanel(panelPos, hmd);
            }
            // Cablear manager + cámara en el panel.
            SetObjectRef(panel, "_manager", manager);
            if (hmd != null)
            {
                SetObjectRef(panel, "_cameraTransform", hmd);
            }

            // Marcar la escena como modificada para que se pueda guardar.
            EditorUtility.SetDirty(manager);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("[RouteProgressSetup] Sistema de progreso de ruta creado/cableado en la escena. " +
                      (player != null ? "Player detectado. " : "Player NO detectado (revisa el tag). ") +
                      (hmd != null ? "HMD detectado." : "HMD NO detectado (asigna height source a mano)."));

            // Seleccionar el manager para que el usuario lo vea.
            Selection.activeObject = manager.gameObject;
        }

        // Permite ejecutar sólo si hay una escena abierta (siempre true en la práctica).
        [MenuItem(MenuRoot + "Setup In Scene", true)]
        private static bool ValidateSetup() => true;

        /// <summary>Cablea manager y player transform en un checkpoint vía SerializedObject.</summary>
        private static void WireCheckpoint(RouteCheckpointBase checkpoint, RouteProgressManager manager, Transform player)
        {
            SetObjectRef(checkpoint, "_manager", manager);
            if (player != null)
            {
                SetObjectRef(checkpoint, "_playerTransform", player);
            }
        }

        // GUID del clip de sonido reutilizado del proyecto (Interaction_Locomotion_Pull_Up_Bar_01.wav).
        private const string FeedbackClipGuid = "238f0d93a2be6e04aaf85e1470416217";

        /// <summary>
        /// Crea (si no existe) un cartel de feedback con mensaje + sonido y lo cablea al campo
        /// privado "_feedback" del checkpoint. Idempotente: si el checkpoint ya tiene un feedback
        /// asignado, no crea otro.
        /// </summary>
        private static void EnsureFeedback(RouteCheckpointBase checkpoint, string signName, string message,
            Color color, Vector3 worldPos, Transform hmd)
        {
            // ¿Ya tiene feedback cableado? Entonces no duplicar.
            var so = new SerializedObject(checkpoint);
            var prop = so.FindProperty("_feedback");
            if (prop != null && prop.objectReferenceValue != null)
            {
                return;
            }

            RouteCheckpointFeedback feedback = BuildFeedbackSign(signName, message, color, worldPos, hmd);
            SetObjectRef(checkpoint, "_feedback", feedback);
        }

        /// <summary>
        /// Construye un cartel world-space (canvas + fondo + TMP) con un AudioSource, devolviendo
        /// el componente RouteCheckpointFeedback ya cableado y con el contenedor oculto.
        /// </summary>
        private static RouteCheckpointFeedback BuildFeedbackSign(string name, string message,
            Color color, Vector3 worldPos, Transform hmd)
        {
            // Canvas raíz (world space) + el componente de feedback (vive SIEMPRE activo).
            var canvasGo = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Route Feedback Sign");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(560f, 160f);
            canvasRt.position = worldPos;
            canvasGo.transform.localScale = Vector3.one * 0.003f; // ~1.7 m de ancho

            // AudioSource (sonido del cartel).
            var audio = canvasGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f; // 2D: se oye claro al cruzar, sin atenuación por distancia
            AudioClip clip = LoadAudioClip(FeedbackClipGuid);
            if (clip != null)
            {
                audio.clip = clip;
            }

            // Contenedor "Sign" (hijo) que se togglea al mostrar/ocultar.
            var sign = CreateUIChild(canvasRt, "Sign", out RectTransform signRt);
            Stretch(signRt);

            // Fondo redondeado semitransparente del color del checkpoint.
            var bg = CreateUIChild(signRt, "Background", out RectTransform bgRt);
            Stretch(bgRt);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.85f);

            // Borde de color (un Image un poco más grande detrás).
            var border = CreateUIChild(signRt, "Border", out RectTransform borderRt);
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-8f, -8f);
            borderRt.offsetMax = new Vector2(8f, 8f);
            borderRt.SetAsFirstSibling(); // detrás del fondo
            var borderImg = border.AddComponent<Image>();
            borderImg.color = color;

            // Texto del mensaje.
            var tmp = CreateTMP(signRt, "Message", message, 56, FontStyles.Bold,
                Vector2.zero, new Vector2(540f, 140f), TextAlignmentOptions.Center);
            var trt = tmp.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            // Componente de feedback y cableado.
            var feedback = canvasGo.AddComponent<RouteCheckpointFeedback>();
            SetObjectRef(feedback, "_messageRoot", sign);
            SetObjectRef(feedback, "_messageText", tmp);
            SetStringField(feedback, "_message", message);
            SetObjectRef(feedback, "_audioSource", audio);
            if (hmd != null)
            {
                SetObjectRef(feedback, "_cameraTransform", hmd);
            }

            // Oculto en edición (en runtime Awake también lo oculta).
            sign.SetActive(false);

            return feedback;
        }

        /// <summary>Carga un AudioClip del proyecto por su GUID.</summary>
        private static AudioClip LoadAudioClip(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[RouteProgressSetup] No se encontró el clip de audio (GUID " + guid +
                                 "). El cartel quedará sin sonido; asígnalo a mano si quieres.");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>
        /// Construye el canvas world-space del panel de compleción con título, 6 líneas de stats
        /// y un botón Reset. Devuelve el componente RouteCompletionPanel ya con los textos cableados.
        /// </summary>
        private static RouteCompletionPanel BuildCompletionPanel(Vector3 worldPos, Transform hmd)
        {
            // Canvas raíz (world space).
            var canvasGo = new GameObject(PanelName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create RouteCompletionPanel");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(600f, 420f);
            canvasRt.position = worldPos;
            // Escala world-space: 600 px -> ~1.2 m de ancho.
            canvasGo.transform.localScale = Vector3.one * 0.002f;
            if (hmd != null)
            {
                // Orientar inicialmente mirando al jugador.
                Vector3 dir = canvasGo.transform.position - hmd.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    canvasGo.transform.rotation = Quaternion.LookRotation(dir);
            }

            // Contenedor "Content": es lo que se activa/desactiva al mostrar/ocultar el panel.
            // IMPORTANTE: debe ser un HIJO del canvas (no el canvas mismo), para que el componente
            // RouteCompletionPanel siga activo y escuchando el evento aunque el contenido esté oculto.
            var content = CreateUIChild(canvasRt, "Content", out RectTransform contentRt);
            Stretch(contentRt);

            // Fondo semitransparente.
            var bg = CreateUIChild(contentRt, "Background", out RectTransform bgRt);
            Stretch(bgRt);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.07f, 0.12f, 0.92f);

            // Botón Reset ARRIBA del todo (sobre el título), para que no tape las estadísticas.
            var panelComp = canvasGo.AddComponent<RouteCompletionPanel>();
            Button resetBtn = CreateButton(contentRt, "ResetButton", "Reintentar",
                new Vector2(0f, -22f), new Vector2(240f, 56f));

            // Título (debajo del botón).
            var title = CreateTMP(contentRt, "Title", "Ruta completada", 40, FontStyles.Bold,
                new Vector2(0f, -95f), new Vector2(560f, 60f), TextAlignmentOptions.Center);

            // Líneas de estadísticas (debajo del título).
            float y = -165f;
            const float step = 46f;
            TMP_Text timeT = CreateStatLine(contentRt, "TimeText", "Tiempo: --:--", ref y, step);
            TMP_Text startT = CreateStatLine(contentRt, "StartHeightText", "Altura inicial: -- m", ref y, step);
            TMP_Text finishT = CreateStatLine(contentRt, "FinishHeightText", "Altura final: -- m", ref y, step);
            TMP_Text maxT = CreateStatLine(contentRt, "MaxHeightText", "Altura máxima: -- m", ref y, step);
            TMP_Text climbedT = CreateStatLine(contentRt, "ClimbedHeightText", "Altura escalada: -- m", ref y, step);
            // onClick -> panel.OnResetButton (persistente).
            UnityEditor.Events.UnityEventTools.AddPersistentListener(resetBtn.onClick, panelComp.OnResetButton);

            // Cablear el contenedor (hijo) como raíz toggleable del panel.
            SetObjectRef(panelComp, "_panelRoot", content);
            SetObjectRef(panelComp, "_statusText", title);
            SetObjectRef(panelComp, "_timeText", timeT);
            SetObjectRef(panelComp, "_startHeightText", startT);
            SetObjectRef(panelComp, "_finishHeightText", finishT);
            SetObjectRef(panelComp, "_maxHeightText", maxT);
            SetObjectRef(panelComp, "_climbedHeightText", climbedT);

            // Dejar el contenido oculto en edición (en runtime Awake→Hide ya lo oculta, pero así
            // tampoco aparece flotando en el Editor). El componente RouteCompletionPanel sigue activo.
            content.SetActive(false);

            return panelComp;
        }

        /// <summary>
        /// Crea (si no existe) un marcador visual tipo "haz de luz" como hijo del checkpoint, para
        /// ubicarlo fácilmente en VR. Es un cilindro alto, emisivo y semitransparente, SIN collider
        /// (no interfiere con el trigger ni con la física). Idempotente: no duplica si ya hay un "Beacon".
        /// </summary>
        private static void EnsureBeacon(GameObject checkpoint, Color color)
        {
            const float height = 50f;
            const float radius = 0.5f;

            Transform existing = checkpoint.transform.Find("Beacon");
            if (existing != null)
            {
                existing.localPosition = new Vector3(0f, height * 0.5f, 0f);
                existing.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
                var existingMr = existing.GetComponent<MeshRenderer>();
                if (existingMr != null)
                    existingMr.sharedMaterial = BuildBeaconMaterial(color);
                return;
            }

            // Cilindro primitivo y le quitamos el collider.
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Beacon";
            Undo.RegisterCreatedObjectUndo(beacon, "Create Checkpoint Beacon");
            var col = beacon.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            beacon.transform.SetParent(checkpoint.transform, false);
            // El cilindro de Unity mide 2 m de alto por defecto; escalamos a la altura deseada.
            beacon.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            beacon.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

            // Material emisivo semitransparente (Built-in RP). Usamos "Particles/Standard Unlit"
            // que soporta color + transparencia + emisión y se ve como un haz brillante sin luces.
            var mr = beacon.GetComponent<MeshRenderer>();
            Material mat = BuildBeaconMaterial(color);
            if (mat != null)
            {
                mr.sharedMaterial = mat;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Componente que lo hace girar/pulsar suavemente para que llame la atención.
            beacon.AddComponent<RouteCheckpointBeacon>();
        }

        /// <summary>Crea un material de haz de luz brillante y semitransparente para Built-in RP.</summary>
        private static Material BuildBeaconMaterial(Color color)
        {
            // Preferimos un shader unlit transparente que siempre exista en Built-in.
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                Debug.LogWarning("[RouteProgressSetup] No se encontró un shader para el haz de luz; " +
                                 "el marcador puede verse opaco. Ajusta el material a mano si quieres.");
                shader = Shader.Find("Standard");
            }

            var mat = new Material(shader);
            var c = new Color(color.r, color.g, color.b, 0.35f); // semitransparente
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", c);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);

            // Modo transparente + emisión si el shader lo soporta (Particles/Standard Unlit).
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 2f); // Fade
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 2f); // brillo
            }
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        // ---------- Helpers de construcción UI ----------

        private static GameObject CreateUIChild(RectTransform parent, string name, out RectTransform rt)
        {
            var go = new GameObject(name, typeof(RectTransform));
            rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TMP_Text CreateTMP(RectTransform parent, string name, string text, float size,
            FontStyles style, Vector2 anchoredPos, Vector2 sizeDelta, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            // Anclado arriba-centro para layout vertical predecible.
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static TMP_Text CreateStatLine(RectTransform parent, string name, string text, ref float y, float step)
        {
            TMP_Text t = CreateTMP(parent, name, text, 30, FontStyles.Normal,
                new Vector2(0f, y), new Vector2(540f, 40f), TextAlignmentOptions.Center);
            y -= step;
            return t;
        }

        private static Button CreateButton(RectTransform parent, string name, string label,
            Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            // Anclado arriba-centro (igual que el título) para layout vertical predecible.
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = new Color(0.18f, 0.45f, 0.85f, 1f);

            var btn = go.GetComponent<Button>();

            // Label.
            var labelTmp = CreateTMP(rt, "Label", label, 28, FontStyles.Bold,
                Vector2.zero, size, TextAlignmentOptions.Center);
            var lrt = labelTmp.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            return btn;
        }

        // ---------- Helpers varios ----------

        /// <summary>Asigna una referencia a un campo [SerializeField] privado vía SerializedObject.</summary>
        private static void SetObjectRef(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[RouteProgressSetup] Campo '{fieldName}' no encontrado en {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Asigna un string a un campo [SerializeField] privado vía SerializedObject.</summary>
        private static void SetStringField(Object target, string fieldName, string value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[RouteProgressSetup] Campo '{fieldName}' no encontrado en {target.GetType().Name}.");
                return;
            }
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Busca el primer Transform de la escena (incluye inactivos) por nombre exacto.</summary>
        private static Transform FindByName(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == name)
                {
                    return t;
                }
            }
            return null;
        }
    }
}
