using UnityEditor;
using UnityEngine;

namespace RayTracer
{
    [CustomEditor(typeof(Raytracer))]

    public class RayTracingEffectEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            Raytracer t = (Raytracer)target;
            
            if (GUILayout.Button($"Save Screenshot ({t.CurrentRenderedFrames} rendered frames)"))
            {
                t.SaveScreenshot(t.screenshotSize);
            }
        }
    }
}
