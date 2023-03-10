using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using System.Runtime.InteropServices;


namespace private_duck.Utils
{
    public class FFT : System.IDisposable
    {
        public enum FFTMode
        {
            SingleChannel = 0,
            MultiChannel
        }

        #region public Getters
        /// <summary>
        /// Texture Format for single channel FFT (16 bit)
        /// </summary>
        public static GraphicsFormat SingleChannelHalfPrecision { get => GraphicsFormat.R16_SFloat; }

        /// <summary>
        /// Texture Format for multi channel FFT (16 bit per channel)
        /// </summary>
        public static GraphicsFormat MultiChannelHalfPrecision { get => GraphicsFormat.R16G16B16A16_SFloat; }

        /// <summary>
        /// Texture Format for single channel FFT (32 bit)
        /// </summary>
        public static GraphicsFormat SingleChannelSinglePrecision { get => GraphicsFormat.R32_SFloat; }

        /// <summary>
        /// Texture Format for multi channel FFT (32 bit per channel)
        /// </summary>
        public static GraphicsFormat MultiChannelSinglePrecision { get => GraphicsFormat.R32G32B32A32_SFloat; }

        /// <summary>
        /// Currently configured Size for this instance
        /// </summary>
        public int GetSize { get => size; }

        #endregion

        private int size;
        private int logSize;
        private ComputeShader fftCompute;
        private ComputeBuffer firstStageTemp;

        /// <summary>
        /// Initializes an instance of a 2D radix-2 FFT Algorithm
        /// </summary>
        /// <param name="cs">Compute Shader that contains the implementation of the 2D FFT (_PD_FFT.compute)</param>
        public FFT(ComputeShader cs)
        {
            disposedValue = false;

            for (int i = 0; i < kernels.Length; i++)
            {
                try
                {
                    int krn = cs.FindKernel(kernels[i]);
                }
                catch (System.Exception)
                {
                    throw new System.Exception($"Incorrect Compute Shader!");
                }
            }
            fftCompute = cs;
        }

        /// <summary>
        /// Initializes an instance of a 2D radix-2 FFT Algorithm
        /// </summary>
        /// <param name="cs">Compute Shader that contains the implementation of the 2D FFT (PD_FFT.compute)</param>
        /// <param name="size">Size of the FFT (size = 2^m where m : integer)</param>
        public FFT(ComputeShader cs, int size, FFTMode mode)
        {
            disposedValue = false;

            if (size < 64 || size > 1024 || !IsPowerOfTwo(((uint)size)))
            {
                throw new System.Exception("Size mismatch! (64 <= size <= 1024 && size has to be a power of 2)");
            }

            for (int i = 0; i < kernels.Length; i++)
            {
                try
                {
                    int krn = cs.FindKernel(kernels[i]);
                }
                catch (System.Exception)
                {
                    throw new System.Exception($"Incorrect Compute Shader!");
                }
            }

            fftCompute = cs;
            ConfigureForSize(size, mode);
        }

        /// <summary>
        /// Initializes the FFT Runtime
        /// </summary>
        /// <param name="size">Size of the FFT (size = 2^m where m : integer)</param>
        /// <param name="mode">Preconfigure FFT to Compute Multi channel FFTs or Single Channel FFTs.
        /// (default: Multi Channel)</param>
        public void ConfigureForSize(int size, FFTMode mode = FFTMode.MultiChannel)
        {
            if (size < 64 || size > 1024 || !IsPowerOfTwo(((uint)size)))
            {
                throw new System.Exception("GPU FFT: Size mismatch! (64 <= size <= 1024 && size must to be a power of 2)");
            }

            this.size = size;
            this.logSize = (int)Mathf.Log(size, 2);

            for (int i = 0; i < keywords.Length; i++)
            {
                fftCompute.DisableKeyword(keywords[i]);
            }

            fftCompute.EnableKeyword(keywords[logSize - 6]);

            firstStageTemp?.Dispose();
            firstStageTemp = new ComputeBuffer(size * size, Marshal.SizeOf(typeof(comp3)));

            fftCompute.SetBuffer(0, "firstStageBuffer", firstStageTemp);
            fftCompute.SetBuffer(1, "firstStageBuffer", firstStageTemp);
            fftCompute.SetBuffer(2, "firstStageBuffer", firstStageTemp);

            fftCompute.DisableKeyword("Single");
            fftCompute.DisableKeyword("Multi");

            if (mode == FFTMode.MultiChannel)
                fftCompute.EnableKeyword("Multi");
            else
                fftCompute.EnableKeyword("Single");
        }

