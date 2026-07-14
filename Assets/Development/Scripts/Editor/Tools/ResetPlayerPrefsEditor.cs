using UnityEditor;
using UnityEngine;

namespace FlatOutLike.Editor.Tools
{
    /// <summary>
    /// Служебный editor-инструмент для сброса dev-сейва профиля игрока (монеты, купленные
    /// машины, апгрейды и прочие ключи, см. PlayerPrefsProfileStorage).
    ///
    /// Не завязан на конкретный список ключей — удаляет PlayerPrefs целиком, поэтому не
    /// требует правок при добавлении новых ключей профиля.
    ///
    /// Undo для PlayerPrefs не поддерживается движком — операция необратима, поэтому
    /// перед удалением обязательно спрашиваем подтверждение.
    /// </summary>
    public static class ResetPlayerPrefsEditor
    {
        [MenuItem("FlatOutLike/Tools/Reset Player Prefs (Dev Save)")]
        public static void ResetPlayerPrefs()
        {
            var message =
                "Будут удалены ВСЕ PlayerPrefs проекта (dev-сейв профиля игрока: монеты, " +
                "купленные машины, апгрейды и прочие ключи).\n\n" +
                "Действие необратимо и не поддерживает Undo.";

            if (Application.isPlaying)
            {
                message +=
                    "\n\nВНИМАНИЕ: игра сейчас запущена (Play Mode). Живые сервисы могут " +
                    "пересохранить профиль из памяти при выходе из Play Mode, и сброс " +
                    "будет перезаписан. Рекомендуется сбрасывать вне Play Mode.";
            }

            var confirmed = EditorUtility.DisplayDialog(
                "Reset Player Prefs",
                message,
                "Удалить",
                "Отмена");

            if (!confirmed)
            {
                Debug.Log("[Editor] Сброс PlayerPrefs отменён пользователем.");
                return;
            }

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            Debug.Log("[Editor] PlayerPrefs полностью сброшены (dev-сейв удалён).");
        }
    }
}
