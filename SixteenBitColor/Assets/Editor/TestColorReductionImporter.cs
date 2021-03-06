﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

public class TestColorReductionImporter : AssetPostprocessor
{
	const string PathPattern4444 = "/CompressTo4444/";
	const string PathPattern5650 = "/CompressTo5650/";
	const string PathPattern4444D = "/CompressTo4444Dither/";
	const string PathPattern5650D = "/CompressTo5650Dither/";
	const string PathPattern4444Unity = "/CompressTo4444Unity/";

	void OnPreprocessTexture()
	{
		var importer = this.assetImporter as TextureImporter;
		// このプロジェクトでは比較のために全テクスチャをポイントサンプリング
		importer.filterMode = FilterMode.Point; // 比較しやすくするためにポイント

		var path = this.assetPath;
		if (path.Contains(PathPattern4444)
			|| path.Contains(PathPattern5650)
			|| path.Contains(PathPattern4444D)
			|| path.Contains(PathPattern5650D))
		{
			importer.alphaIsTransparency = false; // 勝手にいじられるのを避ける
			importer.isReadable = true; // 読めないと何もできない
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.mipmapEnabled = false; // テスト用につき無効
		}
		else if (path.Contains(PathPattern4444Unity))
		{
			importer.alphaIsTransparency = false; // 勝手にいじるな
			importer.mipmapEnabled = false; // テスト用につき無効
			// SetPlatformTextureSettingsした後に使い回して大丈夫かわからなので全部別インスタンスでやる
			var settings = new TextureImporterPlatformSettings();
			settings.format = TextureImporterFormat.ARGB16;
			settings.compressionQuality = 100;
			settings.overridden = true;
			settings.maxTextureSize = importer.maxTextureSize;
			SetPlatformSettingForIndex(importer, "Standalone", settings);
			SetPlatformSettingForIndex(importer, "Android", settings);
			SetPlatformSettingForIndex(importer, "iPhone", settings);
			SetPlatformSettingForIndex(importer, "WebGL", settings);
		}
	}

	void SetPlatformSettingForIndex(TextureImporter importer, string name, TextureImporterPlatformSettings original)
	{
		var settings = new TextureImporterPlatformSettings();
		original.CopyTo(settings);
		settings.name = name;
		importer.SetPlatformTextureSettings(settings);
	}

	void OnPostprocessTexture(Texture2D texture)
	{
		var path = this.assetPath;
		if (path.Contains(PathPattern4444))
		{
			CompressTo4444(texture, path, dither: false);
		}
		else if (path.Contains(PathPattern5650))
		{
			CompressTo5650(texture, path, dither: false);
		}
		else if (path.Contains(PathPattern4444D))
		{
			CompressTo4444(texture, path, dither: true);
		}
		else if (path.Contains(PathPattern5650D))
		{
			CompressTo5650(texture, path, dither: true);
		}
	}

	void CompressTo4444(Texture2D texture, string path, bool dither)
	{
		var pixels = texture.GetPixels32();
		var width = texture.width;
		var height = texture.height;
		string postfix;
		if (dither)
		{
			ColorReductionUtil.FloydSteinberg(pixels, ColorReductionUtil.To4444, width, height);
		}
		else
		{
			for (int i = 0; i < pixels.Length; i++)
			{
				pixels[i] = ColorReductionUtil.To4444(pixels[i]);
			}
		}
		texture.SetPixels32(pixels);
		EditorUtility.CompressTexture(texture, TextureFormat.ARGB4444, quality: 100);
	}

	void CompressTo5650(Texture2D texture, string path, bool dither)
	{
		var pixels = texture.GetPixels32();
		var width = texture.width;
		var height = texture.height;
		string postfix;
		if (dither)
		{
			ColorReductionUtil.FloydSteinberg(pixels, ColorReductionUtil.To5650, width, height);
		}
		else
		{
			for (int i = 0; i < pixels.Length; i++)
			{
				pixels[i] = ColorReductionUtil.To5650(pixels[i]);
			}
		}
		texture.SetPixels32(pixels);
		EditorUtility.CompressTexture(texture, TextureFormat.RGB565, quality: 100);
	}
}
