using UnityEditor;
using UnityEngine;

namespace RayTracer
{
    [CustomEditor(typeof(RayTracingEffect))]

    public class RayTracingEffectEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            RayTracingEffect t = (RayTracingEffect)target;
            if (GUILayout.Button($"Save Screenshot ({t.CurrentRenderedFrames} rendered frames)"))
            {
                t.SaveScreenshot(t.screenshotSize);
            }
        }
    }
}
