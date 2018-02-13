﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class TextureCombiner
{
	public static Texture2D _midGrey;
	public static Texture2D midGrey
	{
		get
		{
			if (_midGrey == null)
				_midGrey = 	TextureFromColor(Color.grey);

			return _midGrey;
		}
	}

	private static Dictionary<Color, Texture2D> singleColorTextures = new Dictionary<Color, Texture2D>();

	public static Texture2D TextureFromColor(Color color)
	{
		if (color == Color.white) return Texture2D.whiteTexture;
		if (color == Color.black) return Texture2D.blackTexture;

		bool makeTexture = !singleColorTextures.ContainsKey(color);
		if (!makeTexture)
			makeTexture = (singleColorTextures[color] == null);

		if (makeTexture)
		{
			Texture2D tex = new Texture2D(4, 4, TextureFormat.ARGB32, false, true);
			tex.SetPixels(new Color[] {
				color, color, color, color,
				color, color, color, color,
				color, color, color, color,
				color, color, color, color
			});
			tex.Apply();

			singleColorTextures[color] = tex;
		}
		
		return singleColorTextures[color];
	}

	public static Texture GetTextureSafe( Material srcMaterial, string propertyName, int fallback)
	{
		switch(fallback)
		{
			case 0: return GetTextureSafe( srcMaterial, propertyName, Texture2D.whiteTexture );
			case 1: return GetTextureSafe( srcMaterial, propertyName, Texture2D.blackTexture );
			case 2: return GetTextureSafe( srcMaterial, propertyName, TextureCombiner.midGrey );
		}

		return null;
	}

	public static Texture GetTextureSafe( Material srcMaterial, string propertyName, Color fallback)
	{
		return GetTextureSafe( srcMaterial, propertyName, TextureFromColor(fallback) );
	}

	public static Texture GetTextureSafe( Material srcMaterial, string propertyName, Texture fallback)
	{
		if (!srcMaterial.HasProperty(propertyName))
			return fallback;

		Texture tex = srcMaterial.GetTexture(propertyName);
		if (tex == null)
			return fallback;
		else
			return tex;
	}

	public static TextureFormat[] TextureFormatsWithouthAlpha = new TextureFormat[]{
	 TextureFormat.ASTC_RGB_10x10 ,
	 TextureFormat.ASTC_RGB_12x12 ,
	 TextureFormat.ASTC_RGB_4x4 ,
	 TextureFormat.ASTC_RGB_5x5 ,
	 TextureFormat.ASTC_RGB_6x6 ,
	 TextureFormat.ASTC_RGB_8x8 ,
	 TextureFormat.BC4 ,
	 TextureFormat.BC5 ,
	 TextureFormat.DXT1 ,
	 TextureFormat.DXT1Crunched ,
	 TextureFormat.EAC_R ,
	 TextureFormat.EAC_R_SIGNED ,
	 TextureFormat.EAC_RG ,
	 TextureFormat.EAC_RG_SIGNED ,
	 TextureFormat.ETC2_RGB ,
	 TextureFormat.ETC_RGB4 ,
	 TextureFormat.ETC_RGB4_3DS ,
	 TextureFormat.ETC_RGB4Crunched ,
	 TextureFormat.PVRTC_RGB2 ,
	 TextureFormat.PVRTC_RGB4 ,
	 TextureFormat.R16 ,
	 TextureFormat.R8 ,
	 TextureFormat.RFloat ,
	 TextureFormat.RG16 ,
	 TextureFormat.RGB24 ,
	 TextureFormat.RGB565 ,
	 TextureFormat.RGB9e5Float ,
	 TextureFormat.RGFloat ,
	 TextureFormat.RGHalf ,
	 TextureFormat.RHalf ,
	 TextureFormat.YUY2
	 };

	public static bool TextureHasAlpha ( Texture2D tex )
	{
		if (tex == null) return false;

		bool o = true;
		int i=0;

		while ( i < TextureFormatsWithouthAlpha.Length && o)
		{
			o = tex.format != TextureFormatsWithouthAlpha[i];
			++i;
		}

		return o;
	}

	private Texture m_rSource;
	private Texture m_gSource;
	private Texture m_bSource;
	private Texture m_aSource;

	// Chanels are : r=0, g=1, b=2, a=3, greyscale from rgb = 4
	private int m_rChanel;
	private int m_gChanel;
	private int m_bChanel;
	private int m_aChanel;

	private bool m_bilinearFilter;

	private Dictionary<Texture, Texture> m_RawTextures;

	public TextureCombiner( Texture rSource, int rChanel, Texture gSource, int gChanel, Texture bSource, int bChanel, Texture aSource, int aChanel, bool bilinearFilter = true )
	{
		m_rSource = rSource;
		m_gSource = gSource;
		m_bSource = bSource;
		m_aSource = aSource;
		m_rChanel = rChanel;
		m_gChanel = gChanel;
		m_bChanel = bChanel;
		m_aChanel = aChanel;
		m_bilinearFilter = bilinearFilter;
	}

	public Texture2D Combine( string savePath )
	{
		int xMin = int.MaxValue;
		int yMin = int.MaxValue;

		if (m_rSource.width > 4 && m_rSource.width < xMin) xMin = m_rSource.width;
		if (m_gSource.width > 4 && m_gSource.width < xMin) xMin = m_gSource.width;
		if (m_bSource.width > 4 && m_bSource.width < xMin) xMin = m_bSource.width;
		if (m_aSource.width > 4 && m_aSource.width < xMin) xMin = m_aSource.width;
		if (xMin == int.MaxValue) xMin = 4;
		
		if (m_rSource.height > 4 && m_rSource.height < yMin) yMin = m_rSource.height;
		if (m_gSource.height > 4 && m_gSource.height < yMin) yMin = m_gSource.height;
		if (m_bSource.height > 4 && m_bSource.height < yMin) yMin = m_bSource.height;
		if (m_aSource.height > 4 && m_aSource.height < yMin) yMin = m_aSource.height;
		if (yMin == int.MaxValue) yMin = 4;

		Texture2D combined = new Texture2D(xMin, yMin, TextureFormat.RGBAFloat, true, true);
		combined.hideFlags = HideFlags.DontUnloadUnusedAsset;

		Material combinerMaterial = new Material(Shader.Find("Hidden/SRP_Core/TextureCombiner"));
		combinerMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset;

		combinerMaterial.SetTexture("_RSource", GetRawTexture(m_rSource));
		combinerMaterial.SetTexture("_GSource", GetRawTexture(m_gSource));
		combinerMaterial.SetTexture("_BSource", GetRawTexture(m_bSource));
		combinerMaterial.SetTexture("_ASource", GetRawTexture(m_aSource));

		combinerMaterial.SetFloat("_RChannel", m_rChanel);
		combinerMaterial.SetFloat("_GChannel", m_gChanel);
		combinerMaterial.SetFloat("_BChannel", m_bChanel);
		combinerMaterial.SetFloat("_AChannel", m_aChanel);

		RenderTexture combinedRT =  new RenderTexture(xMin, yMin, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);

		Graphics.Blit(Texture2D.whiteTexture, combinedRT, combinerMaterial);

		// Readback the render texture
		RenderTexture previousActive = RenderTexture.active;
		RenderTexture.active = combinedRT;
		combined.ReadPixels(new Rect(0, 0, xMin, yMin), 0, 0, false);
		combined.Apply();
		RenderTexture.active = previousActive;

		byte[] bytes = new byte[0];

		if (savePath.EndsWith("png"))
			bytes = ImageConversion.EncodeToPNG(combined);
		if (savePath.EndsWith("exr"))
			bytes = ImageConversion.EncodeToEXR(combined);
		if (savePath.EndsWith("jpg"))
			bytes = ImageConversion.EncodeToJPG(combined);

		string systemPath = Path.Combine(Application.dataPath.Remove(Application.dataPath.Length-6), savePath);
		File.WriteAllBytes(systemPath, bytes);

		Object.DestroyImmediate(combined);

		AssetDatabase.ImportAsset(savePath);

		TextureImporter combinedImporter = (TextureImporter) AssetImporter.GetAtPath(savePath);
		combinedImporter.sRGBTexture = false;
		combinedImporter.SaveAndReimport();

		if (savePath.EndsWith("exr"))
		{
			// The options for the platform string are: "Standalone", "iPhone", "Android", "WebGL", "Windows Store Apps", "PSP2", "PS4", "XboxOne", "Nintendo 3DS", "WiiU", "tvOS".
			combinedImporter.SetPlatformTextureSettings(new TextureImporterPlatformSettings(){name = "Standalone", format = TextureImporterFormat.DXT5, overridden = true });
		}

		combined = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);

		//cleanup "raw" textures
		foreach( KeyValuePair<Texture, Texture> prop in m_RawTextures )
		{
			if (AssetDatabase.Contains(prop.Value))
				AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(prop.Value));
		}
		Object.DestroyImmediate(combinerMaterial);

		m_RawTextures.Clear();

		return combined;
	}

	private Texture GetRawTexture (Texture original, bool sRGB = false)
	{
		if (m_RawTextures == null) m_RawTextures = new Dictionary<Texture, Texture>();
		if (!m_RawTextures.ContainsKey(original))
		{
			if ( AssetDatabase.Contains(original))
			{
				string path = AssetDatabase.GetAssetPath(original);
				string rawPath = "Assets/raw_"+Path.GetFileName(path);

				AssetDatabase.CopyAsset(path, rawPath);

				AssetDatabase.ImportAsset(rawPath);

				Debug.Log("Import raw texture: "+rawPath);

				TextureImporter rawImporter = (TextureImporter) TextureImporter.GetAtPath(rawPath);
				rawImporter.textureType = TextureImporterType.Default;
				rawImporter.mipmapEnabled = false;
				rawImporter.isReadable = true;
				rawImporter.filterMode = m_bilinearFilter? FilterMode.Bilinear : FilterMode.Point;
				rawImporter.npotScale = TextureImporterNPOTScale.None;
				rawImporter.wrapMode = TextureWrapMode.Clamp;
				rawImporter.sRGBTexture = sRGB;
				rawImporter.maxTextureSize = 8192;

				rawImporter.textureCompression = TextureImporterCompression.Uncompressed;
				
				rawImporter.SaveAndReimport();

				m_RawTextures.Add(original, AssetDatabase.LoadAssetAtPath<Texture>(rawPath));
			}
			else
				m_RawTextures.Add(original, original);
		}

		return m_RawTextures[original];
	}
}
