using System;
using UnityEngine;

namespace RayTracer
{
    [RequireComponent(typeof(MeshFilter))]

    public class RayTracedMesh : MonoBehaviour
    {
        public RayTracedMaterial material;
        [SerializeField] private int triangleCount;

        private MeshFilter _mf;

        // TODO: Add support for submeshes. At the minute, only 1 mesh per object is supported unless each submesh gets it's own RayTracedMesh
        public (RayTracingEffect.MeshInfo, Mesh) ToMeshInfo(int totalTriangleCount)
        {
            if (_mf == null)
                _mf = GetComponent<MeshFilter>();

            triangleCount = _mf.sharedMesh.triangles.Length / 3;

            RayTracingEffect.MeshInfo info = new();
            info.material = material.ToRaytracingMaterial();
            info.numTriangles = triangleCount;
            info.boundsMin = _mf.sharedMesh.bounds.min;
            info.boundsMax = _mf.sharedMesh.bounds.max;
            info.firstTriangleIndex = totalTriangleCount;

            return (info, _mf.sharedMesh);
        }
    }
}