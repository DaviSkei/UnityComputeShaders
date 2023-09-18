using UnityEngine;
using System.Collections;


public class MeshDeform : MonoBehaviour
{
    public ComputeShader shader;
    [Range(0.5f, 2.0f)]
	public float radius;
	
    public struct Vertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vertex(Vector3 pos, Vector3 nor)
        {
            position.x = pos.x;
            position.y = pos.y;
            position.z = pos.z;

            normal.x = nor.x;
            normal.y = nor.y;
            normal.z = nor.z;
        }
    }
    int kernelHandle;
    Mesh mesh;
    Vertex[] newVertexArray;
    Vertex[] initialArray;
    ComputeBuffer newVertexbuffer;
    ComputeBuffer initialBuffer;
    
    // Use this for initialization
    void Start()
    {
        if (InitData())
        {
            InitShader();
        }
    }

    private bool InitData()
    {
        kernelHandle = shader.FindKernel("CSMain");

        MeshFilter mf = GetComponent<MeshFilter>();

        if (mf == null)
        {
            Debug.Log("No MeshFilter found");
            return false;
        }

        InitVertexArrays(mf.mesh);
        InitGPUBuffers();

        mesh = mf.mesh;

        return true;
    }

    private void InitShader()
    {
        shader.SetFloat("radius", radius);
    }
    
    private void InitVertexArrays(Mesh mesh)
    {
        // crete the length of the 2 arrays
        newVertexArray = new Vertex[mesh.vertices.Length];
        initialArray = new Vertex[mesh.vertices.Length];

        for (int i = 0; i < newVertexArray.Length; i++)
        {
            // set the values of the vertex struct constructor to equal whats in the meshfilter
            Vertex vert1 = new Vertex(mesh.vertices[i], mesh.normals[i]);
            newVertexArray[i] = vert1;

            Vertex vert2 = new Vertex(mesh.vertices[i], mesh.normals[i]);
            initialArray[i] = vert2;
        }
    }

    private void InitGPUBuffers()
    {
        newVertexbuffer = new ComputeBuffer(newVertexArray.Length, sizeof(float) * 6);
        newVertexbuffer.SetData(newVertexArray);

        initialBuffer = new ComputeBuffer(initialArray.Length, sizeof(float) * 6);
        initialBuffer.SetData(initialArray);

        // feed the buffered data to go into our main kernel
        shader.SetBuffer(kernelHandle, "vertexBuffer", newVertexbuffer);
        shader.SetBuffer(kernelHandle, "initialBuffer", initialBuffer);
    }
    
    void GetVerticesFromGPU()
    {
        newVertexbuffer.GetData(newVertexArray);
        Vector3[] vertices = new Vector3[newVertexArray.Length];
        Vector3[] normals = new Vector3[newVertexArray.Length];

        for (int i = 0; i < newVertexArray.Length; i++)
        {
            vertices[i] = newVertexArray[i].position;
            normals[i] = newVertexArray[i].normal;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
    }

    void Update(){
        if (shader)
        {
        	shader.SetFloat("radius", radius);
            float deltaTime = (Mathf.Sin(Time.time) + 1)/ 2;
            shader.SetFloat("deltaTime", deltaTime);
            shader.Dispatch(kernelHandle, newVertexArray.Length, 1, 1);
            
            GetVerticesFromGPU();
            Debug.Log("Output");
        }
        else
        {
            Debug.Log("No output");
        }
    }

    void OnDestroy()
    {
        
    }
}

