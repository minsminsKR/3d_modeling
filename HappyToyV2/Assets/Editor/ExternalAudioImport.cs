using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Assets already have measured gain and edge fades; do not renormalize them during import.
    public sealed class ExternalAudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/External/")) return;
            var importer = (AudioImporter)assetImporter;
            // Preparation already downmixed/leveled the mono WAV; avoid importer normalization.
            importer.forceToMono = false;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.preloadAudioData = true;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }
    }
}
