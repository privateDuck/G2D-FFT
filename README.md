# G2D-FFT
A Unity Based GPU-Accelerated 2D-FFT Library

# Uses
* Frequency Domain Image Processing
* Convolutional Bloom
* Numerical Computations

# Usage
1. Initialize an instance of the **GFFT** class and pass the **PD_Radix_2.compute** ComputeShader as the parameter
2. Configure the GFFT instance for the size of the image you're processing and the number of color channels in your image
3. Call the relavant methods to perform the fourier transform

* There are 3 types of transforms available
  * Real to Complex Forward Transform
  * Complex to Real Inverse Transform
  * Real to Real Forward Transform (Power Spectrum)
  
# Examples
  ## Basic Use
  ```c#
  // Init
  // computeShader => Compute Shader provided with this library
  GFFT gfft = new GFFT(computeShader);
  
  // size of the input image
  int size = 256;
  // number of color channels in the input image
  GFFT.Mode mode = GFFT.Mode.MultiChannel;
  
  gfft.ConfigureForSize(size, mode);
  
  // Real to Complex
  gfft.ForwardFFT2_R2C(input_re, ref output_re, ref output_im);
  // Complex to Real
  gfft.InverseFFT2_C2R(input_re, input_im, ref output_re);
  // Real to Real Forward
  gfft.PowerSpectrum_R2R(input_re, ref output_re);
  
  // input_re and input_im are of the type UnityEngine.Texture
  // output_re and output_im are of the type UnityEngine.RenderTexture
  ```
## Extras
  * The provided helper method **GFFT.CreateRT(ref RenderTexture rt, GraphicsFormat format, FilterMode filterMode, int size)** should be used to create RenderTextures     for the output of the Fourier Transforms.
  * To create RenderTextures, GFFT provides 4 graphics formats
    * GFFT.SingleChannelHalfPrecision - Single Channel, 16 bits per texel
    * GFFT.MultiChannelHalfPrecision - RGBA Channels, 16 bits per channel, 64 bits per texel
    * GFFT.SingleChannelSinglePrecision - Single Channel, 32 bits per texel
    * GFFT.MultiChannelSinglePrecision - RGBA Channels, 32 bits per channel, 128 bits per texel
    
  * Use of single precision floats has a significantly higher cost than the use of half precision floats
  * Use single precision textures, only if it's absolutely required. Otherwise stick with half precision textures
  
  ```c#
  // output rt
  RenderTexture output;
  // format of the texture
  var format = GFFT.MultiChannelHalfPrecision;
  
  GFFT.CreateRT(ref output, format, FilterMode.Point, size);
  ```
