using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    private RenderTexture _target;
    private Camera _camera;

    private uint _currentSample = 0;
    private Material _addMaterial;

    public Texture SkyBoxTexture;

    public float speed = 0.1f;

    public Light DirectionalLight;

    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public uint SpheresMax = 100;

    public float SpherePlacementRadius = 100.0f;

    private ComputeBuffer _sphereBuffer;

    private List<Sphere> spheres = new List<Sphere>();

    [Range(1.0f, 10.0f)]
    public int numReflections;

    struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
        public float random;
    };

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

        //define the number of reflections we want
        RayTracingShader.SetInt("numReflections", numReflections);

        // Use a random pixel offset to sample from multiple parts of the pixel
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));

        Vector3 l = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));

        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);
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

        //Graphics.Blit(_target, destination, _addMaterial);
        Graphics.Blit(_target, destination);
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
        List<Sphere> alsoSpheres = new List<Sphere>();
    
        //bob spheres
        for (int i = 0; i < spheres.Count; i++)
        {
            Sphere sphere = spheres[i];

            sphere.position.y += Mathf.Sin(Time.time + sphere.position.z / 20) * 10;

            alsoSpheres.Add(sphere);
        }

        _sphereBuffer.SetData(alsoSpheres);




        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            speed *= 2;
        }

        if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            speed /= 2;
        }

        //movement
        if (Input.GetKey("w"))
        {
            _camera.transform.position += Vector3.forward * speed;
        }

        if (Input.GetKey("s"))
        {
            _camera.transform.position += Vector3.back * speed;
        }

        if (Input.GetKey("a"))
        {
            _camera.transform.position += Vector3.left * speed;
        }

        if (Input.GetKey("d"))
        {
            _camera.transform.position += Vector3.right * speed;
        }

        if (Input.GetKey(KeyCode.Space))
        {
            _camera.transform.position += Vector3.up * speed;
        }

        if (Input.GetKey(KeyCode.LeftControl))
        {
            _camera.transform.position += Vector3.down * speed;
        }

        //Rotation
        if (Input.GetKey("e"))
        {
            _camera.transform.eulerAngles += Vector3.right * speed;
        }

        if (Input.GetKey("r"))
        {
            _camera.transform.eulerAngles += Vector3.left * speed;
        }

        if (Input.GetKey("f"))
        {
            _camera.transform.eulerAngles += Vector3.up * speed;
        }

        if (Input.GetKey("g"))
        {
            _camera.transform.eulerAngles += Vector3.down * speed;
        }


        if (transform.hasChanged)
        {
            //reset samples to 0 if we move the camera
            _currentSample = 0;
            transform.hasChanged = false;
        }
    }

    private void OnEnable()
    {
        _currentSample = 0;
        SetUpScene();
    }

    private void OnDisable()
    {
        if (_sphereBuffer != null)
        {
            _sphereBuffer.Release();
        }
    }

    private void SetUpScene()
    {
        //List<Sphere> spheres = new List<Sphere>();

        //Add a number of random spheres
        for (int i = 0; i < SpheresMax; i++)
        {
            Sphere sphere = new Sphere();

            //Radius and radius
            sphere.radius = SphereRadius.x + Random.value * (SphereRadius.y - SphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * SpherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);
            sphere.random = Random.Range(-5.0f, 5.0f);

            //prevent super speed spheres
            if (sphere.random < 0.1 && sphere.random > -0.1)
            {
                sphere.random += 0.2f;
            }

            foreach (Sphere other in spheres)
            {
                float minDist = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
                {
                    goto SkipSphere;
                }
            }

            //Albedo and specular color
            Color color = Random.ColorHSV();
            bool metal = Random.value < 0.5f;
            sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;

            //Add the sphere to the list
            spheres.Add(sphere);

            SkipSphere:
                continue;
        }


        //create a compute buffer
        _sphereBuffer = new ComputeBuffer(spheres.Count, 44);

    }
    



}
