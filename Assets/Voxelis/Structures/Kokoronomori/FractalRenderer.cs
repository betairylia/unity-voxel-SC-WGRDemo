//using UnityEngine;
//using System.Collections;

//using Assets.FractalSystemCore;
//using Assets.FractalSystemCore.NodeInherits;

//public enum FractalType
//{
//    boxTest,
//    treeVer1,
//    treeVer1_ReducedVertices,
//    treeVer2Cyc_ConcretedNormals,
//    treeVer3Cyc_Spline
//}

//public class fractalRenderer : MonoBehaviour
//{
//    FractalSystemNode startNode;
//    public int iterationMax = 10;
//    public float iterGrowRate = 1.0f;

//    public const int verticesMax = 60000;//normals, uvs, tangents = vertices
//    public const int indicesMax = 524286;

//    public FractalType fractalType = FractalType.boxTest;
//    public float startGrowRate = 128.0f;

//    public bool renderNormals = true;
//    public bool renderUV1s = false;
//    public bool renderUV2s = false;
//    public bool renderTangents = false;

//    public int randomSeed = 0;

//    int stoppedCount = 0;//DEBUGGING VARIABLE

//    MeshFilter mFilter;
//    Vector3[] vertices = new Vector3[verticesMax];
//    int[] indices = new int[indicesMax];
//    Vector3[] normals = new Vector3[verticesMax];
//    int verticesCount = 0, indicesCount = 0, normalsCount = 0, tmp;

//    // Use this for initialization
//    void Start()
//    {
//        UnityEngine.Random.seed = randomSeed;

//        startNode = new TreeVer4_Ultra();
//        startNode.growRate = startGrowRate;

//        Descript(startNode, 0);

//        //Debug.Log(stoppedCount);

//        RenderMesh();
//    }

//    public void Descript(FractalSystemNode node, int depth)
//    {
//        if (node.growRate < iterGrowRate)
//        {
//            stoppedCount++;
//            return;
//        }
//        if (depth >= iterationMax)
//        {
//            //因为在这个工程中，只会渲染整棵树，所以不需要下面串起来的链表。
//            node.ClearNode();
//            return;
//        }

//        if (node.child.Count == 0)//这个节点还没有展开过
//        {
//            node.generateChildren();
//        }
//        //node.updateChildren();
//        foreach (FractalSystemNode child in node.child)
//        {
//            Descript(child, depth + 1);
//        }

//        //同样因为没有链表，所以不用进行后续的处理。
//    }

//    public void RenderMesh()
//    {
//        mFilter = gameObject.GetComponent<MeshFilter>();
//        FractalRenderState state;
//        state.centerPos = new Vector3(0, 0, 0);
//        state.rotation = Quaternion.identity;

//        RenderNodeRec(state, startNode);

//        Debug.Log("Render summary: Vertices count = " + verticesCount + " Indices count = " + indicesCount + " (" + fractalType.ToString() + ")");

//        Mesh mesh = new Mesh();
//        mesh.hideFlags = HideFlags.DontSave;
//        mesh.vertices = vertices;
//        mesh.triangles = indices;

//        if (renderNormals) { mesh.normals = normals; }
//        if (renderUV1s) { /*mesh.uv = uv1s;*/ }
//        if (renderUV2s) { /*mesh.uv2 = uv2s;*/ }
//        if (renderTangents) { /*mesh.tangents = tangents*/ }

//        mFilter.mesh = mesh;
//    }

//    void RenderNodeRec(FractalRenderState state, FractalSystemNode node)
//    {
//        // TODO: blocks
//        //node.Express(
//        //    vertices, ref verticesCount,    //Vertices
//        //    indices, ref indicesCount,      //Indices
//        //    normals, ref normalsCount,      //Normals
//        //    null, ref tmp,                  //TexCoord(uv)1
//        //    null, ref tmp,                  //TexCoord(uv)2
//        //    null, ref tmp,                  //Tangents
//        //    ref state);

//        foreach (FractalSystemNode child in node.child)
//        {
//            RenderNodeRec(state, child);
//        }
//    }

//    // Update is called once per frame
//    void Update()
//    {
//        //startNode.growRate *= 1 + (0.2f * Time.deltaTime);

//        //verticesCount = 0;
//        //indicesCount = 0;

//        //Descript(startNode, 0);
//        //RenderMesh();
//    }
//}