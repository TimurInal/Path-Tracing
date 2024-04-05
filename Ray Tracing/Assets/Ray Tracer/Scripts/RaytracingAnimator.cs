using System;
using System.Collections;
using System.Collections.Generic;
using RayTracer;
using UnityEngine;

public class RaytracingAnimator : MonoBehaviour
{
    public Transform camera;
    
    public float lerpSpeed = 5;
    public Vector3[] positions =
    {
        new(0, 0, -10),
        new(0, 0, 10),
    };

    public enum EasingMode
    {
        None,
        CubicIn,
        CubicOut,
        CubicInOut
    }

    private int _currentPositionIndex = 0;
    private int _frameCounter = 0;
    public EasingMode easingMode = EasingMode.CubicInOut;
    [Tooltip("The amount of frames the ray tracer needs to render before the camera moves")] public int samplesPerFrame = 1000;
    public int superSize = 5;

    private Raytracer _raytracer;

    private void Awake()
    {
        _raytracer = FindObjectOfType<Raytracer>();
    }
    
    private void LateUpdate()
    {
        _frameCounter++;
        
        if (_frameCounter >= samplesPerFrame) 
        {
            _frameCounter = 0;
            _raytracer.SaveScreenshot(superSize);
            
            _currentPositionIndex++;
            if (_currentPositionIndex >= positions.Length) 
            {
                _currentPositionIndex = 0;
            }
            camera.position = positions[_currentPositionIndex];
        }
    }
}
