using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AstralUI.Editor
{
    /// <summary>
    /// Builds a 5-second looping frame animation from Assets/AstralUI/Animation
    /// (51 ordered frames, ~10.2 fps) and mounts it on the main scene's HomeHero.
    /// </summary>
    public static class AstralFrameAnimationTool
    {
        const string Root = "Assets/AstralUI/";
        const string FramesFolder = Root + "Animation";
        const string ClipPath = Root + "Animation/AstralHeroFrameAnim.anim";
        const string ControllerPath = Root + "Animation/AstralHeroFrameAnim.controller";
        const string StateName = "AstralHeroLoop";
        public const float DurationSeconds = 5f;

        [MenuItem("Tools/Astral UI/Build Hero Frame Animation")]
        public static void Build()
        {
            var frames = LoadOrderedFrames();
            if (frames.Length == 0) throw new InvalidOperationException("No frame sprites found in " + FramesFolder);

            RebuildAssets(frames);
            var mounted = MountOnScene();
            if (mounted) EditorSceneManager.SaveOpenScenes();

            Debug.Log("[AstralUI] Frame animation ready: " + frames.Length + " frames, " + DurationSeconds +
                      "s loop, clip=" + ClipPath + ", controller=" + ControllerPath +
                      (mounted ? ", mounted on HomeHero." : ", scene mount skipped (main scene not open)."));
        }

        /// <summary>Loads frame sprites ordered by name (processed_frame_001 ... 051).</summary>
        public static Sprite[] LoadOrderedFrames()
        {
            return Directory.GetFiles(FramesFolder, "*.png")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n, StringComparer.Ordinal)
                .Select(n => AssetDatabase.LoadAssetAtPath<Sprite>(FramesFolder + "/" + n + ".png"))
                .Where(s => s != null)
                .ToArray();
        }

        /// <summary>Creates/updates the AnimationClip: one sprite key per frame, evenly spread over 5s, looping.</summary>
        static AnimationClip BuildClip(Sprite[] frames)
        {
            var clip = new AnimationClip { frameRate = frames.Length / DurationSeconds };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0f;
            settings.stopTime = DurationSeconds;
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            float step = DurationSeconds / frames.Length;
            // 51 keys at t = i*step (last one at 4.902s); the clip ends at exactly 5s,
            // so the final frame holds for one step and the loop wraps to frame 1.
            var keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i * step, value = frames[i] };

            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(Image), "m_Sprite"), keys);

            AssetDatabase.DeleteAsset(ClipPath);
            AssetDatabase.CreateAsset(clip, ClipPath);
            return clip;
        }

        /// <summary>Creates/updates the AnimatorController with a single looping state.</summary>
        static AnimatorController BuildController(AnimationClip clip)
        {
            if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var layer = controller.layers[0];
            var state = layer.stateMachine.AddState(StateName);
            state.motion = clip;
            layer.stateMachine.defaultState = state;
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>Regenerates clip + controller from the given frames.</summary>
        static void RebuildAssets(Sprite[] frames)
        {
            var clip = BuildClip(frames);
            BuildController(clip);
        }

        static void EnsureAssets(Sprite[] frames)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath) == null) BuildClip(frames);
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null)
                BuildController(AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath));
        }

        /// <summary>Mounts the animation on the open main scene's HomeHero image. Returns false when the scene is not open.</summary>
        static bool MountOnScene()
        {
            var heroGo = GameObject.Find("Astral Lobby/Safe Area/Stage/Home/HomeHero");
            if (heroGo == null) return false;
            Mount(heroGo);
            return true;
        }

        /// <summary>Mounts the frame animation on any Image GameObject (used by the builder too).</summary>
        public static void Mount(GameObject heroGo)
        {
            var image = heroGo.GetComponent<Image>();
            if (image == null) throw new InvalidOperationException("Mount target has no Image: " + heroGo.name);
            var frames = LoadOrderedFrames();
            if (frames.Length == 0) throw new InvalidOperationException("No frame sprites found in " + FramesFolder);
            EnsureAssets(frames);

            // Frame art is transparent-background key art; never stretch it.
            image.preserveAspect = true;
            // Replace the placeholder static art with frame 1 so the pre-play view already shows the animation art.
            image.sprite = frames[0];

            // Match the rect to the frame aspect, keeping height and the top-left anchor,
            // so the hero keeps the same on-screen size as the original key art.
            var rt = image.rectTransform;
            float aspect = frames[0].rect.width / frames[0].rect.height;
            rt.sizeDelta = new Vector2(rt.sizeDelta.y * aspect, rt.sizeDelta.y);

            // Animator drives the sprite swap at runtime.
            var animator = heroGo.GetComponent<Animator>();
            if (animator == null) animator = heroGo.AddComponent<Animator>();
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Component fallback: also carries the ordered frames (used by QA and manual playback).
            var frameAnim = heroGo.GetComponent<AstralFrameAnimation>();
            if (frameAnim == null) frameAnim = heroGo.AddComponent<AstralFrameAnimation>();
            frameAnim.frames = frames;
            frameAnim.duration = DurationSeconds;
            frameAnim.loop = true;
            frameAnim.playOnEnable = false; // Animator is the primary driver.
            frameAnim.enabled = true;
        }
    }
}
