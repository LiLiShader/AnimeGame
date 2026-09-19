using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace AstralUI.Editor
{
    /// <summary>One-shot repair for the lobby VideoPlayer: rebind the clip, switch to a
    /// RenderTexture + RawImage pipeline (required under a Canvas), and drop the stray
    /// duplicate component on the scene root.</summary>
    public static class AstralVideoFixer
    {
        const string VideoPath = "Assets/AstralUI/Animation/jimeng-2026-09-19-5831-使用同一张图片作为首帧和尾帧，生成一个可以无缝循环播放的短视频。 保持原图的人物....mp4";
        const string RtDir = "Assets/AstralUI/Art";
        const string RtPath = RtDir + "/AstralHeroVideo.renderTexture";

        public static void Fix()
        {
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoPath);
            if (clip == null) throw new InvalidOperationException("VideoClip not found: " + VideoPath);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid()) throw new InvalidOperationException("No active scene.");
            var allPlayers = UnityEngine.Object.FindObjectsOfType<VideoPlayer>(true);
            if (allPlayers.Length == 0) throw new InvalidOperationException("No VideoPlayer in scene.");

            // Keep the player on the "video" node (UI Canvas child); destroy the stray one on the scene root.
            VideoPlayer target = null;
            foreach (var p in allPlayers)
            {
                if (p.transform.name == "video") { target = p; break; }
            }
            if (target == null) target = allPlayers[0];
            foreach (var p in allPlayers)
            {
                if (!ReferenceEquals(p, target)) UnityEngine.Object.DestroyImmediate(p);
            }

            // RenderTexture sized to the clip (clip.width/height are available once imported).
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RtPath);
            if (rt == null)
            {
                if (!Directory.Exists(RtDir)) Directory.CreateDirectory(RtDir);
                rt = new RenderTexture(Mathf.Max(2, (int)clip.width), Mathf.Max(2, (int)clip.height), 0);
                rt.name = "AstralHeroVideo";
                AssetDatabase.CreateAsset(rt, RtPath);
            }
            else if (rt.width != (int)clip.width || rt.height != (int)clip.height)
            {
                rt.Release();
                rt.width = (int)clip.width;
                rt.height = (int)clip.height;
                rt.Create();
                EditorUtility.SetDirty(rt);
            }

            target.clip = clip;
            target.renderMode = VideoRenderMode.RenderTexture;
            target.targetTexture = rt;
            target.playOnAwake = true;
            target.isLooping = true;
            target.waitForFirstFrame = true;

            // The node sits under a Canvas: it needs a RawImage to display the RT.
            var raw = target.GetComponent<RawImage>();
            if (raw == null) raw = target.gameObject.AddComponent<RawImage>();
            raw.texture = rt;
            raw.raycastTarget = false;
            raw.color = Color.white;

            EditorUtility.SetDirty(target);
            EditorSceneManager_SaveOpenScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[AstralVideoFixer] Fixed VideoPlayer on '" + target.transform.name +
                      "': clip=" + clip.name + ", RT=" + rt.width + "x" + rt.height +
                      ", mode=RenderTexture, RawImage added, stray players removed=" + (allPlayers.Length - 1));
        }

        static void EditorSceneManager_SaveOpenScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
    }
}
