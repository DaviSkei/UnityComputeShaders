using UnityEngine;
using System.Collections;

public class SolidColor : MonoBehaviour
{

    public ComputeShader shader;
    public int texResolution = 256;
    private string kernelName = "SolidRed";
    private string kernelName2 = "SolidYellow";
    private string kernelSplitName = "Splitscreen";

    Renderer rend;
    RenderTexture outputTexture;

    int kernelHandle;
    int kernelHandle2;
    int kernelSplit;

    // Use this for initialization
    void Start()
    {
        outputTexture = new RenderTexture(texResolution, texResolution, 0);
        outputTexture.enableRandomWrite = true;
        outputTexture.Create();

        rend = GetComponent<Renderer>();
        rend.enabled = true;

        InitShader();
    }

    private void InitShader()
    {
        kernelHandle = shader.FindKernel(kernelName);
        kernelHandle2 = shader.FindKernel(kernelName2);
        kernelSplit = shader.FindKernel(kernelSplitName);

        shader.SetInt("textResolution", texResolution);

        shader.SetTexture(kernelHandle, "Result", outputTexture);
        shader.SetTexture(kernelSplit, "Result", outputTexture);
 
        rend.material.SetTexture("_MainTex", outputTexture);

        DispatchShader(texResolution / 8, texResolution / 8);
    }

    private void DispatchShader(int x, int y)
    {
        // shader.Dispatch(kernelHandle, x, y, 1);
        shader.Dispatch(kernelSplit, x, y, 1);
    }

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.U))
        {
            DispatchShader(texResolution / 8, texResolution / 8);
        }
    }
}

