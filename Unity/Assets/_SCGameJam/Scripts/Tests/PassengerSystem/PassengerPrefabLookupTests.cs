using System.Collections.Generic;
using NUnit.Framework;
using SCJam.Common;
using SCJam.PassengerSystem;
using UnityEngine;

namespace SCJam.Tests.PassengerSystem
{
    public class PassengerPrefabLookupTests
    {
        private readonly List<GameObject> _spawnedGameObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawned in _spawnedGameObjects)
                Object.DestroyImmediate(spawned);

            _spawnedGameObjects.Clear();
        }

        private PassengerController CreatePrefab()
        {
            GameObject gameObject = new();
            _spawnedGameObjects.Add(gameObject);
            return gameObject.AddComponent<PassengerController>();
        }

        [Test]
        public void Build_ResolvesPrefabForItsMappedColor()
        {
            PassengerController orangePrefab = CreatePrefab();
            PassengerPrefabMapping[] mappings = { new(PuzzleColor.Orange, orangePrefab) };
            List<string> errors = new();

            PassengerPrefabLookup lookup = PassengerPrefabLookup.Build(mappings, new[] { PuzzleColor.Orange }, errors);

            Assert.IsTrue(lookup.TryGetPrefab(PuzzleColor.Orange, out PassengerController resolvedPrefab));
            Assert.AreEqual(orangePrefab, resolvedPrefab);
            Assert.IsEmpty(errors);
        }

        [Test]
        public void Build_RejectsDuplicateMappingsForTheSameColor()
        {
            PassengerController firstPrefab = CreatePrefab();
            PassengerController secondPrefab = CreatePrefab();
            PassengerPrefabMapping[] mappings =
            {
                new(PuzzleColor.Orange, firstPrefab),
                new(PuzzleColor.Orange, secondPrefab)
            };
            List<string> errors = new();

            PassengerPrefabLookup lookup = PassengerPrefabLookup.Build(mappings, null, errors);

            Assert.IsFalse(lookup.TryGetPrefab(PuzzleColor.Orange, out _));
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("duplicate", errors[0]);
            StringAssert.Contains(nameof(PuzzleColor.Orange), errors[0]);
        }

        [Test]
        public void Build_DetectsColorMissingFromRequiredColors()
        {
            PassengerPrefabMapping[] mappings = { new(PuzzleColor.Orange, CreatePrefab()) };
            List<string> errors = new();

            PassengerPrefabLookup lookup = PassengerPrefabLookup.Build(mappings, new[] { PuzzleColor.Orange, PuzzleColor.Pink }, errors);

            Assert.IsFalse(lookup.TryGetPrefab(PuzzleColor.Pink, out _));
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("no passenger prefab mapping", errors[0]);
            StringAssert.Contains(nameof(PuzzleColor.Pink), errors[0]);
        }

        [Test]
        public void Build_DetectsNullPrefab()
        {
            PassengerPrefabMapping[] mappings = { new(PuzzleColor.Orange, null) };
            List<string> errors = new();

            PassengerPrefabLookup lookup = PassengerPrefabLookup.Build(mappings, null, errors);

            Assert.IsFalse(lookup.TryGetPrefab(PuzzleColor.Orange, out _));
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("null prefab", errors[0]);
        }
    }
}
