using UnityEditor;
using UnityEngine;

namespace RayTracer
{
     // TODO: Add more meshes to hierarchy menu

    public class RayTracedMeshHierarchyMenu : Editor
    {
        [MenuItem("GameObject/Ray Traced/Sphere", false, 10)]
        static void CreateCustomMeshSphere(MenuCommand menuCommand)
        {
            GameObject raytracedMeshSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            raytracedMeshSphere.name = "Ray Traced Sphere";
            raytracedMeshSphere.tag = "Sphere";
            var raytracedMat = raytracedMeshSphere.AddComponent<RayTracedMaterial>();
            bool reflective = Random.Range(0, 1) > 0.5f;
            raytracedMat.colour = reflective ? Color.white : Random.ColorHSV();
            raytracedMat.specularColour = Color.white;
            raytracedMat.smoothness = reflective ? 0.75f : 0;
            raytracedMat.specularProbability = reflective ? 0.5f : 0;
            raytracedMat.OnValidate();
            GameObjectUtility.SetParentAndAlign(raytracedMeshSphere, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(raytracedMeshSphere, "Create " + raytracedMeshSphere.name);
            Selection.activeObject = raytracedMeshSphere;
        }
    }
}
