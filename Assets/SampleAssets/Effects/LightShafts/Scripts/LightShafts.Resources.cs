using System;
using UnityEngine;

public partial class LightShafts : MonoBehaviour
{
    public LightShaftsShadowmapMode shadowmapMode = LightShaftsShadowmapMode.Dynamic;
    public Camera[] cameras;
    public Camera currentCamera;
    public Vector3 size = new Vector3(10, 10, 20);
    public float spotNear = 0.1f;
    public float spotFar = 1.0f;
    public LayerMask cullingMask = ~0;
    public LayerMask colorFilterMask = 0;
    public float brightness = 5;
    public float brightnessColored = 5;
    public float extinction = 0.5f;
    public float minDistFromCamera = 0.0f;
    public int shadowmapRes = 1024;
    public Shader depthShader;
    public Shader colorFilterShader;
    public bool colored = false;
    public float colorBalance = 1.0f;
    public int epipolarLines = 256;
    public int epipolarSamples = 512;
    public Shader coordShader;
    public Shader depthBreaksShader;
    public Shader raymarchShader;
    public Shader interpolateAlongRaysShader;
    public Shader samplePositionsShader;
    public Shader finalInterpolationShader;
    public float depthThreshold = 0.5f;
    public int interpolationStep = 32;
    public bool showSamples = false;
    public bool showInterpolatedSamples = false;
    public float showSamplesBackgroundFade = 0.8f;
    public bool attenuationCurveOn = false;
    public AnimationCurve attenuationCurve;

    private LightShaftsShadowmapMode m_ShadowmapModeOld = LightShaftsShadowmapMode.Dynamic;
    private bool m_ShadowmapDirty = true;
    private Camera m_ShadowmapCamera;
    private RenderTexture m_Shadowmap;
    private RenderTexture m_ColorFilter;
    private RenderTexture m_CoordEpi;
    private RenderTexture m_DepthEpi;
    private Material m_CoordMaterial;
    private Camera m_CoordsCamera;
    private RenderTexture m_InterpolationEpi;
    private Material m_DepthBreaksMaterial;
    private RenderTexture m_RaymarchedLightEpi;
    private Material m_RaymarchMaterial;
    private RenderTexture m_InterpolateAlongRaysEpi;
    private Material m_InterpolateAlongRaysMaterial;
    private RenderTexture m_SamplePositions;
    private Material m_SamplePositionsMaterial;
    private Material m_FinalInterpolationMaterial;
    private Texture2D m_AttenuationCurveTex;
    private LightType m_LightType = LightType.Directional;
    private bool m_DX11Support;
    private bool m_MinRequirements;


