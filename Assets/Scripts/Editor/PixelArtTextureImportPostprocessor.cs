using UnityEditor;
using UnityEngine;

class PixelArtTextureImportPostprocessor : AssetPostprocessor
{
    static readonly string[] managedRoots =
    {
        "Assets/Picture/",
        "Assets/Tilemap/Sources/"
    };

    static readonly string[] targetBuilds =
    {
        "DefaultTexturePlatform",
        "Standalone",
        "WebGL"
    };

    void OnPreprocessTexture()
    {
        if (!IsManagedPixelArtAsset(assetPath))
        {
            return;
        }

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.streamingMipmaps = false;
        importer.anisoLevel = 0;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        importer.compressionQuality = 100;

        for (int i = 0; i < targetBuilds.Length; i++)
        {
            TextureImporterPlatformSettings settings =
                importer.GetPlatformTextureSettings(targetBuilds[i]);
            settings.name = targetBuilds[i];
            settings.overridden = true;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.crunchedCompression = false;
            settings.compressionQuality = 100;
            importer.SetPlatformTextureSettings(settings);
        }
    }

    static bool IsManagedPixelArtAsset(string path)
    {
        for (int i = 0; i < managedRoots.Length; i++)
        {
            if (path.StartsWith(managedRoots[i]))
            {
                return true;
            }
        }

        return false;
    }
}
