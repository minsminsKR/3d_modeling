using System;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class GraphicsAuditEncodingTests
    {
        [Test]
        public void NativeHdrCaptureEncodesLinearGrayToSrgbAndClampsOnlyTheSdrExport()
        {
            var audit=RequireType("GraphicsPresentationAudit");
            var pixels=(Color32[])Call(audit,"EncodeToneMappedLinearToSrgb",new[]{new Color(.18f,.18f,.18f,1),new Color(-.1f,2,0,1)});
            Assert.That(pixels[0].r,Is.InRange(117,119),"18% linear gray must become ~118 sRGB, not 46 linear bytes");
            Assert.That(pixels[0].g,Is.EqualTo(pixels[0].r));Assert.That(pixels[0].b,Is.EqualTo(pixels[0].r));
            Assert.That(pixels[1].r,Is.Zero);Assert.That(pixels[1].g,Is.EqualTo(255));Assert.That(pixels[1].b,Is.Zero);
        }
        [Test]
        public void NativeHdrCaptureRejectsNonfinitePixelsInsteadOfSavingFalseEvidence()
        {
            Assert.Throws<InvalidOperationException>(()=>Call(RequireType("GraphicsPresentationAudit"),"EncodeToneMappedLinearToSrgb",new[]{new Color(float.NaN,0,0,1)}));
        }
    }
}
