#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.Experimental.Rendering;

namespace private_duck.Utils
{
    [CreateAssetMenu(fileName = "FFT Tester", menuName = "Private_Duck/FFT Tester", order = 0)]
    public class TestFFT : ScriptableObject
    {
        public ComputeShader fftCompute;
        private int resolution;
        private Texture2D input;
        private RenderTexture out_pwsp, out_re, out_im;
        private GFFT fftRuntime;
        private int pwsp_status = 0;
        private void ComputeForward()
        {
            if (!fftCompute)
            {
                var strs = AssetDatabase.FindAssets("PD_Radix_2");
                var assetPath = AssetDatabase.GUIDToAssetPath(strs[0]);
                fftCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(assetPath);
                Debug.Log("Found Compute Shader");
            }

            if (fftRuntime == null)
                fftRuntime = new GFFT(fftCompute);

            fftRuntime.ConfigureForSize(resolution);

            GFFT.CreateRT(ref out_pwsp, GFFT.MultiChannelHalfPrecision, FilterMode.Point, resolution);
            GFFT.CreateRT(ref out_re, GFFT.MultiChannelHalfPrecision, FilterMode.Point, resolution);
            GFFT.CreateRT(ref out_im, GFFT.MultiChannelHalfPrecision, FilterMode.Point, resolution);

            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            fftRuntime.PowerSpectrum_R2R(input, ref out_pwsp);
            sw.Stop();
            Debug.Log($"Power Spectrum took: {sw.ElapsedMilliseconds}ms to complete");

            sw.Restart();
            fftRuntime.ForwardFFT2_R2C(input, ref out_re, ref out_im);
            sw.Stop();
            Debug.Log($"Forward FFT took: {sw.ElapsedMilliseconds}ms to complete");
            pwsp_status = 1;
        }

        private void ComputeInverse()
        {
            if (!fftCompute)
            {
                var strs = AssetDatabase.FindAssets("PD_Radix_2");
                var assetPath = AssetDatabase.GUIDToAssetPath(strs[0]);
                fftCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(assetPath);
                Debug.Log("Found Compute Shader");
            }

            if (fftRuntime == null)
                fftRuntime = new GFFT(fftCompute);

            fftRuntime.ConfigureForSize(resolution);
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            fftRuntime.InverseFFT2_C2R(out_re, out_im, ref out_pwsp);
            sw.Stop();
            Debug.Log($"Inverse FFT took: {sw.ElapsedMilliseconds}ms to complete");
            pwsp_status = 2;
        }

        [CustomEditor(typeof(TestFFT))]
        private class ApertureMakerEditor : Editor
        {
            TexView window;
            private Texture customTex;

            public override void OnInspectorGUI()
            {
                var parent = (TestFFT)target;
                base.OnInspectorGUI();
                EditorGUILayout.Space();

                customTex = EditorGUILayout.ObjectField("Custom Aperture", customTex, typeof(Texture), false) as Texture;

                if (customTex)
                {
                    if (customTex.height != customTex.width)
                    {
                        EditorGUILayout.HelpBox("Must Be A Square Texture", MessageType.Error);
                    }
                    else if (customTex.height > 1024 || customTex.height < 64 || !GFFT.IsPowerOfTwo(((uint)customTex.height)))
                    {
                        EditorGUILayout.HelpBox("Texture Dimensions Must Be Powers Of 2 and Has To Be Between 64 and 1024", MessageType.Error);
                    }
                    else
                    {
                        parent.resolution = customTex.height;
                        parent.input = (Texture2D)customTex;
                    }
                }

                EditorGUILayout.Space();

                if (parent.input)
                {
                    if (GUILayout.Button("Show Input Image"))
                    {
                        CreateWindow(parent.input);
                    }
                    if (GUILayout.Button("Compute The Forward Fourier Transform"))
                    {
                        parent.ComputeForward();
                    }
                }

                if (parent.out_im && parent.out_re)
                {
                    if (GUILayout.Button("Compute The Inverse Fourier Transform"))
                    {
                        parent.ComputeInverse();
                    }
                }

                EditorGUILayout.Space();

                if (parent.pwsp_status == 1)
                {
                    if (GUILayout.Button("Show Power Spectrum"))
                    {
                        CreateWindow(parent.out_pwsp);
                    }
                }
                else if (parent.pwsp_status == 2)
                {
                    if (GUILayout.Button("Show Inverse Transform"))
                    {
                        CreateWindow(parent.out_pwsp);
                    }
                }
                if (parent.out_re)
                {
                    if (GUILayout.Button("Show Real Part"))
                    {
                        CreateWindow(parent.out_re);
                    }
                }
                if (parent.out_im)
                {
                    if (GUILayout.Button("Show Imaginary Part"))
                    {
                        CreateWindow(parent.out_im);
                    }
                }
            }

            private void CreateWindow(Texture texture)
            {
                if (window == null)
                {
                    window = EditorWindow.GetWindow<TexView>();
                    window.titleContent = new GUIContent("Texture Viewer");
                    window.maxSize = new Vector2(400, 400);
                }
                window.m_temp = texture;
                window.Repaint();
                window.Show();
            }
        }


        public class TexView : EditorWindow
        {
            public Texture m_temp;
            void OnGUI()
            {
                if (m_temp)
                {
                    EditorGUI.DrawPreviewTexture(
                        new Rect(new Vector2(10, 10), Vector2.one * 300),
                        m_temp);
                }
            }
        }
    }

}

#endif