        /// <summary>
        /// Computes the Complex-2D frequency domain transform of a 2D-Real time domain function.
        /// (zero-frequency elements in the center)
        /// </summary>
        /// <param name="inputRE">Real part of the input function</param>
        /// <param name="outputRE">Real part of the output function</param>
        /// <param name="outputIM">Imaginary part of the output function</param>
        public void ForwardFFT2_R2C(Texture inputRE, ref RenderTexture outputRE, ref RenderTexture outputIM)
        {
            fftCompute.SetTexture(0, ShaderIDs.inputREID, inputRE);
            fftCompute.SetTexture(0, ShaderIDs.outputREID, outputRE);
            fftCompute.SetTexture(0, ShaderIDs.outputIMID, outputIM);
            fftCompute.SetBool(ShaderIDs.inverseID, false);
            fftCompute.SetBool(ShaderIDs.horizontalID, true);
            fftCompute.Dispatch(0, 1, size, 1);

            fftCompute.SetBool(ShaderIDs.horizontalID, false);
            fftCompute.Dispatch(0, 1, size, 1);
        }

        /// <summary>
        /// Computes the Complex-2D frequency domain transform of a 2D-Real time domain function. 
        /// (zero-frequency elements in the center)
        /// </summary>
        /// <param name="cmb">CommandBuffer to queue dispatch calls</param>
        /// <param name="inputRE">Real part of the input function</param>
        /// <param name="outputRE">Real part of the output function</param>
        /// <param name="outputIM">Imaginary part of the output function</param>
        public void ForwardFFT2_R2C(CommandBuffer cmb, Texture inputRE, ref RenderTexture outputRE, ref RenderTexture outputIM)
        {
            fftCompute.SetTexture(0, ShaderIDs.inputREID, inputRE);
            fftCompute.SetTexture(0, ShaderIDs.outputREID, outputRE);
            fftCompute.SetTexture(0, ShaderIDs.outputIMID, outputIM);
            cmb.SetComputeIntParam(fftCompute, ShaderIDs.inverseID, 0);
            cmb.SetComputeIntParam(fftCompute, ShaderIDs.horizontalID, 1);
            cmb.DispatchCompute(fftCompute, 0, 1, size, 1);
            cmb.SetComputeIntParam(fftCompute, ShaderIDs.horizontalID, 0);
            cmb.DispatchCompute(fftCompute, 0, 1, size, 1);
        }

        /// <summary>
        /// Computes the Real-2D time domain transform of a 2D-Complex frequency domain function
        /// (zero-frequency elements should be in the center)
        /// </summary>
        /// <param name="inputRE">Real part of the input function</param>
        /// <param name="inputIM">Imaginary part of the input function</param>   
        /// <param name="outputRE">Real part of the output function</param>
        public void InverseFFT2_C2R(Texture inputRE, Texture inputIM, ref RenderTexture outputRE)
        {
            fftCompute.SetTexture(1, ShaderIDs.inputREID, inputRE);
            fftCompute.SetTexture(1, ShaderIDs.inputIMID, inputIM);
            fftCompute.SetTexture(1, ShaderIDs.outputREID, outputRE);
            fftCompute.SetBool(ShaderIDs.inverseID, true);
            fftCompute.SetBool(ShaderIDs.horizontalID, true);
            fftCompute.Dispatch(1, 1, size, 1);

            fftCompute.SetBool(ShaderIDs.horizontalID, false);
            fftCompute.Dispatch(1, 1, size, 1);
        }

