using UnityEngine;

namespace E7
{
    /// <summary>
    /// Rendu final : la caméra du monde et celle des armes dessinent dans une même image,
    /// puis cet effet applique le look « caméra piéton » (grand angle, grain, halo, vignette, sang, flash…).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BodycamFX : MonoBehaviour
    {
        public Camera world, weapons;
        RenderTexture sceneRT;
        Material mat;
        int lastW, lastH, lastAA;

        public static BodycamFX Create(Transform parent)
        {
            var go = new GameObject("CameraFinale");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Camera>();
            c.cullingMask = 0; c.clearFlags = CameraClearFlags.Nothing; c.depth = 50;
            c.allowHDR = false; c.allowMSAA = false;
            return go.AddComponent<BodycamFX>();
        }

        void Awake()
        {
            var sh = Shader.Find("Hidden/E7/Bodycam");
            if (sh) mat = new Material(sh);
            else Debug.LogWarning("[E7] Shader Hidden/E7/Bodycam introuvable : image sans effet");
        }

        void EnsureRT()
        {
            int aa = Save.Data.settings.quality == "haut" ? 4 : Save.Data.settings.quality == "moyen" ? 2 : 1;
            if (sceneRT != null && lastW == Screen.width && lastH == Screen.height && lastAA == aa) return;
            if (sceneRT != null) { if (world) world.targetTexture = null; if (weapons) weapons.targetTexture = null; sceneRT.Release(); Destroy(sceneRT); }
            lastW = Screen.width; lastH = Screen.height; lastAA = aa;
            sceneRT = new RenderTexture(Mathf.Max(64, lastW), Mathf.Max(64, lastH), 24, RenderTextureFormat.DefaultHDR) { antiAliasing = aa, name = "E7_Scene" };
            sceneRT.Create();
        }

        void LateUpdate()
        {
            if (!world) return;
            EnsureRT();
            world.targetTexture = sceneRT;
            if (weapons) weapons.targetTexture = sceneRT;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (sceneRT == null) { Graphics.Blit(src, dst); return; }
            if (!mat) { Graphics.Blit(sceneRT, dst); return; }
            var s = Save.Data.settings;
            var p = Player.I;
            float fx = s.camFx == "off" ? 0 : s.camFx == "leger" ? 0.5f : 1f;
            mat.SetFloat("_K", 0.2f * fx);
            mat.SetFloat("_CA", 0.006f * fx);
            mat.SetFloat("_Grain", 0.085f * (0.35f + 0.65f * fx));
            mat.SetFloat("_Vig", 0.4f + 0.6f * fx);
            mat.SetFloat("_Hurt", p ? p.HurtFx : 0);
            mat.SetFloat("_Blind", p ? p.BlindFx : 0);
            mat.SetFloat("_Dead", p && !p.Alive && Game.I.State == GameState.Dead ? Mathf.Min(1, p.DeathT / 1.1f) * 0.8f : 0);
            mat.SetFloat("_Time2", Time.time);
            mat.SetFloat("_Aspect", Screen.width / (float)Mathf.Max(1, Screen.height));
            mat.SetFloat("_Exposure", s.bright * 1.05f);
            mat.SetFloat("_Threshold", 1.1f);
            mat.SetFloat("_BloomAmt", s.quality == "bas" ? 0f : 0.9f);
            // halo lumineux : on extrait les zones très claires puis on floute en réduisant
            RenderTexture bloom = null;
            if (s.quality != "bas")
            {
                int w = sceneRT.width / 2, h = sceneRT.height / 2;
                var a = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.DefaultHDR);
                Graphics.Blit(sceneRT, a, mat, 0);
                var b = RenderTexture.GetTemporary(w / 2, h / 2, 0, RenderTextureFormat.DefaultHDR);
                Graphics.Blit(a, b, mat, 1);
                var c = RenderTexture.GetTemporary(w / 4, h / 4, 0, RenderTextureFormat.DefaultHDR);
                Graphics.Blit(b, c, mat, 1);
                Graphics.Blit(c, b, mat, 1);
                RenderTexture.ReleaseTemporary(a); RenderTexture.ReleaseTemporary(c);
                bloom = b;
            }
            mat.SetTexture("_Bloom", bloom ? (Texture)bloom : Texture2D.blackTexture);
            Graphics.Blit(sceneRT, dst, mat, 2);
            if (bloom) RenderTexture.ReleaseTemporary(bloom);
        }

        void OnDestroy() { if (sceneRT) sceneRT.Release(); }
    }
}
