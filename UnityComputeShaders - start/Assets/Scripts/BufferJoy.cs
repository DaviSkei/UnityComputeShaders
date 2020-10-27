﻿using UnityEngine;
using System.Collections;

public class BufferJoy : MonoBehaviour
{

    public ComputeShader shader;
    public int texResolution = 1024;

    Renderer rend;
    RenderTexture outputTexture;

    int circlesHandle;

    public Color clearColor = new Color();
    public Color circleColor = new Color();

    int count = 10;

    // Use this for initialization
    void Start()
    {
        outputTexture = new RenderTexture(texResolution, texResolution, 0);
        outputTexture.enableRandomWrite = true;
        outputTexture.Create();

        rend = GetComponent<Renderer>();
        rend.enabled = true;

        InitData();

        InitShader();
    }

    private void InitData()
    {
        circlesHandle = shader.FindKernel("Circles");
    }

    private void InitShader()
    {
        shader.SetVector( "clearColor", clearColor );
        shader.SetVector( "circleColor", circleColor );
        shader.SetInt( "texResolution", texResolution );

        shader.SetTexture( circlesHandle, "Result", outputTexture );

        rend.material.SetTexture("_MainTex", outputTexture);
    }
 
    private void DispatchKernel(int count)
    {
        shader.SetFloat("time", Time.time);
        shader.SetBool("clearTexture", true );
        shader.Dispatch(circlesHandle, count, 1, 1);
    }

    void Update()
    {
        DispatchKernel(count);
    }
}
