﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GPUPhysics : MonoBehaviour {
	struct RigidBody
    {
		public Vector3 position;
		public Quaternion quaternion;
		public Vector3 velocity;
		public Vector3 angularVelocity;
		public int particleIndex;
		public int particleCount;

		public RigidBody(Vector3 pos, int pIndex, int pCount)
        {
			position = pos;
			quaternion = Random.rotation;//Quaternion.identity;
			velocity = angularVelocity = Vector3.zero;
			particleIndex = pIndex;
			particleCount = pCount;
        }
    };

	int SIZE_RIGIDBODY = 13 * sizeof(float) + 2 * sizeof(int);

	struct Particle
    {
		public Vector3 position;
		public Vector3 velocity;
		public Vector3 force;
		public Vector3 localPosition;
		public Vector3 offsetPosition;

		public Particle(Vector3 pos)
        {
			position = velocity = force = offsetPosition = Vector3.zero;
			localPosition = pos;
        }
    };

	int SIZE_PARTICLE = 15 * sizeof(float);

	public bool debugWireframe;

	// set from editor
	public Mesh cubeMesh {
		get {
			return debugWireframe ? CjLib.PrimitiveMeshFactory.BoxWireframe() : CjLib.PrimitiveMeshFactory.BoxFlatShaded();
		}
	}
	public Mesh sphereMesh {
		get {
			return CjLib.PrimitiveMeshFactory.SphereWireframe(6, 6);
		}
	}
	public Mesh lineMesh {
		get {
			return CjLib.PrimitiveMeshFactory.Line(Vector3.zero, new Vector3(1.0f, 1.0f, 1.0f));
		}
	}

	public ComputeShader shader;
	public Material cubeMaterial;
	public Material sphereMaterial;
	public Material lineMaterial;
	public Bounds m_bounds;
	public float m_cubeMass;
	public float scale;
	public int particlesPerEdge;
	public float springCoefficient;
	public float dampingCoefficient;
	public float tangentialCoefficient;
	public float gravityCoefficient;
	public float frictionCoefficient;
	public float angularFrictionCoefficient;
	public float angularForceScalar;
	public float linearForceScalar;
	public Vector3Int gridSize = new Vector3Int(5, 5, 5);
	public Vector3 gridStartPosition;
	public bool useGrid = true;
	public int rigidBodyCount = 1000;
	[Range(1, 20)]
	public int stepsPerUpdate = 10;
	
	// calculated
	private Vector3 m_cubeScale;
	
	int particlesPerBody;
	float particleDiameter;

	RigidBody[] rigidBodiesArray;
	Particle[] particlesArray;
	int[] voxelGridArray;

	ComputeBuffer rigidBodiesBuffer;
	ComputeBuffer particlesBuffer;
	private ComputeBuffer argsBuffer;
	private ComputeBuffer m_bufferWithSphereArgs;
	private ComputeBuffer m_bufferWithLineArgs;
	private ComputeBuffer voxelGridBuffer;                 // int4
	
	private int kernelGenerateParticleValues;
	private int kernelClearGrid;
	private int kernelPopulateGrid;
	private int kernelCollisionDetectionWithGrid;
	private int kernelComputeMomenta;
	private int kernelComputePositionAndRotation;
	private int kernelCollisionDetection;

	private int m_threadGroupsPerRigidBody;
	private int m_threadGroupsPerParticle;
	private int m_threadGroupsPerGridCell;
	private int deltaTimeID;

	private int frameCounter;

	void Start() {
		Application.targetFrameRate = 300;

		m_cubeScale = new Vector3(scale, scale, scale);

		int particlesPerEdgeMinusTwo = particlesPerEdge-2;
		particlesPerBody = particlesPerEdge * particlesPerEdge * particlesPerEdge - particlesPerEdgeMinusTwo*particlesPerEdgeMinusTwo*particlesPerEdgeMinusTwo;
		particleDiameter = scale / particlesPerEdge;

		InitArrays();

		InitRigidBodies();

		InitParticles();

		InitBuffers();

		InitShader();

		voxelGridArray = new int[gridSize.x * gridSize.y * gridSize.z * 4];

		Debug.Log("nparticles: " + rigidBodyCount * particlesPerBody);
		
		InitInstancing();
		
	}

	void InitArrays()
    {
		rigidBodiesArray = new RigidBody[rigidBodyCount];
		particlesArray = new Particle[rigidBodyCount * particlesPerBody];
	}

	void InitRigidBodies()
    {
		Vector3 spawnPosition = new Vector3(0, 5, 0);

		int pIndex = 0;

		for(int i=0; i<rigidBodyCount; i++)
        {
			Vector3 pos = spawnPosition;// + Random.insideUnitSphere * 10.0f;
			pos.y += i * 2;
			rigidBodiesArray[i] = new RigidBody(pos, pIndex, particlesPerBody);
			pIndex += particlesPerBody;
		}
	}

	void InitParticles()
    {
		int count = rigidBodyCount * particlesPerBody;

		particlesArray = new Particle[count];

		// initialize buffers
		// initial local particle positions within a rigidbody
		int index = 0;
		float centerer = scale * -0.5f + particleDiameter * 0.5f;
		Vector3 centeringOffset = new Vector3(centerer, centerer, centerer);

		for (int xIter = 0; xIter < particlesPerEdge; xIter++)
		{
			for (int yIter = 0; yIter < particlesPerEdge; yIter++)
			{
				for (int zIter = 0; zIter < particlesPerEdge; zIter++)
				{
					if (xIter == 0 || xIter == (particlesPerEdge - 1) || yIter == 0 || yIter == (particlesPerEdge - 1) || zIter == 0 || zIter == (particlesPerEdge - 1))
					{
						Vector3 pos = centeringOffset + new Vector3(xIter * particleDiameter, yIter * particleDiameter, zIter * particleDiameter);
						for (int i = 0; i < rigidBodyCount; i++)
						{
							RigidBody body = rigidBodiesArray[i];
							particlesArray[body.particleIndex + index] = new Particle(pos);
						}
						index++;
					}
				}
			}
		}

		//particleVoxelPositionsArray = new int[count * 3];
	}

	void InitBuffers()
    {
		//int total = rigidBodyCount;
		rigidBodiesBuffer = new ComputeBuffer(rigidBodyCount, SIZE_RIGIDBODY);
		rigidBodiesBuffer.SetData(rigidBodiesArray);

		int numOfParticles = rigidBodyCount * particlesPerBody;
		particlesBuffer = new ComputeBuffer(numOfParticles, SIZE_PARTICLE);
		particlesBuffer.SetData(particlesArray);

		int numGridCells = gridSize.x * gridSize.y * gridSize.z;
		voxelGridBuffer = new ComputeBuffer(numGridCells, 4 * sizeof(int));
	}

	void InitShader()
	{
		deltaTimeID = Shader.PropertyToID("deltaTime");

		int[] gridDimensions = new int[] { gridSize.x, gridSize.y, gridSize.z };
		shader.SetInts("gridDimensions", gridDimensions);
		shader.SetInt("gridMax", gridSize.x * gridSize.y * gridSize.z);
		shader.SetInt("particlesPerRigidBody", particlesPerBody);
		shader.SetFloat("particleDiameter", particleDiameter);
		shader.SetFloat("springCoefficient", springCoefficient);
		shader.SetFloat("dampingCoefficient", dampingCoefficient);
		shader.SetFloat("frictionCoefficient", frictionCoefficient);
		shader.SetFloat("angularFrictionCoefficient", angularFrictionCoefficient);
		shader.SetFloat("gravityCoefficient", gravityCoefficient);
		shader.SetFloat("tangentialCoefficient", tangentialCoefficient);
		shader.SetFloat("angularForceScalar", angularForceScalar);
		shader.SetFloat("linearForceScalar", linearForceScalar);
		shader.SetFloat("particleMass", m_cubeMass / particlesPerBody);
		shader.SetFloats("gridStartPosition", new float[] { gridStartPosition.x, gridStartPosition.y, gridStartPosition.z });

		int particleCount = rigidBodyCount * particlesPerBody;
		// Get Kernels
		kernelGenerateParticleValues = shader.FindKernel("GenerateParticleValues");
		kernelClearGrid = shader.FindKernel("ClearGrid");
		kernelPopulateGrid = shader.FindKernel("PopulateGrid");
		kernelCollisionDetection = shader.FindKernel("CollisionDetection");
		kernelComputeMomenta = shader.FindKernel("ComputeMomenta");
		kernelComputePositionAndRotation = shader.FindKernel("ComputePositionAndRotation");

		// Count Thread Groups
		m_threadGroupsPerRigidBody = Mathf.CeilToInt(rigidBodyCount / 8.0f);
		m_threadGroupsPerParticle = Mathf.CeilToInt(particleCount / 8f);
		m_threadGroupsPerGridCell = Mathf.CeilToInt((gridSize.x * gridSize.y * gridSize.z) / 8f);

		// Bind buffers

		// kernel 0 GenerateParticleValues
		shader.SetBuffer(kernelGenerateParticleValues, "rigidBodiesBuffer", rigidBodiesBuffer);
		shader.SetBuffer(kernelGenerateParticleValues, "particlesBuffer", particlesBuffer);
		
		// kernel 1 ClearGrid
		shader.SetBuffer(kernelClearGrid, "voxelGridBuffer", voxelGridBuffer);

		// kernel 2 Populate Grid
		shader.SetBuffer(kernelPopulateGrid, "voxelGridBuffer", voxelGridBuffer);
		shader.SetBuffer(kernelPopulateGrid, "particlesBuffer", particlesBuffer);
		
		// kernel 3 Collision Detection
		shader.SetBuffer(kernelCollisionDetection, "particlesBuffer", particlesBuffer);
		shader.SetBuffer(kernelCollisionDetection, "voxelGridBuffer", voxelGridBuffer);
		
		// kernel 4 Computation of Momenta
		shader.SetBuffer(kernelComputeMomenta, "rigidBodiesBuffer", rigidBodiesBuffer);
		shader.SetBuffer(kernelComputeMomenta, "particlesBuffer", particlesBuffer);
		
		// kernel 5 Compute Position and Rotation
		shader.SetBuffer(kernelComputePositionAndRotation, "rigidBodiesBuffer", rigidBodiesBuffer);
	}

	void InitInstancing() {
		// Setup Indirect Renderer
		cubeMaterial.SetBuffer("rigidBodiesBuffer", rigidBodiesBuffer);

		uint[] args = new uint[] { cubeMesh.GetIndexCount(0), (uint)rigidBodyCount, 0, 0, 0 };
		argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
		argsBuffer.SetData(args);
		
		if (debugWireframe)
		{
			int numOfParticles = rigidBodyCount * particlesPerBody;

			uint[] sphereArgs = new uint[] { sphereMesh.GetIndexCount(0), (uint)numOfParticles, 0, 0, 0 };
			m_bufferWithSphereArgs = new ComputeBuffer(1, sphereArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
			m_bufferWithSphereArgs.SetData(sphereArgs);

			uint[] lineArgs = new uint[] { lineMesh.GetIndexCount(0), (uint)numOfParticles, 0, 0, 0 };
			m_bufferWithLineArgs = new ComputeBuffer(1, lineArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
			m_bufferWithLineArgs.SetData(lineArgs);

			sphereMaterial.SetBuffer("particlesBuffer", particlesBuffer);
			sphereMaterial.SetVector("scale", new Vector4(particleDiameter * 0.5f, particleDiameter * 0.5f, particleDiameter * 0.5f, 1.0f));
			
			lineMaterial.SetBuffer("rigidBodiesBuffer", rigidBodiesBuffer);
		}
	}

	void Update() {
		if (frameCounter++ < 10) {
			return;
		}

		float dt = Time.deltaTime/stepsPerUpdate;
		shader.SetFloat(deltaTimeID, dt);

		for (int i=0; i<stepsPerUpdate; i++) {
			shader.Dispatch(kernelGenerateParticleValues, m_threadGroupsPerRigidBody, 1, 1);
			shader.Dispatch(kernelClearGrid, m_threadGroupsPerGridCell, 1, 1);
			shader.Dispatch(kernelPopulateGrid, m_threadGroupsPerParticle, 1, 1);
			shader.Dispatch(kernelCollisionDetection, m_threadGroupsPerParticle, 1, 1);
			shader.Dispatch(kernelComputeMomenta, m_threadGroupsPerRigidBody, 1, 1);
			shader.Dispatch(kernelComputePositionAndRotation, m_threadGroupsPerRigidBody, 1, 1);
		}

		if (debugWireframe) {
			Graphics.DrawMeshInstancedIndirect(sphereMesh, 0, sphereMaterial, m_bounds, m_bufferWithSphereArgs);
			Graphics.DrawMeshInstancedIndirect(lineMesh, 0, lineMaterial, m_bounds, m_bufferWithLineArgs);
		}
        else
        {
            Graphics.DrawMeshInstancedIndirect(cubeMesh, 0, cubeMaterial, m_bounds, argsBuffer);
		}
	}

	void OnDestroy() {
		rigidBodiesBuffer.Release();
		particlesBuffer.Release();

		voxelGridBuffer.Release();
		
		if (m_bufferWithSphereArgs != null) {
			m_bufferWithSphereArgs.Release();
		}
		if (m_bufferWithLineArgs != null) {
			m_bufferWithLineArgs.Release();
		}
		if (argsBuffer != null) {
			argsBuffer.Release();
		}
	}
}