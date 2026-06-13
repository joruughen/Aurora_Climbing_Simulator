using UnityEditor;
using UnityEngine;

namespace Oculus.Interaction.Locomotion
{
    public static class ClimbPendulumSetup
    {
        [MenuItem("Tools/Aurora/Setup Climb Pendulum Audio")]
        public static void SetupAudio()
        {
            var pendulum = Object.FindFirstObjectByType<ClimbPendulumController>();
            if (pendulum == null) { Debug.LogError("ClimbPendulumController not found in scene."); return; }

            var so = new SerializedObject(pendulum);

            AssignClips(so, "releaseSounds",
                "Assets/ShowcaseSamples/Locomotion/Sound/Interaction_Locomotion_Climbing_Holds_Release_01.wav",
                "Assets/ShowcaseSamples/Locomotion/Sound/Interaction_Locomotion_Climbing_Holds_Release_02.wav",
                "Assets/ShowcaseSamples/Locomotion/Sound/Interaction_Locomotion_Climbing_Holds_Release_03.wav",
                "Assets/ShowcaseSamples/Locomotion/Sound/Interaction_Locomotion_Climbing_Holds_Release_04.wav",
                "Assets/ShowcaseSamples/Locomotion/Sound/Interaction_Locomotion_Climbing_Holds_Release_05.wav");

            AssignClips(so, "landingSounds",
                "Assets/ShowcaseSamples/Locomotion/Sound/Interaction_Locomotion_WalkingStick_Floor_Thud_1.wav",
                "Assets/ShowcaseSamples/Locomotion/Sound/Interaction_Locomotion_WalkingStick_Floor_Thud_2.wav",
                "Assets/ShowcaseSamples/Locomotion/Sound/Interaction_Locomotion_WalkingStick_Floor_Thud_3.wav",
                "Assets/ShowcaseSamples/Locomotion/Sound/Interaction_Locomotion_WalkingStick_Floor_Thud_4.wav",
                "Assets/ShowcaseSamples/Locomotion/Sound/Interaction_Locomotion_WalkingStick_Floor_Thud_5.wav");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(pendulum);
            Debug.Log("ClimbPendulumController audio clips assigned.");
        }

        private static void AssignClips(SerializedObject so, string fieldName, params string[] paths)
        {
            var prop = so.FindProperty(fieldName);
            prop.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);
                if (clip == null) { Debug.LogWarning($"Clip not found: {paths[i]}"); continue; }
                prop.GetArrayElementAtIndex(i).objectReferenceValue = clip;
            }
        }
    }
}
