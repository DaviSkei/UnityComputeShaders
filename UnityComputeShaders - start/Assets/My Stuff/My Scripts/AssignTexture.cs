using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AssignTexture : MonoBehaviour
{
    public ComputeShader computeShader;
    public int textRes = 256;
    Renderer renderer;
    RenderTexture outTexture;
    int kernelHandle;
    // Start is called before the first frame update
    void Start()
    {
        // create a new render texture, which is a square, using my variable for the resolution.
        outTexture = new RenderTexture(textRes, textRes, 0);
        outTexture.enableRandomWrite = true;
        outTexture.Create();

        renderer = GetComponent<Renderer>();
        renderer.enabled = true;

        InitShader();
    }
    private void InitShader()
    {
        // initialize compute shader by accessing the main method, and setting my rendertexture to be the same
        // as the variable MainTex, being output through Result
        kernelHandle = computeShader.FindKernel("CSMain");
        computeShader.SetTexture(kernelHandle, "Result", outTexture);
        renderer.material.SetTexture("_MainTex", outTexture);

        DispatchShader(textRes/16, textRes/16);
    }
    private void DispatchShader(int x, int y)
    {
        // this starts the shader execution
        computeShader.Dispatch(kernelHandle, x, y, 1);
    }

    // Update is called once per frame
    void Update()
    {
        // this will change the resolution of the shader on keypress
        if(Input.GetKeyDown(KeyCode.U))
        {
            DispatchShader(textRes/8, textRes/8);
        }
    }
}
