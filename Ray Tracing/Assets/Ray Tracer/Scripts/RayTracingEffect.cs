using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace RayTracer
{
    [ExecuteAlways]
    [ImageEffectAllowedInSceneView]
    [RequireComponent(typeof(Camera))]
    public class RayTracingEffect : MonoBehaviour
    {
        public enum DenoiserMode{ SingleFrame, MultiFrame, Compute, None }
        
        public ComputeShader rayTracingShader;
        //public ComputeShader denoiserComputeShader;
        [FormerlySerializedAs("singleFrameDenoiser")] [FormerlySerializedAs("addShader")] public Shader denoiser;
        //public Shader multiFrameDenoiser;

        public Light sun;

        public bool rayTracingEnabled = true;
        public bool rayTraceSceneViewCamera = true;
        public bool drawEnvironment = true;
        public bool drawSun = false;
        public bool useBackfaceCulling = true;
        public bool drawShadows = true;

        //public DenoiserMode denoiserMode;

        [Range(0, 16)]
        public int bounces = 12;

        public int numRaysPerPixel = 50;

        public float renderDistance = 1000;

        public float divergeStrength;

        public Color groundColour;
        public Color skyColourHorizon;
        public Color skyColourZenith;

        public float sunFocus = 700;
        public float sunIntensity = 15;

        [Space(5), Header("Auto Capture")]
        public bool autoScreenshot;

        public int samplesBeforeScreenshot = 10000;

        public int screenshotSize = 1;
        
        private Camera _cam;
        private RenderTexture _target;
        private RenderTexture _prevFrame;

        private List<MeshInfo> _allMeshes;
        private List<Triangle> _allTriangles;
        private List<Sphere> _spheres;
        private ComputeBuffer _meshBuffer;
        private ComputeBuffer _triangleBuffer;
        private ComputeBuffer _sphereBuffer;

        private Material _multiFrameDenoiserMat;
        private Material _singleFrameDenoiserMat;

        private Vector3 _dirToSun;

        private bool _captured = false;
        
        public int CurrentRenderedFrames { get; private set; } = 0;

        private void UpdateScene()
        {
            // Setup spheres

            _spheres = new();
            GameObject[] spheres = GameObject.FindGameObjectsWithTag("Sphere");
            if (spheres.Length > 0) // Length check here to prevent a 0 length compute buffer error
            {
                for (int i = 0; i < spheres.Length; i++)
                {
                    Sphere sphere = new Sphere
                    {
                        position = spheres[i].transform.position,
                        radius = spheres[i].transform.localScale.x / 2f,
                        material = spheres[i].GetComponent<RayTracedMaterial>().ToRaytracingMaterial()
                    };
                
                    _spheres.Add(sphere);
                }
                _sphereBuffer = new ComputeBuffer(spheres.Length, SizeOf(new Sphere()));
                _sphereBuffer.SetData(_spheres);
                rayTracingShader.SetBuffer(0, "Spheres", _sphereBuffer);
                rayTracingShader.SetInt("NumSpheres", spheres.Length);
            }
            
            RayTracedMesh[] rtMeshes = FindObjectsByType<RayTracedMesh>(FindObjectsSortMode.None);

            if (_allTriangles != null)
                _allTriangles.Clear();
            if (_allMeshes != null)
                _allMeshes.Clear();
            
            _allMeshes = new(); 
            _allTriangles = new();

            // Setup triangles
            if (rtMeshes.Length > 0)
            {
                for (int i = 0; i < rtMeshes.Length; i++)
                {
                    (MeshInfo, Mesh) m;
                    if (_allTriangles != null)
                        m = rtMeshes[i].ToMeshInfo(_allTriangles.Count);
                    else
                        m = rtMeshes[i].ToMeshInfo(0);
                
                    var mesh = m.Item2;
                    Transform meshTransform = rtMeshes[i].transform;

                    Vector3[] vertices = mesh.vertices;
                    for (int j = 0; j < mesh.triangles.Length; j += 3)  // looping over every triplet of indices
                    {
                        Triangle triangle = new Triangle
                        {
                            // Transforming vertices into world space
                            posA = meshTransform.TransformPoint(vertices[mesh.triangles[j]]),
                            posB = meshTransform.TransformPoint(vertices[mesh.triangles[j + 1]]),
                            posC = meshTransform.TransformPoint(vertices[mesh.triangles[j + 2]])
                        };

                        _allTriangles.Add(triangle);
                    }
                    _allMeshes.Add(m.Item1);
                }
            
                _meshBuffer = new ComputeBuffer(_allMeshes.Count, SizeOf(new MeshInfo()));
                _triangleBuffer = new ComputeBuffer(_allTriangles.Count, SizeOf(new Triangle()));
            
                _meshBuffer.SetData(_allMeshes);
                _triangleBuffer.SetData(_allTriangles);
            
                rayTracingShader.SetBuffer(0, "Triangles", _triangleBuffer);
                rayTracingShader.SetBuffer(0, "Meshes", _meshBuffer);
                rayTracingShader.SetInt("NumMeshes", _allMeshes.Count);
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            try
            {
                bool isSceneCam = Camera.current.name == "SceneCamera";
                if (_cam == null)
                    _cam = GetComponent<Camera>();
                
                if (_singleFrameDenoiserMat == null || _singleFrameDenoiserMat.shader != denoiser)
                    _singleFrameDenoiserMat = new Material(denoiser);

                if (rayTracingEnabled)
                {
                    InitRenderTexture();
                    
                    // Configure shader
                    SetShaderParameters();
                    
                    UpdateScene();
                    
                    int threadGroupsX = Mathf.CeilToInt(Screen.width / 32f);
                    int threadGroupsY = Mathf.CeilToInt(Screen.height / 32f);
                    
                    rayTracingShader.SetTexture(0, "Result", _target);
                    
                    if (isSceneCam)
                    {
                        if (rayTraceSceneViewCamera) // Code/Blit calls inside of this if block are run on the scene view camera.
                        {
                            // This is done to prevent a strange noise flickering
                            //
                            // It occurs when the application is running and the
                            // frame is changed but the add shader is not applied
                            // causing noise flickering.
                            rayTracingShader.SetInt("Frame", 0);
                            
                            rayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
                            Graphics.Blit(_target, destination);

                            DisposeComputeBuffers(_meshBuffer, _triangleBuffer, _sphereBuffer);
                        }
                        else
                        {
                            Graphics.Blit(source, destination);
                        }
                    } 
                    else // This is the game view camera
                    {
                        rayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

                        if (transform.hasChanged)
                        {
                            CurrentRenderedFrames = 0;
                            transform.hasChanged = false;
                        }
                        
                        _singleFrameDenoiserMat.SetInt("_Sample", CurrentRenderedFrames);
                        
                        RenderTexture denoised = RenderTexture.GetTemporary(_target.width, _target.height);
                        try // Probably inefficient having a try-catch block inside another try-catch but I can't think of another way of making sure denoised is disposed.
                        {
                            
                            Graphics.Blit(_target, denoised, _singleFrameDenoiserMat);

                            DisposeComputeBuffers(_meshBuffer, _triangleBuffer, _sphereBuffer);
                            CurrentRenderedFrames += Application.isPlaying ? 1 : 0;
                        
                            Graphics.Blit(denoised, destination);
                            Graphics.Blit(denoised, _prevFrame);
                            RenderTexture.ReleaseTemporary(denoised);
                        } catch (Exception e) 
                        {
                            Debug.LogException(e);
                            DisposeComputeBuffers(_meshBuffer, _triangleBuffer, _sphereBuffer);
                            RenderTexture.ReleaseTemporary(denoised);
                            return;
                        }

                        if (autoScreenshot && CurrentRenderedFrames >= samplesBeforeScreenshot && !_captured)
                        {
                            _captured = true;
                            
                            SaveScreenshot(screenshotSize);
                        }
                    }
                }
                else
                {
                    Graphics.Blit(source, destination);
                    DisposeComputeBuffers(_meshBuffer, _triangleBuffer, _sphereBuffer);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Graphics.Blit(source, destination);
                
                DisposeComputeBuffers(_meshBuffer, _triangleBuffer, _sphereBuffer);
            }
        }

        private void InitRenderTexture()
        {
            if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
            {
                _target = new RenderTexture(Screen.width, Screen.height, 24)
                {
                    enableRandomWrite = true
                };
                _target.Create();
            }
        }
        
        private void SetShaderParameters()
        {
            rayTracingShader.SetMatrix("_CameraToWorld", _cam.cameraToWorldMatrix);
            rayTracingShader.SetMatrix("_CameraInverseProjection", _cam.projectionMatrix.inverse);
            
            rayTracingShader.SetInt("maxBounces", bounces);
            rayTracingShader.SetInt("NumRaysPerPixel", numRaysPerPixel);
            
            rayTracingShader.SetVector("GroundColour", groundColour);
            rayTracingShader.SetVector("SkyColourHorizon", skyColourHorizon);
            rayTracingShader.SetVector("SkyColourZenith", skyColourZenith);
            
            rayTracingShader.SetBool("DrawEnvironment", drawEnvironment);
            rayTracingShader.SetBool("DrawSun", drawSun);
            rayTracingShader.SetBool("UseBackfaceCulling", useBackfaceCulling);
            rayTracingShader.SetBool("ShadowsEnabled", drawShadows);
            
            rayTracingShader.SetFloat("RenderDistance", renderDistance);
            
            rayTracingShader.SetFloat("DivergeStrength", divergeStrength);
            
            // TODO: Add support for multiple suns

            if (sun != null)
            {
                _dirToSun = sun.transform.forward;
            }
            
            rayTracingShader.SetVector("SunLightDirection", _dirToSun);
            
            rayTracingShader.SetFloat("SunFocus", sunFocus);
            rayTracingShader.SetFloat("SunIntensity", sunIntensity);

            rayTracingShader.SetInt("Frame", Application.isPlaying ? CurrentRenderedFrames : 0);
        }

        // Utility function for quickly disposing compute buffers.
        public static void DisposeComputeBuffers(params ComputeBuffer[] buffers)
        {
            foreach (var buffer in buffers)
            {
                if (buffer != null)
                {
                    buffer.Release();
                }
            }
        }

        private void CopyRenderTexture(ref RenderTexture original, ref RenderTexture copy)
        {
            if (copy == null || copy.width != original.width || copy.height != original.height)
            {
                if (copy != null)
                {
                    copy.Release();
                }
                copy = new RenderTexture(original.width, original.height, original.depth)
                {
                    enableRandomWrite = true
                };
                copy.Create();
            }
            
            Graphics.Blit(original, copy);
        }
        
        private struct Sphere
        {
            public Vector3 position;
            public float radius;
            public RayTracingMaterial material;
        }
        public struct RayTracingMaterial
        {
            public Vector3 colour;
            public Vector3 specularColour;
            public Vector3 emissionColour;
            public float emissionStrength;
            public float smoothness;
            public float specularProbability;
        }
        public struct Triangle
        {
            public Vector3 posA, posB, posC;
        }
        
        public struct MeshInfo
        {
            public int firstTriangleIndex;
            public int numTriangles;
            public Vector3 boundsMin;
            public Vector3 boundsMax;
            public RayTracingMaterial material;
        }
        
        private void OnDisable()
        {
            try
            {
                DisposeComputeBuffers(_meshBuffer, _triangleBuffer, _sphereBuffer);
            }
            catch
            {
                return;
            }
        }

        private void OnDestroy()
        {
            try
            {
                DisposeComputeBuffers(_meshBuffer, _triangleBuffer, _sphereBuffer);
            }
            catch
            {
                return;
            }
        }
        
        private static int SizeOf(System.Object structure) => System.Runtime.InteropServices.Marshal.SizeOf(structure);

        private void OnValidate()
        {
            if (_cam != null)
            {
                if (renderDistance < _cam.nearClipPlane) renderDistance = _cam.nearClipPlane;

                _cam.farClipPlane = renderDistance;
            }
        }

        public void SaveScreenshot(int superSize)
        {
            string path = Path.Combine(Application.persistentDataPath, $"sample {CurrentRenderedFrames}.png");
            ScreenCapture.CaptureScreenshot(path, superSize);
            Debug.Log($"Saved screenshot to path: {path}");
        }
    }
}