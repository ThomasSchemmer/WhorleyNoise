using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WhirleyNoiseCreator : MonoBehaviour
{

    public struct Point
    {
        public Vector2 position;
    }

    public ComputeShader shader;
    public RenderTexture rt;

    private int createNoiseKernel, debugKernel, clearKernel;
    private ComputeBuffer pointBuffer;
    private ComputeBuffer debug;
    private ComputeBuffer distanceBuffer;
    private ComputeBuffer groupDistanceBuffer;
    private ComputeBuffer maxMinDistanceBuffer;
    private ComputeBuffer directionsBuffer;
    private List<Point> points;
    //has to be a square number, as we construct a grid out of it
    private const int POINTCOUNT = 100;
    //has to be a multiple of 16, as each threadgroup gets at least 16 pixels
    //this avoids "split" pixels in between threads
    private const int SIZE = 1024;
    private const int GROUPX = SIZE / 16, GROUPY = SIZE / 16;

    private Vector2[] directions = new Vector2[9]{
        new Vector2(+0, +0),
        new Vector2(+1, +0),
        new Vector2(+1, -1),
        new Vector2(+0, -1),
        new Vector2(-1, -1),
        new Vector2(-1, +0),
        new Vector2(-1, +1),
        new Vector2(+0, +1),
        new Vector2(+1, +1)
    };

    // Start is called before the first frame update
    void Start()
    {
        Random.InitState(1);
        CreateRT();
        CreateAndFillBuffer();
        Vector2[] arr = new Vector2[SIZE * SIZE];
        debug.GetData(arr);
        ExportAsPNG();
    }

    private void CreateAndFillBuffer()
    {
        points = new List<Point>();
        float size = Mathf.Sqrt(POINTCOUNT);
        float scale = 1 / size;
        for(int y = 0; y < size; y++)
        {
            for(int x = 0; x < size; x++)
            {
                Point p;
                p.position = new Vector2(scale * x + Random.Range(0, scale), scale * y + Random.Range(0, scale)) * SIZE;
                //p.position = new Vector2(scale * x + 0.1f, scale * y + 0.1f) * SIZE;
                points.Add(p);
            }
        }

        pointBuffer = new ComputeBuffer(POINTCOUNT, sizeof(float) * 2);
        pointBuffer.SetData(points);
        debug = new ComputeBuffer(SIZE * SIZE, sizeof(float) * 2);
        distanceBuffer = new ComputeBuffer(SIZE * SIZE, sizeof(float));
        groupDistanceBuffer = new ComputeBuffer(GROUPX * GROUPY, sizeof(float));
        maxMinDistanceBuffer = new ComputeBuffer(1, sizeof(float));
        directionsBuffer = new ComputeBuffer(directions.Length, sizeof(float) * 2);
        directionsBuffer.SetData(directions);

        createNoiseKernel = shader.FindKernel("CreateNoiseTexture");
        debugKernel = shader.FindKernel("Debug");

        shader.SetBuffer(createNoiseKernel, "points", pointBuffer);
        shader.SetBuffer(createNoiseKernel, "debug", debug);
        shader.SetBuffer(createNoiseKernel, "distances", distanceBuffer);
        shader.SetBuffer(createNoiseKernel, "groupDistances", groupDistanceBuffer);
        shader.SetBuffer(createNoiseKernel, "maxMinDistance", maxMinDistanceBuffer);
        shader.SetBuffer(createNoiseKernel, "directions", directionsBuffer);
        shader.SetTexture(createNoiseKernel, "result", rt);
        shader.SetTexture(debugKernel, "result", rt);
        shader.SetBuffer(debugKernel, "points", pointBuffer);
        shader.SetInt("pointCount", POINTCOUNT);
        shader.SetInt("size", SIZE);
        shader.SetInt("groupX", GROUPX);
        shader.SetInt("groupY", GROUPY);
        shader.SetInt("directionsCount", directions.Length);

        shader.Dispatch(createNoiseKernel, SIZE / 16, SIZE / 16, 1);
        //shader.Dispatch(debugKernel, 1, 1, 1);
    }

    private void CreateRT()
    {
        rt = RenderTexture.GetTemporary(SIZE, SIZE, 0, RenderTextureFormat.ARGB32);
        rt.enableRandomWrite = true;
        rt.Create();
    }


    private void OnDestroy()
    {
        RenderTexture.ReleaseTemporary(rt);
        pointBuffer.Dispose();
        debug.Dispose();
        distanceBuffer.Dispose();
        groupDistanceBuffer.Dispose();
        maxMinDistanceBuffer.Dispose();
        directionsBuffer.Dispose();
    }

    private void ExportAsPNG() {
        Texture2D tex = new Texture2D(SIZE, SIZE);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, SIZE, SIZE), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        byte[] bytes = tex.EncodeToPNG();
        string path = Application.persistentDataPath + "/p.png";
        System.IO.File.WriteAllBytes(path, bytes);
        Debug.Log("Output to :" + (path));
    }
}
