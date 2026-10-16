using UnityEngine;

namespace MmaManagerTycoon
{
    [ExecuteAlways, RequireComponent(typeof(SpriteRenderer))]
    public sealed class FighterAppearance : MonoBehaviour
    {
        public const int ToneCount = 5;
        public static readonly string[] ToneNames = { "Light", "Light-medium", "Medium", "Medium-deep", "Deep" };
        // Reserved shades in the approved fighter PNG palette, dark to light.
        static readonly string[] Source = { "3b2523", "59372d", "784938", "965e43", "b67c56", "cf986b", "e3b48a", "f0cfaa" };
        static readonly string[][] Tones = {
            new[]{"604039","835b4d","a67b65","c5977c","ddb393","edcbb0","f6deca","fff0df"},
            new[]{"4c3029","6c4636","916245","b2815e","cd9e77","e2b68e","efcba7","f8dfbf"},
            Source,
            new[]{"241b19","392820","51372b","6b4935","875e43","a67954","bc916b","d4b08a"},
            new[]{"171315","241b19","34251f","493227","624632","7b5b40","967752","b49770"}
        };
        [SerializeField, Range(0,ToneCount-1)] int skinTone = 2;
        [SerializeField] Texture2D[] sourceTextures = new Texture2D[0];
        [SerializeField] Texture2D[] skinMasks = new Texture2D[0];
        Texture lastTexture;
        SpriteRenderer target;
        MaterialPropertyBlock properties;
        readonly Vector4[] sourceColors = new Vector4[8];
        readonly Vector4[] targetColors = new Vector4[8];
        public int SkinTone => skinTone;
        public string SkinToneName => ToneNames[skinTone];
        public static Color Swatch(int index) => Parse(Tones[Mathf.Clamp(index,0,ToneCount-1)][4]);
        static Color Parse(string hex) { ColorUtility.TryParseHtmlString("#"+hex,out var color);return color; }
        static Vector4 ShaderColor(string hex)
        {
            Color c=Parse(hex);
            if(QualitySettings.activeColorSpace==ColorSpace.Linear)c=c.linear;
            return new Vector4(c.r,c.g,c.b,1);
        }
        void OnEnable() { Apply(); }
        void OnValidate() { skinTone=Mathf.Clamp(skinTone,0,ToneCount-1);Apply(); }
        public void SetSkinTone(int value) { skinTone=Mathf.Clamp(value,0,ToneCount-1);Apply(); }
        public void ConfigureMasks(Texture2D[] sources, Texture2D[] masks)
        {
            if(sources==null || masks==null || sources.Length!=masks.Length)throw new System.ArgumentException("Source textures and masks must match.");
            sourceTextures=sources;skinMasks=masks;Apply();
        }
        void LateUpdate()
        {
            if(target && target.sprite && target.sprite.texture!=lastTexture)Apply();
        }
        void Apply()
        {
            if(!target)target=GetComponent<SpriteRenderer>();
            if(!target)return;
            if(properties==null)properties=new MaterialPropertyBlock();
            target.GetPropertyBlock(properties);
            for(int i=0;i<8;i++){sourceColors[i]=ShaderColor(Source[i]);targetColors[i]=ShaderColor(Tones[skinTone][i]);}
            properties.SetVectorArray("_SkinSource",sourceColors);
            properties.SetVectorArray("_SkinTarget",targetColors);
            lastTexture=target.sprite?target.sprite.texture:null;
            Texture2D mask=null;
            for(int i=0;i<sourceTextures.Length && i<skinMasks.Length;i++)
                if(sourceTextures[i]==lastTexture){mask=skinMasks[i];break;}
            properties.SetTexture("_SkinMask",mask?mask:Texture2D.blackTexture);
            // Preserve the approved original exactly; recolor other tones only
            // where the matching animation mask identifies skin geometry.
            properties.SetFloat("_SkinEnabled",mask && skinTone!=2?1:0);
            target.SetPropertyBlock(properties);
        }
    }
}
