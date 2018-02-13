using UnityEngine;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    public class StandardSpecularToHDLitMaterialUpgrader : MaterialUpgrader
    {
        public StandardSpecularToHDLitMaterialUpgrader() : this("Standard (Specular setup)", "HDRenderPipeline/Lit", LitGUI.SetupMaterialKeywordsAndPass) {}

        public StandardSpecularToHDLitMaterialUpgrader(string sourceShaderName, string destShaderName, MaterialFinalizer finalizer)
        {
            RenameShader(sourceShaderName, destShaderName, finalizer);

            RenameTexture("_MainTex", "_BaseColorMap");
            RenameColor("_Color", "_BaseColor");
            RenameFloat("_Glossiness", "_Smoothness");
            RenameTexture("_BumpMap", "_NormalMap");
            RenameFloat("_BumpScale", "_NormalScale");
            RenameColor("_EmissionColor", "_EmissiveColor");
            RenameTexture("_EmissionMap", "_EmissiveColorMap");
            RenameFloat("_DetailNormalMapScale", "_DetailNormalScale");
            RenameFloat("_Cutoff", "_AlphaCutoff");
            RenameKeywordToFloat("_ALPHATEST_ON", "_AlphaCutoffEnable", 1f, 0f);

            SetFloat("_MaterialID", 4f);

            // the HD renderloop packs detail albedo and detail normals into a single texture.
            // mapping the detail normal map, if any, to the detail map, should do the right thing if
            // there is no detail albedo.

            // Anything reasonable that can be done here?
            RenameColor("_SpecColor", "_SpecularColor");
            RenameTexture("_SpecGlossMap", "_SpecularColorMap");

            //@TODO: Seb. Why do we multiply color by intensity
            //       in shader when we can just store a color?
            // builtinData.emissiveColor * builtinData.emissiveIntensity
        }

        public override void Convert(Material srcMaterial, Material dstMaterial)
        {
            dstMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset;

            base.Convert(srcMaterial, dstMaterial);
            //@TODO: Find a good way of setting up keywords etc from properties.
            // Code should be shared with material UI code.

            if ( srcMaterial.GetTexture("_MetallicGlossMap") != null ||
            srcMaterial.GetTexture("_OcclusionMap") != null ||
            srcMaterial.GetTexture("_DetailMask") != null)
            {
                Texture2D maskMap;
                TextureCombiner maskMapCombiner = new TextureCombiner(
                    TextureCombiner.GetTextureSafe(srcMaterial, "_MetallicGlossMap", 1), 4,
                    TextureCombiner.GetTextureSafe(srcMaterial, "_OcclusionMap", 0), 4,
                    TextureCombiner.GetTextureSafe(srcMaterial, "_DetailMask", 0), 4,
                    TextureCombiner.GetTextureSafe(srcMaterial, (srcMaterial.GetFloat("_SmoothnessTextureChannel") == 0)?"_SpecGlossMap": "_MainTex", 2), 3
                );
                string maskMapPath = AssetDatabase.GetAssetPath(srcMaterial);
                maskMapPath = maskMapPath.Remove(maskMapPath.Length-4) + "_MaskMap.png";
                maskMap = maskMapCombiner.Combine( maskMapPath );
                dstMaterial.SetTexture("_MaskMap", maskMap);
            }

            if ( srcMaterial.GetTexture("_DetailAlbedoMap") != null ||
                srcMaterial.GetTexture("_DetailNormalMap") != null
            )
            {
                Texture2D detailMap;
                TextureCombiner detailCombiner = new TextureCombiner(
                    TextureCombiner.GetTextureSafe(srcMaterial, "_DetailAlbedoMap", 2), 4,
                    TextureCombiner.GetTextureSafe(srcMaterial, "_DetailNormalMap", 2), 1,
                    TextureCombiner.midGrey, 1,
                    TextureCombiner.GetTextureSafe(srcMaterial, "_DetailNormalMap", 2), 0
                );
                string detailMapPath = AssetDatabase.GetAssetPath(srcMaterial);
                detailMapPath = detailMapPath.Remove(detailMapPath.Length-4) + "_DetailMap.png";
                detailMap = detailCombiner.Combine( detailMapPath );
                Debug.Log("Coucou");
                dstMaterial.SetTexture("_DetailMap", detailMap);
            }


            // Blend Mode
            int previousBlendMode = srcMaterial.GetInt("_Mode");
            switch (previousBlendMode)
            {
                case 0: // Opaque
                    dstMaterial.SetFloat("_SurfaceType", 0);
                    dstMaterial.SetFloat("_BlendMode", 0);
                    dstMaterial.SetFloat("_AlphaCutoffEnable", 0);

                    break;
                case 1: // Cutout
                    dstMaterial.SetFloat("_SurfaceType", 0);
                    dstMaterial.SetFloat("_BlendMode", 0);
                    dstMaterial.SetFloat("_AlphaCutoffEnable", 1);
                    break;
                case 2: // Fade -> Alpha
                    dstMaterial.SetFloat("_SurfaceType", 1);
                    dstMaterial.SetFloat("_BlendMode", 0);
                    dstMaterial.SetFloat("_AlphaCutoffEnable", 0);
                    break;
                case 3: // Transparent -> alpha pre-multiply
                    dstMaterial.SetFloat("_SurfaceType", 1);
                    dstMaterial.SetFloat("_BlendMode", 4);
                    dstMaterial.SetFloat("_AlphaCutoffEnable", 0);
                    break;
            }

            HDEditorUtils.ResetMaterialKeywords(dstMaterial);
        }
    }
}
