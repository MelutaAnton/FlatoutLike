using System;
using System.Collections.Generic;
using FlatOutLike.Models;
using FlatOutLike.Services.Progression;
using NUnit.Framework;
using UnityEngine;

namespace FlatOutLike.Tests
{
    /// <summary>
    /// Юнит-тесты <see cref="PlayerPrefsHubSaveStorage"/> (Шаг 12a): round-trip save-блока хаба
    /// и пустой Load без сейва. PlayerPrefs в EditMode ДЕЛИТ хранилище с живым сейвом разработчика
    /// (те же ключи FlatOutLike.Hub.*), поэтому SetUp бэкапит реальные значения, а TearDown
    /// удаляет тестовые и восстанавливает бэкап — прогон тестов не стирает плейтестовую стройку.
    /// </summary>
    [TestFixture]
    public class PlayerPrefsHubSaveStorageTests
    {
        // Дублируют формат private-ключей хранилища осознанно: тест проверяет ЧЁРНЫЙ ЯЩИК
        // (реальные ключи PlayerPrefs), а не внутреннюю структуру класса.
        private const string KeyPrefix = "FlatOutLike.Hub.Build.";
        private const string StagesSuffix = ".Stages";
        private const string InvestedSuffix = ".Invested";

        private PlayerPrefsHubSaveStorage _storage;

        // Бэкап живого dev-сейва: ключ → значение (null — ключа не было).
        private readonly Dictionary<string, int?> _backup = new Dictionary<string, int?>();

        [SetUp]
        public void SetUp()
        {
            _storage = new PlayerPrefsHubSaveStorage();
            BackupRealSave();
            DeleteAllKeys();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteAllKeys();
            RestoreRealSave();
        }

        // ── Тесты ───────────────────────────────────────────────────────────────

        [Test]
        public void Load_WithoutSave_ReturnsEmptyDataNotNull()
        {
            var data = _storage.Load();

            Assert.IsNotNull(data);
            Assert.IsFalse(data.TryGet(BuildingId.HighJumpTower, out _));
            CollectionAssert.IsEmpty(data.Records);
        }

        [Test]
        public void SaveThenLoad_RoundtripsRecords()
        {
            var data = new HubSaveData();
            data.Set(BuildingId.HighJumpTower, 2, 17);
            data.Set(BuildingId.TicketBooth, 1, 0);

            _storage.Save(data);
            var loaded = _storage.Load();

            Assert.IsTrue(loaded.TryGet(BuildingId.HighJumpTower, out var tower));
            Assert.AreEqual(2, tower.CompletedStages);
            Assert.AreEqual(17, tower.InvestedInStage);

            Assert.IsTrue(loaded.TryGet(BuildingId.TicketBooth, out var booth));
            Assert.AreEqual(1, booth.CompletedStages);
            Assert.AreEqual(0, booth.InvestedInStage);

            Assert.IsFalse(loaded.TryGet(BuildingId.Tribunes, out _), "Несохранённое здание не должно получить запись.");
        }

        [Test]
        public void Save_WritesKeysByNumericBuildingId()
        {
            var data = new HubSaveData();
            data.Set(BuildingId.HighJumpTower, 3, 5);

            _storage.Save(data);

            int id = (int)BuildingId.HighJumpTower;
            Assert.AreEqual(3, PlayerPrefs.GetInt(KeyPrefix + id + StagesSuffix, -1));
            Assert.AreEqual(5, PlayerPrefs.GetInt(KeyPrefix + id + InvestedSuffix, -1));
        }

        [Test]
        public void Save_OverwritesPreviousValues()
        {
            var data = new HubSaveData();
            data.Set(BuildingId.Tribunes, 0, 40);
            _storage.Save(data);

            data.Set(BuildingId.Tribunes, 1, 3);
            _storage.Save(data);

            Assert.IsTrue(_storage.Load().TryGet(BuildingId.Tribunes, out var record));
            Assert.AreEqual(1, record.CompletedStages);
            Assert.AreEqual(3, record.InvestedInStage);
        }

        // ── Бэкап/восстановление реального сейва ────────────────────────────────

        private static IEnumerable<string> AllKeys()
        {
            foreach (BuildingId id in Enum.GetValues(typeof(BuildingId)))
            {
                yield return KeyPrefix + (int)id + StagesSuffix;
                yield return KeyPrefix + (int)id + InvestedSuffix;
            }
        }

        private void BackupRealSave()
        {
            _backup.Clear();
            foreach (var key in AllKeys())
                _backup[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
        }

        private void RestoreRealSave()
        {
            foreach (var pair in _backup)
            {
                if (pair.Value.HasValue)
                    PlayerPrefs.SetInt(pair.Key, pair.Value.Value);
                else
                    PlayerPrefs.DeleteKey(pair.Key);
            }

            PlayerPrefs.Save();
        }

        private static void DeleteAllKeys()
        {
            foreach (var key in AllKeys())
                PlayerPrefs.DeleteKey(key);
        }
    }
}
