using System;
using System.Collections.Generic;

namespace FlatOutLike.Views.UI.Hub
{
    /// <summary>
    /// Неизменяемый держатель всех ценников зданий сцены (pure C#, шаг 12a) — зависимость
    /// <see cref="BuildingPanelPresenter"/>.
    /// <para>
    /// Зачем отдельный тип: VContainer не собирает коллекцию из нескольких Singleton-регистраций
    /// ОДНОГО implementation type — N раз <c>RegisterComponent(panel)</c> падает в
    /// <c>CollectionInstanceProvider.Add</c> с «Conflict implementation type». Поэтому панели
    /// регистрируются одним объектом-списком.
    /// </para>
    /// <para>
    /// У <see cref="BuildingPanelView"/> нет <c>[Inject]</c> — инъекция в MonoBehaviour не нужна,
    /// значит и <c>RegisterComponent</c> не нужен. Сам держатель — plain C# объект, для него
    /// <c>RegisterInstance</c> корректен (CLAUDE.md «Нюансы VContainer»).
    /// </para>
    /// </summary>
    public sealed class BuildingPanelViews
    {
        /// <summary>Ценники сцены; не null (пустой список, если панелей нет).</summary>
        public IReadOnlyList<BuildingPanelView> Items { get; }

        public BuildingPanelViews(BuildingPanelView[] views)
        {
            // Копия — внешний массив не может изменить содержимое держателя.
            Items = views == null
                ? Array.Empty<BuildingPanelView>()
                : (BuildingPanelView[])views.Clone();
        }
    }
}
