using UnityEngine;

namespace RayTracer
{
    public class RayTracedMaterial : MonoBehaviour
    {
        public Color colour;
        public Color specularColour;
        public Color emissionColour;
        public float emissionStrength;
        [Range(0, 1)] public float smoothness;
        [Range(0, 1)] public float specularProbability;

        private Material _mat;

        public Raytracer.RayTracingMaterial ToRaytracingMaterial()
        {
            Raytracer.RayTracingMaterial material = new();
            material.colour = new Vector3(colour.r, colour.g, colour.b);
            material.specularColour = new Vector3(specularColour.r, specularColour.g, specularColour.b);
            material.emissionColour = new Vector3(emissionColour.r, emissionColour.g, emissionColour.b);
            material.emissionStrength = emissionStrength;
            material.smoothness = smoothness;
            material.specularProbability = specularProbability;
            return material;
        }

        public void OnValidate()
        {
            if (TryGetComponent(out MeshRenderer mr))
            {
                if (_mat == null)
                    _mat = new Material(mr.sharedMaterial);
                _mat.color = colour;
                mr.material = _mat;
            }
        }
    }
}