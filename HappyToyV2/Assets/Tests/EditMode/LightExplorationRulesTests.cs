using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class LightExplorationRulesTests
    {
        [TestCase(90f, 10f, true, true, 82.5f)]
        [TestCase(180f, 120f, true, true, 90f)]
        [TestCase(180f, 240f, true, true, 0f)]
        [TestCase(90f, 10f, false, true, 90f)]
        [TestCase(90f, 10f, true, false, 90f)]
        [TestCase(90f, 10f, false, false, 90f)]
        [TestCase(1f, 10f, true, true, 0f)]
        public void BatteryCostsOnlyActualLitGameplayTime(float charge, float seconds, bool lit, bool active, float expected)
        {
            Assert.That((float)Call(RequireType("FlashlightChargeRules"), "Drain", charge, seconds, lit, active), Is.EqualTo(expected));
        }
        [Test]
        public void RefillClampsAndNonFiniteInputsCannotProduceFreeLight()
        {
            var rules = RequireType("FlashlightChargeRules");
            Assert.That((float)Call(rules, "Refill", 150f), Is.EqualTo(180f));
            Assert.That((float)Call(rules, "Refill", 0f), Is.EqualTo(90f));
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -1f, 181f })
            {
                Assert.Throws<ArgumentException>(() => Call(rules, "Drain", invalid, 1f, true, true));
                Assert.Throws<ArgumentException>(() => Call(rules, "Refill", invalid));
            }
            Assert.Throws<ArgumentException>(() => Call(rules, "Drain", 90f, float.NaN, false, false));
        }
        object Lighting(string text) => JsonUtility.FromJson(text, RequireType("LightExplorationCheckpoint"));
        const string Valid = "{\"version\":1,\"charge\":45,\"packsCollected\":1,\"depletions\":1,\"batteries\":[{\"id\":\"a\",\"available\":false}],\"candles\":[{\"id\":\"c\",\"lit\":true}]}";
        [Test]
        public void LightingSchemaRejectsDuplicateIdentityImpossibleStockAndUnknownVersion()
        {
            Call(Lighting(Valid), "Validate");
            foreach (string invalid in new[]
            {
                Valid.Replace("\"version\":1", "\"version\":2"),
                Valid.Replace("\"charge\":45", "\"charge\":181"),
                Valid.Replace("\"packsCollected\":1", "\"packsCollected\":0"),
                Valid.Replace("\"id\":\"a\",\"available\":false", "\"id\":\"\",\"available\":false"),
                Valid.Replace("\"candles\":[{\"id\":\"c\",\"lit\":true}]", "\"candles\":[{\"id\":\"c\",\"lit\":true},{\"id\":\"c\",\"lit\":false}]")
            }) Assert.Throws<ArgumentException>(() => Call(Lighting(invalid), "Validate"));
        }
        [Test]
        public void MissingLightingMarkerOnSubstantiveDepletedSaveIsCorruptionInsteadOfMigration()
        {
            var type=RequireType("CorridorCheckpoint");
            var data=JsonUtility.FromJson(File.ReadAllText("Assets/Tests/Fixtures/corridor-checkpoint-state.json"),type);
            Set(data,"lightingVersion",1); Set(data,"lighting",Lighting(Valid.Replace("\"charge\":45","\"charge\":0")));
            Call(data,"Validate");
            var json=JsonUtility.ToJson(data).Replace("\"lightingVersion\":1,","");
            var missing=JsonUtility.FromJson(json,type);
            Assert.That(Get<int>(missing,"lightingVersion"),Is.Zero);
            Assert.Throws<ArgumentException>(()=>Call(missing,"Validate"),"Marker loss silently made a depleted new save legacy");
        }
        [Test]
        public void LegacyCorridorFixtureRemainsValidButNewLightingCannotOmitChargeState()
        {
            var data = JsonUtility.FromJson(File.ReadAllText("Assets/Tests/Fixtures/corridor-checkpoint-state.json"), RequireType("CorridorCheckpoint"));
            Assert.That(Get<int>(data, "lightingVersion"), Is.Zero); Call(data, "Validate");
            Set(data, "lightingVersion", 1); Set(data, "lighting", null);
            Assert.Throws<ArgumentException>(() => Call(data, "Validate"));
            Set(data, "lightingVersion", 99);
            Assert.Throws<ArgumentException>(() => Call(data, "Validate"));
        }
    }
}
