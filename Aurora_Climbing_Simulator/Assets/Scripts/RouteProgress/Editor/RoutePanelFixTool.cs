using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Aurora.RouteProgress;

namespace Aurora.RouteProgress.EditorTools
{
    /// <summary>
    /// Herramienta de Editor NO destructiva para arreglar/mejorar el sistema de progreso ya
    /// existente en la escena, SIN mover los checkpoints (a diferencia de "Setup In Scene", que
    /// los reposiciona junto al spawn). Menú: <b>Aurora ▸ Route Progress ▸ Fix Panel &amp; Beacons</b>.
    ///
    /// Qué hace (todo idempotente):
    ///   • Añade un botón "Volver al menú" al panel de compleción y lo cablea a
    ///     <see cref="RouteCompletionPanel.OnReturnToMenuButton"/>.
    ///   • Hace MUCHO más altos los haces de luz (Beacon) de los dos checkpoints, para verlos
    ///     desde cualquier punto del mapa. No mueve los checkpoints.
    ///   • Garantiza un Rigidbody kinemático en cada checkpoint, para que los OnTriggerEnter
    ///     disparen de forma fiable en VR aunque el rig del jugador no tenga Rigidbody.
    ///   • Imprime un diagnóstico del cableado por consola.
    /// </summary>
    public static class RoutePanelFixTool
    {
        private const string MenuRoot = "Aurora/Route Progress/";

        // Altura del haz de luz: muy alto para verse desde cualquier parte del mapa.
        private const float BeaconHeight = 300f;
        private const float BeaconRadius = 0.9f;

        [MenuItem(MenuRoot + "Fix Panel & Beacons", false, 1)]
        public static void FixPanelAndBeacons()
        {
            int changes = 0;

            // --- 1. Panel: añadir y cablear el botón "Volver al menú" ---
            var panel = Object.FindFirstObjectByType<RouteCompletionPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                Debug.LogWarning("[RoutePanelFix] No se encontró RouteCompletionPanel en la escena. " +
                                 "Abre MainScene_backup o ejecuta primero 'Setup In Scene'.");
            }
            else
            {
                if (EnsureReturnButton(panel)) changes++;
                LogPanelDiagnostics(panel);
            }

            // --- 2. Checkpoints: beacons más altos + Rigidbody kinemático + diagnóstico ---
            var start = Object.FindFirstObjectByType<RouteStartCheckpoint>(FindObjectsInactive.Include);
            var finish = Object.FindFirstObjectByType<RouteFinishCheckpoint>(FindObjectsInactive.Include);

            if (start != null)
            {
                if (RaiseBeacon(start.gameObject, new Color(1f, 0.9f, 0.2f))) changes++;
                if (EnsureKinematicRigidbody(start.gameObject)) changes++;
                LogCheckpointDiagnostics("INICIO", start.gameObject);
            }
            else Debug.LogWarning("[RoutePanelFix] No se encontró RouteStartCheckpoint en la escena.");

            if (finish != null)
            {
                if (RaiseBeacon(finish.gameObject, new Color(0.3f, 1f, 0.4f))) changes++;
                if (EnsureKinematicRigidbody(finish.gameObject)) changes++;
                LogCheckpointDiagnostics("FIN", finish.gameObject);
            }
            else Debug.LogWarning("[RoutePanelFix] No se encontró RouteFinishCheckpoint en la escena.");

            if (changes > 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }

            Debug.Log($"[RoutePanelFix] Listo. Cambios aplicados: {changes}. " +
                      "Guarda la escena (Ctrl+S) si todo se ve bien.");

            if (panel != null) Selection.activeObject = panel.gameObject;
        }

        // -------------------------------------------------------------------------------------
        //  Botón "Volver al menú"
        // -------------------------------------------------------------------------------------

        /// <summary>
        /// Crea (si no existe) un botón "Volver al menú" en el contenedor (_panelRoot) del panel,
        /// y lo cablea a OnReturnToMenuButton. Idempotente: no duplica si ya hay un "ReturnButton".
        /// </summary>
        private static bool EnsureReturnButton(RouteCompletionPanel panel)
        {
            // Localizar el contenedor (Content) que togglea el panel.
            var so = new SerializedObject(panel);
            var rootProp = so.FindProperty("_panelRoot");
            GameObject content = rootProp != null ? rootProp.objectReferenceValue as GameObject : null;
            if (content == null) content = panel.gameObject; // respaldo

            var contentRt = content.GetComponent<RectTransform>();
            if (contentRt == null)
            {
                Debug.LogWarning("[RoutePanelFix] El _panelRoot del panel no tiene RectTransform; " +
                                 "no se puede añadir el botón 'Volver al menú' automáticamente.", panel);
                return false;
            }

            // ¿Ya existe?
            Transform existing = content.transform.Find("ReturnButton");
            if (existing != null)
            {
                Debug.Log("[RoutePanelFix] El botón 'Volver al menú' ya existe; no se duplica.", panel);
                return false;
            }

            // Crear el botón anclado abajo-centro del contenedor.
            Button btn = CreateButton(contentRt, "ReturnButton", "Volver al menú",
                new Vector2(0f, -360f), new Vector2(260f, 54f),
                new Color(0.7f, 0.2f, 0.25f, 1f)); // rojo apagado para distinguir de "Reintentar"

            // onClick -> panel.OnReturnToMenuButton (persistente).
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, panel.OnReturnToMenuButton);

