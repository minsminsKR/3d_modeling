using System;
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        static string RemoveClueFields(string json)
        {
            json = Regex.Replace(json, ",\"storyVersion\":-?[0-9]+", "");
            json = Regex.Replace(json, ",\"legacyClueExemptMask\":-?[0-9]+", "");
            json = Regex.Replace(json, ",\"firstClueRead\":(true|false)", "");
            return Regex.Replace(json, ",\"secondClueRead\":(true|false)", "");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TempleClueCheckpointMigratesLegacyAndRejectsInvalidProgress()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var checkpoint = Call(Get<Component>(session, "Prologue"), "Capture", 0f);
            var type = RequireType("TemplePrologueCheckpoint");
            // Simulate actual old files by removing all extension fields.
            foreach (int version in new[] { 1, 2, 3 })
            {
                Set(checkpoint, "version", version); Set(checkpoint, "storyVersion", 0);
                Set(checkpoint, "legacyClueExemptMask", 0);
                Set(checkpoint, "firstClueRead", false); Set(checkpoint, "secondClueRead", false);
                Set(checkpoint, "reviewGateWalk", version == 3);
                Set(checkpoint, "reviewPreviousPosition", version == 3 ? new Vector3(500, 0, 500) : Vector3.zero);
                string legacy = RemoveClueFields(JsonUtility.ToJson(checkpoint));
                Assert.That(legacy, Does.Not.Contain("storyVersion"));
                Assert.That(legacy, Does.Not.Contain("legacyClueExemptMask"));
                var migrated = Call(type, "FromJson", legacy);
                Assert.That(Get<int>(migrated, "storyVersion"), Is.EqualTo(2));
                Assert.That(Get<bool>(migrated, "firstClueRead"), Is.False);
                Assert.That(Get<bool>(migrated, "secondClueRead"), Is.False);
                Assert.That(Get<int>(migrated, "legacyClueExemptMask"), Is.EqualTo(3));
            }
            Set(checkpoint, "version", 2); Set(checkpoint, "reviewGateWalk", false);
            Set(checkpoint, "reviewPreviousPosition", Vector3.zero);
            string legacySerialization = (string)Call(checkpoint, "ToJson");
            Assert.That(legacySerialization, Does.Not.Contain("storyVersion"));
            Assert.That(legacySerialization, Does.Not.Contain("legacyClueExemptMask"));
            var copiedLegacy = Call(checkpoint, "Copy");
            Assert.That(Get<int>(copiedLegacy, "storyVersion"), Is.EqualTo(2));
            Assert.That(Get<bool>(copiedLegacy, "firstClueRead"), Is.False);
            Assert.That(Get<bool>(copiedLegacy, "secondClueRead"), Is.False);
            Assert.That(Get<int>(copiedLegacy, "legacyClueExemptMask"), Is.EqualTo(3));

            // Story schema 1 existed before the mask; its actual flags survive.
            Set(checkpoint, "storyVersion", 1); Set(checkpoint, "firstClueRead", true);
            Set(checkpoint, "secondClueRead", false); Set(checkpoint, "phase", 3);
            string v1 = (string)Call(checkpoint, "ToJson");
            Assert.That(v1, Does.Not.Contain("legacyClueExemptMask"));
            var oldExtension = Call(type, "FromJson", v1);
            Assert.That(Get<int>(oldExtension, "storyVersion"), Is.EqualTo(1));
            Assert.That(Get<bool>(oldExtension, "firstClueRead"), Is.True);
            Assert.That(Get<bool>(oldExtension, "secondClueRead"), Is.False);
            Assert.That(Get<int>(oldExtension, "legacyClueExemptMask"), Is.Zero);
            Call(oldExtension, "Copy");

            Set(checkpoint, "storyVersion", 2); Set(checkpoint, "firstClueRead", false);
            Set(checkpoint, "secondClueRead", false); Set(checkpoint, "legacyClueExemptMask", 3);
            var copy = Call(checkpoint, "Copy");
            Assert.That(Get<bool>(copy, "firstClueRead"), Is.False);
            Assert.That(Get<bool>(copy, "secondClueRead"), Is.False);
            Assert.That(Get<int>(copy, "legacyClueExemptMask"), Is.EqualTo(3));
            Set(copy, "phase", 4); Call(copy, "Validate");
            Set(copy, "secondClueRead", true); Set(copy, "legacyClueExemptMask", 1);
            Call(copy, "Validate"); // First observation is still unknown.
            var later = Call(copy, "Copy");
            Assert.That(Get<bool>(later, "firstClueRead"), Is.False);
            Assert.That(Get<bool>(later, "secondClueRead"), Is.True);
            Assert.That(Get<int>(later, "legacyClueExemptMask"), Is.EqualTo(1));
            Set(copy, "firstClueRead", true);
            Assert.Throws<ArgumentException>(() => Call(copy, "Validate")); // Read and exempt cannot both be set.
            Set(copy, "firstClueRead", false); Set(copy, "legacyClueExemptMask", 0);
            Assert.Throws<ArgumentException>(() => Call(copy, "Validate")); // Missing first observation/exemption.
            Set(copy, "legacyClueExemptMask", 4);
            Assert.Throws<ArgumentException>(() => Call(copy, "Validate"));
            Set(copy, "secondClueRead", false); Set(copy, "legacyClueExemptMask", 1);
            Assert.Throws<ArgumentException>(() => Call(copy, "Validate")); // Cleared second exemption without reading.
            Set(copy, "legacyClueExemptMask", 2);
            Assert.Throws<ArgumentException>(() => Call(copy, "Validate")); // Cleared first exemption without reading.
            Set(copy, "secondClueRead", true);
            Set(copy, "legacyClueExemptMask", 1); Set(copy, "storyVersion", 3);
            Assert.Throws<ArgumentException>(() => Call(copy, "Validate"));

            string validJson = (string)Call(checkpoint, "ToJson");
            string incomplete = Regex.Replace(validJson, ",\\s*\"secondClueRead\":\\s*(true|false)", "");
            string noMask = Regex.Replace(validJson, ",\\s*\"legacyClueExemptMask\":\\s*-?[0-9]+", "");
            string orphan = Regex.Replace(validJson, ",\\s*\"storyVersion\":\\s*-?[0-9]+", "");
            string zero = Regex.Replace(validJson, "\"storyVersion\":\\s*2", "\"storyVersion\":0");
            string duplicate = Regex.Replace(validJson, "\"storyVersion\":\\s*2", "\"storyVersion\":2,\"storyVersion\":2");
            string nullVersion = Regex.Replace(validJson, "\"storyVersion\":\\s*2", "\"storyVersion\":null");
            string nullMask = Regex.Replace(validJson, "\"legacyClueExemptMask\":\\s*3", "\"legacyClueExemptMask\":null");
            string nullFirst = Regex.Replace(validJson, "\"firstClueRead\":\\s*false", "\"firstClueRead\":null");
            string nullSecond = Regex.Replace(validJson, "\"secondClueRead\":\\s*false", "\"secondClueRead\":null");
            foreach (string malformed in new[] { incomplete, noMask, orphan, zero, duplicate,
                nullVersion, nullMask, nullFirst, nullSecond })
                Assert.Throws<ArgumentException>(() => Call(type, "FromJson", malformed));
        }
    }
}