        /// <summary>
        /// Computes the Real-2D time domain transform of a 2D-Complex frequency domain function
        /// (zero-frequency elements should be in the center)
        /// </summary>
        /// <param name="cmb">CommandBuffer to queue dispatch calls</param>
        /// <param name="inputRE">Real part of the input function</param>
        /// <param name="inputIM">Imaginary part of the input function</param>   
        /// <param name="outputRE">Real part of the output function</param>
        public void InverseFFT2_C2R(CommandBuffer cmb, Texture inputRE, Texture inputIM, ref RenderTexture outputRE)
        {
            fftCompute.SetTexture(1, ShaderIDs.inputREID, inputRE);
            fftCompute.SetTexture(1, ShaderIDs.inputIMID, inputIM);
            fftCompute.SetTexture(1, ShaderIDs.outputREID, outputRE);
            cmb.SetComputeIntParam(fftCompute, ShaderIDs.inverseID, 1);
            cmb.SetComputeIntParam(fftCompute, ShaderIDs.horizontalID, 1);
            cmb.DispatchCompute(fftCompute, 1, 1, size, 1);
            cmb.SetComputeIntParam(fftCompute, ShaderIDs.horizontalID, 0);
            cmb.DispatchCompute(fftCompute, 1, 1, size, 1);
        }

        /// <summary>
        /// Computes the 2D-Real fourier transform of a 2D-Real function
        /// </summary>
        /// <param name="inputRE">Real part of the input function</param>
        /// <param name="outputRE">Real part of the output function</param>
        public void PowerSpectrum_R2R(Texture inputRE, ref RenderTexture outputRE)
        {
            fftCompute.SetTexture(2, ShaderIDs.inputREID, inputRE);
            fftCompute.SetTexture(2, ShaderIDs.outputREID, outputRE);
            fftCompute.SetBool(ShaderIDs.inverseID, false);
            fftCompute.SetBool(ShaderIDs.horizontalID, true);
            fftCompute.Dispatch(2, 1, size, 1);

            fftCompute.SetBool(ShaderIDs.horizontalID, false);
            fftCompute.Dispatch(2, 1, size, 1);
        }

        public static bool IsPowerOfTwo(uint x)
        {
            return (x & (x - 1)) == 0;
        }

        private int[] bitReverse(int n)
        {
            int k = (int)Mathf.Log(n, 2);
            int[] reversedIndices = new int[n];
            for (int i = 0; i < n; i++)
            {
                int x = i;
                int reversed = 0;
                for (int j = 0; j < k; j++)
                {
                    reversed |= (x & (1 << j)) != 0 ? 1 << (k - 1 - j) : 0;
                }
                reversedIndices[i] = reversed;
            }

            return reversedIndices;
        }

        public static void CreateRT(ref RenderTexture rt, GraphicsFormat format, FilterMode filterMode, int size)
        {
            rt = new RenderTexture(size, size, 0, format);
            rt.useMipMap = false;
            rt.enableRandomWrite = true;
            rt.wrapMode = TextureWrapMode.Repeat;
            rt.filterMode = filterMode;
            rt.Create();
        }

        public static void CreateRT(ref RenderTexture rt, GraphicsFormat format, FilterMode filterMode, int x, int y)
        {
            rt = new RenderTexture(x, y, 0, format);
            rt.useMipMap = false;
            rt.enableRandomWrite = true;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.filterMode = filterMode;
            rt.Create();
        }

        readonly private string[] keywords = { "FFT_64", "FFT_128", "FFT_256", "FFT_512", "FFT_1024" };
        readonly private string[] kernels = { "R2C", "C2R", "R2R" };
        private static class ShaderIDs
        {
            public static int inputREID = Shader.PropertyToID("inputRE");
            public static int inputIMID = Shader.PropertyToID("inputIM");
            public static int outputREID = Shader.PropertyToID("outputRE");
            public static int outputIMID = Shader.PropertyToID("outputIM");
            public static int horizontalID = Shader.PropertyToID("horizontal");
            public static int inverseID = Shader.PropertyToID("inverse");
        }

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {

                }
                fftCompute = null;

                firstStageTemp?.Dispose();
                disposedValue = true;
            }
        }

        /// <summary>
        /// Dispose the allocated resources
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            System.GC.SuppressFinalize(this);
        }

        private struct comp3
        {
            public readonly Vector2 r;
            public readonly Vector2 g;
            public readonly Vector2 b;

            public comp3(Vector2 r, Vector2 g, Vector2 b)
            {
                this.r = r;
                this.g = g;
                this.b = b;
            }
            public comp3(float r, float g, float b)
            {
                this.r = Vector2.right * r;
                this.g = Vector2.right * g;
                this.b = Vector2.right * b;
            }
            public comp3(Vector3 rgb)
            {
                this.r = Vector2.right * rgb.x;
                this.g = Vector2.right * rgb.y;
                this.b = Vector2.right * rgb.z;
            }
        }
    }
}