            Debug.Log("[RoutePanelFix] Botón 'Volver al menú' creado y cableado a OnReturnToMenuButton.", panel);
            return true;
        }

        // -------------------------------------------------------------------------------------
        //  Beacons más altos
        // -------------------------------------------------------------------------------------

        /// <summary>
        /// Sube la altura del haz "Beacon" del checkpoint (sin mover el checkpoint). Si no existe el
        /// beacon, lo crea. Devuelve true si modificó algo.
        /// </summary>
        private static bool RaiseBeacon(GameObject checkpoint, Color color)
        {
            Transform beacon = checkpoint.transform.Find("Beacon");
            if (beacon == null)
            {
                beacon = CreateBeacon(checkpoint, color).transform;
            }

            beacon.localPosition = new Vector3(0f, BeaconHeight * 0.5f, 0f);
            beacon.localScale = new Vector3(BeaconRadius * 2f, BeaconHeight * 0.5f, BeaconRadius * 2f);
            EditorUtility.SetDirty(beacon);
            Debug.Log($"[RoutePanelFix] Beacon de '{checkpoint.name}' elevado a {BeaconHeight} m.", checkpoint);
            return true;
        }

        private static GameObject CreateBeacon(GameObject checkpoint, Color color)
        {
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Beacon";
            Undo.RegisterCreatedObjectUndo(beacon, "Create Checkpoint Beacon");
            var col = beacon.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            beacon.transform.SetParent(checkpoint.transform, false);

            var mr = beacon.GetComponent<MeshRenderer>();
            mr.sharedMaterial = BuildBeaconMaterial(color);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            beacon.AddComponent<RouteCheckpointBeacon>();
            return beacon;
        }

        private static Material BuildBeaconMaterial(Color color)
        {
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader);
            var c = new Color(color.r, color.g, color.b, 0.35f);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", c);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 2f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 2f);
            }
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        // -------------------------------------------------------------------------------------
        //  Rigidbody kinemático (fiabilidad de triggers en VR)
        // -------------------------------------------------------------------------------------

        /// <summary>
        /// Garantiza un Rigidbody kinemático (sin gravedad) en el checkpoint. Para que Unity dispare
        /// OnTriggerEnter, AL MENOS uno de los dos colliders debe tener Rigidbody; ponerlo en el
        /// checkpoint asegura la detección aunque el rig del jugador se mueva por transform sin físicas.
        /// </summary>
        private static bool EnsureKinematicRigidbody(GameObject checkpoint)
        {
            var rb = checkpoint.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Asegurar que no perturbe la escena.
                if (!rb.isKinematic || rb.useGravity)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    EditorUtility.SetDirty(rb);
                    return true;
                }
                return false;
            }

            rb = Undo.AddComponent<Rigidbody>(checkpoint);
            rb.isKinematic = true;
            rb.useGravity = false;
            EditorUtility.SetDirty(rb);
            Debug.Log($"[RoutePanelFix] Rigidbody kinemático añadido a '{checkpoint.name}' " +
                      "(triggers VR más fiables).", checkpoint);
            return true;
        }

        // -------------------------------------------------------------------------------------
        //  Diagnóstico
        // -------------------------------------------------------------------------------------

        private static void LogPanelDiagnostics(RouteCompletionPanel panel)
        {
            var so = new SerializedObject(panel);
            Object mgr = so.FindProperty("_manager")?.objectReferenceValue;
            Object root = so.FindProperty("_panelRoot")?.objectReferenceValue;
            Object cam = so.FindProperty("_cameraTransform")?.objectReferenceValue;
            Debug.Log($"[RoutePanelFix][DIAG] Panel='{panel.name}' activo={panel.gameObject.activeInHierarchy} | " +
                      $"_manager={(mgr ? mgr.name : "NULL")} | _panelRoot={(root ? root.name : "NULL")} | " +
                      $"_cameraTransform={(cam ? cam.name : "NULL")}", panel);
        }

        private static void LogCheckpointDiagnostics(string label, GameObject checkpoint)
        {
            var col = checkpoint.GetComponent<Collider>();
            string colInfo = "SIN COLLIDER";
            if (col is BoxCollider box)
                colInfo = $"Box isTrigger={box.isTrigger} size={box.size} (mundo≈{Vector3.Scale(box.size, checkpoint.transform.lossyScale)})";
            else if (col != null)
                colInfo = $"{col.GetType().Name} isTrigger={col.isTrigger}";

            var cp = checkpoint.GetComponent<RouteCheckpointBase>();
            var so = new SerializedObject(cp);
            Object mgr = so.FindProperty("_manager")?.objectReferenceValue;
            Object pt = so.FindProperty("_playerTransform")?.objectReferenceValue;
            string tag = so.FindProperty("_playerTag")?.stringValue;
            bool hasRb = checkpoint.GetComponent<Rigidbody>() != null;

            Debug.Log($"[RoutePanelFix][DIAG] Checkpoint {label} '{checkpoint.name}' pos={checkpoint.transform.position} | " +
                      $"{colInfo} | Rigidbody={hasRb} | _manager={(mgr ? mgr.name : "NULL")} | " +
                      $"_playerTransform={(pt ? pt.name : "NULL")} | _playerTag='{tag}'", checkpoint);
        }

        // -------------------------------------------------------------------------------------
        //  Helpers UI (autocontenidos, copiados del estilo de RouteProgressSetup)
        // -------------------------------------------------------------------------------------

        private static Button CreateButton(RectTransform parent, string name, string label,
            Vector2 anchoredPos, Vector2 size, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(go, "Create Return Button");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            go.GetComponent<Image>().color = bgColor;
            var btn = go.GetComponent<Button>();

            var labelTmp = CreateTMP(rt, "Label", label, 26, FontStyles.Bold, TextAlignmentOptions.Center);
            var lrt = labelTmp.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            return btn;
        }

        private static TMP_Text CreateTMP(RectTransform parent, string name, string text, float size,
            FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }
    }
}
