using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader; 
    private RenderTexture _target; //
    private Camera _camera;

    private uint _currentSample = 0;
    private Material _addMaterial;

    public Texture SkyBoxTexture;

    private void Awake()
    {
        _camera = GetComponent<Camera>(); 
    }

    private void SetShaderParameters()
    {
        //figure out where in the world a specific camera point is
        //then, draw some rays in the shader
        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);   //??
        RayTracingShader.SetTexture(0, "_SkyBoxTexture", SkyBoxTexture);

        // Use a random pixel offset to sample from multiple parts of the pixel
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        // make sure we have a current render target
        InitRenderTexture();

        // set the texture at kernel index 0 using our result to target
        RayTracingShader.SetTexture(0, "Result", _target);

        // create some threads for each 8x8 group of pixels on the screen
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);

        // dispatch the work to the GPU -- get to work!!
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);  // No Z dimension

        // Blit the result texture to the screen
        if (_addMaterial == null)
        {
            // create a new material with our anti-aliasing shader
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        }
        _addMaterial.SetFloat("_Sample", _currentSample);
        Graphics.Blit(_target, destination, _addMaterial);
        _currentSample++;

        //send the texture from the target to the destination
        //Graphics.Blit(_target, destination);
    }

    private void InitRenderTexture()
    {
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            if (_target != null)
            {
                _target.Release(); 
            }

            //set up our target
            _target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _currentSample = 0;
            _target.Create();
        }
    }

    private void Update()
    {
        if (transform.hasChanged)
        {
            //reset samples to 0 if we move the camera
            _currentSample = 0;
            transform.hasChanged = false;
        }
    }

}