    private void InitLUTs()
    {
        if (m_AttenuationCurveTex)
        {
            return;
        }

        m_AttenuationCurveTex = new Texture2D(256, 1, TextureFormat.ARGB32, false, true);
        m_AttenuationCurveTex.wrapMode = TextureWrapMode.Clamp;
        m_AttenuationCurveTex.hideFlags = HideFlags.HideAndDontSave;

        if (attenuationCurve == null || attenuationCurve.length == 0)
        {
            attenuationCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 1));
        }

        if (m_AttenuationCurveTex)
        {
            UpdateLUTs();
        }
    }


    public void UpdateLUTs()
    {
        InitLUTs();

        if (attenuationCurve == null)
        {
            return;
        }

        for (int i = 0; i < 256; ++i)
        {
            float v = Mathf.Clamp(attenuationCurve.Evaluate(i/255.0f), 0.0f, 1.0f);
            m_AttenuationCurveTex.SetPixel(i, 0, new Color(v, v, v, v));
        }
        m_AttenuationCurveTex.Apply();
    }


    private void InitRenderTexture(ref RenderTexture rt, int width, int height, int depth, RenderTextureFormat format,
                                   bool temp = true)
    {
        if (temp)
        {
            rt = RenderTexture.GetTemporary(width, height, depth, format);
        }
        else
        {
            if (rt != null)
            {
                if (rt.width == width && rt.height == height && rt.depth == depth && rt.format == format)
                {
                    return;
                }

                rt.Release();
                DestroyImmediate(rt);
            }

            rt = new RenderTexture(width, height, depth, format);
            rt.hideFlags = HideFlags.HideAndDontSave;
        }
    }


    private void InitShadowmap()
    {
        bool dynamic = (shadowmapMode == LightShaftsShadowmapMode.Dynamic);
        if (dynamic && shadowmapMode != m_ShadowmapModeOld)
        {
            // Destroy static render textures, we only need temp now
            if (m_Shadowmap)
            {
                m_Shadowmap.Release();
            }
            if (m_ColorFilter)
            {
                m_ColorFilter.Release();
            }
        }
        InitRenderTexture(ref m_Shadowmap, shadowmapRes, shadowmapRes, 24, RenderTextureFormat.RFloat, dynamic);
        m_Shadowmap.filterMode = FilterMode.Point;
        m_Shadowmap.wrapMode = TextureWrapMode.Clamp;

        if (colored)
        {
            InitRenderTexture(ref m_ColorFilter, shadowmapRes, shadowmapRes, 0, RenderTextureFormat.ARGB32, dynamic);
        }

        m_ShadowmapModeOld = shadowmapMode;
    }


    private void ReleaseShadowmap()
    {
        if (shadowmapMode == LightShaftsShadowmapMode.Static)
        {
            return;
        }

        RenderTexture.ReleaseTemporary(m_Shadowmap);
        RenderTexture.ReleaseTemporary(m_ColorFilter);
    }


    private void InitEpipolarTextures()
    {
        epipolarLines = epipolarLines < 8 ? 8 : epipolarLines;
        epipolarSamples = epipolarSamples < 4 ? 4 : epipolarSamples;

        InitRenderTexture(ref m_CoordEpi, epipolarSamples, epipolarLines, 0, RenderTextureFormat.RGFloat);
        m_CoordEpi.filterMode = FilterMode.Point;
        InitRenderTexture(ref m_DepthEpi, epipolarSamples, epipolarLines, 0, RenderTextureFormat.RFloat);
        m_DepthEpi.filterMode = FilterMode.Point;
        InitRenderTexture(ref m_InterpolationEpi, epipolarSamples, epipolarLines, 0,
                          m_DX11Support ? RenderTextureFormat.RGInt : RenderTextureFormat.RGFloat);
        m_InterpolationEpi.filterMode = FilterMode.Point;

        InitRenderTexture(ref m_RaymarchedLightEpi, epipolarSamples, epipolarLines, 24,
                          RenderTextureFormat.ARGBFloat);
        m_RaymarchedLightEpi.filterMode = FilterMode.Point;
        InitRenderTexture(ref m_InterpolateAlongRaysEpi, epipolarSamples, epipolarLines, 0,
                          RenderTextureFormat.ARGBFloat);
        m_InterpolateAlongRaysEpi.filterMode = FilterMode.Point;
    }


    private void InitMaterial(ref Material material, Shader shader)
    {
        if (material || !shader)
        {
            return;
        }
        material = new Material(shader);
        material.hideFlags = HideFlags.HideAndDontSave;
    }


    private void InitMaterials()
    {
        InitMaterial(ref m_FinalInterpolationMaterial, finalInterpolationShader);
        InitMaterial(ref m_CoordMaterial, coordShader);
        InitMaterial(ref m_SamplePositionsMaterial, samplePositionsShader);
        InitMaterial(ref m_RaymarchMaterial, raymarchShader);
        InitMaterial(ref m_DepthBreaksMaterial, depthBreaksShader);
        InitMaterial(ref m_InterpolateAlongRaysMaterial, interpolateAlongRaysShader);
    }


    private Mesh m_SpotMesh;
    private float m_SpotMeshNear = -1;
    private float m_SpotMeshFar = -1;
    private float m_SpotMeshAngle = -1;
    private float m_SpotMeshRange = -1;


    private void InitSpotFrustumMesh()
    {
        if (!m_SpotMesh)
        {
            m_SpotMesh = new Mesh();
            m_SpotMesh.hideFlags = HideFlags.HideAndDontSave;
        }

        Light l = GetComponent<Light>();
        if (m_SpotMeshNear != spotNear || m_SpotMeshFar != spotFar || m_SpotMeshAngle != l.spotAngle ||
            m_SpotMeshRange != l.range)
        {
            float far = l.range*spotFar;
            float near = l.range*spotNear;
            float tan = Mathf.Tan(l.spotAngle*Mathf.Deg2Rad*0.5f);
            float halfwidthfar = far*tan;
            float halfwidthnear = near*tan;

            var vertices = (m_SpotMesh.vertices != null && m_SpotMesh.vertices.Length == 8)
                                     ? m_SpotMesh.vertices
                                     : new Vector3[8];
            vertices[0] = new Vector3(-halfwidthfar, -halfwidthfar, far);
            vertices[1] = new Vector3(halfwidthfar, -halfwidthfar, far);
            vertices[2] = new Vector3(halfwidthfar, halfwidthfar, far);
            vertices[3] = new Vector3(-halfwidthfar, halfwidthfar, far);
            vertices[4] = new Vector3(-halfwidthnear, -halfwidthnear, near);
            vertices[5] = new Vector3(halfwidthnear, -halfwidthnear, near);
            vertices[6] = new Vector3(halfwidthnear, halfwidthnear, near);
            vertices[7] = new Vector3(-halfwidthnear, halfwidthnear, near);
            m_SpotMesh.vertices = vertices;

            if (m_SpotMesh.triangles == null || m_SpotMesh.triangles.Length != 36)
            {
                //                          far           near          top           right         left          bottom
                var triangles = new int[]
                    {
                        0, 1, 2, 0, 2, 3, 6, 5, 4, 7, 6, 4, 3, 2, 6, 3, 6, 7, 2, 1, 5, 2, 5, 6, 0, 3, 7, 0, 7, 4, 5, 1, 0,
                        5, 0, 4
                    };
                m_SpotMesh.triangles = triangles;
            }

            m_SpotMeshNear = spotNear;
            m_SpotMeshFar = spotFar;
            m_SpotMeshAngle = l.spotAngle;
            m_SpotMeshRange = l.range;
        }
    }


    public void UpdateLightType()
    {
        m_LightType = GetComponent<Light>().type;
    }


    public bool CheckMinRequirements()
    {
        if (m_MinRequirements)
        {
            return true;
        }

        m_DX11Support = SystemInfo.graphicsShaderLevel >= 50;

        m_MinRequirements = SystemInfo.graphicsShaderLevel >= 30;
        m_MinRequirements &= SystemInfo.supportsRenderTextures;
        m_MinRequirements &= SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGFloat);
        m_MinRequirements &= SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat);

        return m_MinRequirements;
    }


    private void InitResources()
    {
        UpdateLightType();

        InitMaterials();
        InitEpipolarTextures();
        InitLUTs();
        InitSpotFrustumMesh();
    }


    private void ReleaseResources()
    {
        ReleaseShadowmap();
        RenderTexture.ReleaseTemporary(m_CoordEpi);
        RenderTexture.ReleaseTemporary(m_DepthEpi);
        RenderTexture.ReleaseTemporary(m_InterpolationEpi);
        RenderTexture.ReleaseTemporary(m_RaymarchedLightEpi);
        RenderTexture.ReleaseTemporary(m_InterpolateAlongRaysEpi);
    }
}
