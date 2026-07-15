using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlatOutLike.Editor.Tools
{
    /// <summary>
    /// Выключает лишние дефолтные флаги <see cref="Graphic.raycastTarget"/> и
    /// <see cref="MaskableGraphic.maskable"/> на всём UI проекта (Unity включает оба флага
    /// по умолчанию, хотя raycastTarget нужен только элементам, реально ловящим ввод, а
    /// maskable — только элементам под родительской маской). Лишние true раздувают
    /// GraphicRaycaster-проход и Mask-стенсил без пользы.
    ///
    /// Обрабатывает два места:
    ///  1) Все Canvas активной сцены — только объекты, НЕ являющиеся частью prefab-инстанса
    ///     (иначе получаем per-instance override вместо правки самого префаба).
    ///  2) Префаб Assets/Development/Prefabs/UI/MobileInputCanvas.prefab напрямую через
    ///     PrefabUtility.LoadPrefabContents (образец — BuildMobileInputCanvasEditor.StyleMobileInputButtons).
    ///
    /// Идемпотентность: значение флага меняется только если реально отличается —
    /// повторный запуск даёт 0 изменений и попадает в лог как "changed: 0".
    /// </summary>
    public static class OptimizeUiGraphicFlagsEditor
    {
        private const string MobileInputCanvasPrefabPath =
            "Assets/Development/Prefabs/UI/MobileInputCanvas.prefab";

        [MenuItem("FlatOutLike/Tools/Optimize UI Raycast And Maskable Flags")]
        public static void OptimizeUiGraphicFlags()
        {
            Undo.SetCurrentGroupName("Optimize UI Raycast And Maskable Flags");
            int undoGroup = Undo.GetCurrentGroup();

            var sceneStats = new Stats();
            bool sceneChanged = ProcessScene(sceneStats);

            if (sceneChanged)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);

            var prefabStats = new Stats();
            ProcessMobileInputCanvasPrefab(prefabStats);

            Debug.Log("[Editor] Optimize UI Raycast And Maskable Flags — сцена '" +
                      SceneManager.GetActiveScene().name + "': raycastTarget выключено " +
                      sceneStats.RaycastDisabled + ", включено " + sceneStats.RaycastEnabled +
                      "; maskable выключено " + sceneStats.MaskableDisabled +
                      ", включено " + sceneStats.MaskableEnabled +
                      ". Префаб MobileInputCanvas: raycastTarget выключено " +
                      prefabStats.RaycastDisabled + ", включено " + prefabStats.RaycastEnabled +
                      "; maskable выключено " + prefabStats.MaskableDisabled +
                      ", включено " + prefabStats.MaskableEnabled + ".");
        }

        /// <summary>Возвращает true, если в сцене были реальные изменения.</summary>
        private static bool ProcessScene(Stats stats)
        {
            var canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            bool anyChanged = false;

            foreach (var canvas in canvases)
            {
                // Сверху вниз по иерархии — эвристика raycastTarget опирается на уже
                // выставленный флаг родителя (GetComponentsInChildren обходит именно так).
                var graphics = canvas.GetComponentsInChildren<Graphic>(true);

                foreach (var graphic in graphics)
                {
                    // per-instance override не плодим — правится через сам префаб.
                    // ВНИМАНИЕ: сейчас prefab-проходом покрыт только MobileInputCanvas;
                    // если другой UI станет prefab-инстансом, он молча выпадет из обработки —
                    // добавить его префаб в ProcessMobileInputCanvasPrefab-аналог.
                    if (PrefabUtility.IsPartOfPrefabInstance(graphic.gameObject))
                        continue;

                    if (ApplyFlags(graphic, stats, useUndo: true))
                        anyChanged = true;
                }
            }

            return anyChanged;
        }

        private static void ProcessMobileInputCanvasPrefab(Stats stats)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MobileInputCanvasPrefabPath) == null)
            {
                Debug.LogWarning("[Editor] " + MobileInputCanvasPrefabPath +
                                  " не найден — пропускаем обработку префаба.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(MobileInputCanvasPrefabPath);
            try
            {
                bool changed = false;
                var graphics = root.GetComponentsInChildren<Graphic>(true);
                foreach (var graphic in graphics)
                {
                    if (ApplyFlags(graphic, stats, useUndo: false))
                        changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, MobileInputCanvasPrefabPath);
                    Debug.Log("[Editor] Optimize UI Raycast And Maskable Flags: префаб сохранён — " +
                              MobileInputCanvasPrefabPath + ".");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Применяет эвристики raycastTarget/maskable к одному Graphic. Возвращает true,
        /// если хотя бы один флаг реально изменился.
        /// </summary>
        private static bool ApplyFlags(Graphic graphic, Stats stats, bool useUndo)
        {
            bool changed = false;

            bool wantRaycast = ResolveRaycastTarget(graphic);
            if (graphic.raycastTarget != wantRaycast)
            {
                if (useUndo) Undo.RecordObject(graphic, "Optimize UI raycastTarget");
                graphic.raycastTarget = wantRaycast;
                if (useUndo) EditorUtility.SetDirty(graphic);

                if (wantRaycast) stats.RaycastEnabled++;
                else stats.RaycastDisabled++;
                changed = true;
            }

            var maskable = graphic as MaskableGraphic;
            if (maskable != null)
            {
                bool wantMaskable = ResolveMaskable(maskable);
                if (maskable.maskable != wantMaskable)
                {
                    if (useUndo) Undo.RecordObject(maskable, "Optimize UI maskable");
                    maskable.maskable = wantMaskable;
                    if (useUndo) EditorUtility.SetDirty(maskable);

                    if (wantMaskable) stats.MaskableEnabled++;
                    else stats.MaskableDisabled++;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// raycastTarget = true, если сам GameObject ловит ввод (Selectable или
        /// пойнтер-интерфейс). Иначе идём вверх и ищем ближайшего предка-ЛОВЦА или
        /// предка-обработчика — это разные роли, когда обработчик сидит на GO без своего
        /// Graphic (VirtualJoystickView на корне стика: ловец — дочерний Background-Image):
        ///  - первым встретился предок-Graphic с raycastTarget=true (флаг уже актуализирован
        ///    этим же проходом — обход сверху вниз) → он поймает ввод за нас, текущему false
        ///    (Handle внутри Background);
        ///  - первым встретился обработчик, а ловца по пути не было → ловец — текущий
        ///    Graphic, оставляем true (Background под VirtualJoystickView).
        /// Допущение: ребёнок лежит внутри rect предка-ловца (для текущего UI верно).
        /// </summary>
        private static bool ResolveRaycastTarget(Graphic graphic)
        {
            if (IsPointerHandler(graphic.gameObject))
                return true;

            for (var t = graphic.transform.parent; t != null; t = t.parent)
            {
                var ancestorGraphic = t.GetComponent<Graphic>();
                if (ancestorGraphic != null && ancestorGraphic.raycastTarget)
                    return false; // ловец — графика предка, текущему можно выключить.

                if (IsPointerHandler(t.gameObject))
                    return true; // дошли до обработчика без ловца по пути — ловец это мы.
            }

            return false; // обработчика во всей цепочке предков нет — декоративный элемент.
        }

        private static bool IsPointerHandler(GameObject go)
        {
            if (go.GetComponent<Selectable>() != null)
                return true;

            if (go.GetComponent<IPointerDownHandler>() != null) return true;
            if (go.GetComponent<IPointerUpHandler>() != null) return true;
            if (go.GetComponent<IPointerClickHandler>() != null) return true;
            if (go.GetComponent<IDragHandler>() != null) return true;
            if (go.GetComponent<IBeginDragHandler>() != null) return true;
            if (go.GetComponent<IEndDragHandler>() != null) return true;
            if (go.GetComponent<IScrollHandler>() != null) return true;

            return false;
        }

        /// <summary>
        /// maskable = true только если среди предков (не считая сам объект — маска режет
        /// детей, а не себя) есть Mask или RectMask2D.
        /// </summary>
        private static bool ResolveMaskable(MaskableGraphic graphic)
        {
            for (var t = graphic.transform.parent; t != null; t = t.parent)
            {
                if (t.GetComponent<Mask>() != null) return true;
                if (t.GetComponent<RectMask2D>() != null) return true;
            }

            return false;
        }

        // Класс, не struct: экземпляр передаётся в Process*/ApplyFlags и мутируется там —
        // struct по значению копировался бы, и сводка в логе всегда была бы нулевой.
        private sealed class Stats
        {
            public int RaycastDisabled;
            public int RaycastEnabled;
            public int MaskableDisabled;
            public int MaskableEnabled;
        }
    }
}
