using System;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using SkiaSharp;

namespace SkiaSharpTextures
{
    public sealed class MainWindow : GameWindow
    {
        private GRContext context;

        private int textureId;
        private GRBackendTextureDesc textureDesc;
        private SKSurface textureSurface;

        private GRBackendRenderTargetDesc renderDesc;
        private SKSurface renderSurface;

        private bool previousState;

        private int textureIdDirect;
        private int shaderProgram;
        private static readonly Vector2 _bottomLeft = new Vector2(0.0f, 1.0f);
        private static readonly Vector2 _bottomRight = new Vector2(1.0f, 1.0f);
        private static readonly Vector2 _topRight = new Vector2(1.0f, 0.0f);
        private static readonly Vector2 _topLeft = new Vector2(0.0f, 0.0f);

        private static readonly short[] _quadIndices = {3,0,1,  // first triangle (top left - bottom left - bottom right)
                                                       3,1,2}; // second triangle (top left - bottom right - top right)
        private readonly GLVertex[] _quad = new GLVertex[4];

        public MainWindow()
            : base(1280, 720)
        {
            Title = $"SkiaSharp Textures - OpenGL {GL.GetString(StringName.Version)}";

            Load += OnLoad;
            Resize += OnResize;
            Unload += OnUnload;
            UpdateFrame += OnUpdateFrame;
            RenderFrame += OnRenderFrame;
        }

        private void OnResize(object sender, EventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);
        }

        private void OnLoad(object sender, EventArgs e)
        {
            CursorVisible = true;

            PrepareSkia();
            PrepareDirect();
        }

        private void OnUnload(object sender, EventArgs e)
        {
            textureSurface.Dispose();
            textureSurface = null;

            renderSurface.Dispose();
            renderSurface = null;

            context.Dispose();
            context = null;
        }

        private void OnUpdateFrame(object sender, FrameEventArgs e)
        {
            var currentState = Mouse[MouseButton.Left];
            if (previousState != currentState)
            {
                UpdateTexture(currentState);
            }
            previousState = currentState;
        }

        private void OnRenderFrame(object sender, FrameEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            DrawSkia(e);

            DrawDirect();

            SwapBuffers();
        }

        private void PrepareSkia()
        {
            // CONTEXT

            // create the SkiaSharp context
            var glInterface = GRGlInterface.CreateNativeGlInterface();
            context = GRContext.Create(GRBackend.OpenGL, glInterface);

            // TEXTURE

            // the texture size
            var textureSize = new SKSizeI(256, 256);

            // create the OpenGL texture
            textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, textureId);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, textureSize.Width, textureSize.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            // create the SkiaSharp texture description
            var textureInfo = new GRGlTextureInfo
            {
                Id = (uint)textureId,
                Target = (uint)TextureTarget.Texture2D
            };
            var textureHandle = GCHandle.Alloc(textureInfo, GCHandleType.Pinned);
            textureDesc = new GRBackendTextureDesc
            {
                Width = textureSize.Width,
                Height = textureSize.Height,
                Config = GRPixelConfig.Rgba8888,
                Flags = GRBackendTextureDescFlags.RenderTarget,
                Origin = GRSurfaceOrigin.TopLeft,
                SampleCount = 0,
                TextureHandle = textureHandle.AddrOfPinnedObject(),
            };

            // create the SkiaSharp texture surface
            textureSurface = SKSurface.CreateAsRenderTarget(context, textureDesc);

            // free the pinned GC handle when we are done
            textureHandle.Free();

            // initialize the texture content
            UpdateTexture(false);

            // RENDER TARGET

            // create the SkiaSharp render target description
            renderDesc = new GRBackendRenderTargetDesc
            {
                RenderTargetHandle = (IntPtr)0,
                Width = Width,
                Height = Height,
                Config = GRPixelConfig.Rgba8888,
                Origin = GRSurfaceOrigin.TopLeft,
                SampleCount = 0,
                StencilBits = 0,
            };

            // create the SkiaSharp render target surface
            renderSurface = SKSurface.Create(context, renderDesc);
        }

        private void PrepareDirect()
        {
            GL.ClearColor(0f, 0f, 0f, 1f);
            CheckErrors();
            GL.Enable(EnableCap.Blend);
            CheckErrors();
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            CheckErrors();

            GL.Enable(EnableCap.Texture2D);
            CheckErrors();
            GL.EnableClientState(ArrayCap.VertexArray);
            CheckErrors();
            GL.EnableClientState(ArrayCap.TextureCoordArray);
            CheckErrors();
            GL.EnableClientState(ArrayCap.ColorArray);
            CheckErrors();
            GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);
            CheckErrors();

            textureIdDirect = GL.GenTexture();
            CheckErrors();
            GL.BindTexture(TextureTarget.Texture2D, textureIdDirect);
            CheckErrors();
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            CheckErrors();
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            CheckErrors();
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            CheckErrors();
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            CheckErrors();

            SKBitmap bitmap = new SKBitmap(100, 100);
            for (int x = 0; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    bitmap.SetPixel(x, y, SKColors.White);
                }
            }

            bitmap.LockPixels();

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bitmap.Width, bitmap.Height, 
                          0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bitmap.GetAddr(0,0));
            CheckErrors();
            bitmap.UnlockPixels();

            shaderProgram = GL.CreateProgram();
            CheckErrors();

            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            CheckErrors();
            GL.ShaderSource(vertexShader, GetStandardVertexShader());
            CheckErrors();
            GL.CompileShader(vertexShader);
            CheckErrors();
            GL.AttachShader(shaderProgram, vertexShader);
            CheckErrors();
            GL.DeleteShader(vertexShader);
            CheckErrors();

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            CheckErrors();
            GL.ShaderSource(fragmentShader, GetStandardFragmentShader());
            CheckErrors();
            GL.CompileShader(fragmentShader);
            CheckErrors();
            GL.AttachShader(shaderProgram, fragmentShader);
            CheckErrors();
            GL.DeleteShader(fragmentShader);
            CheckErrors();

            GL.LinkProgram(shaderProgram);
            CheckErrors();

            var buf1 = GL.GenBuffer();
            CheckErrors();
            GL.BindBuffer(BufferTarget.ArrayBuffer, buf1);
            CheckErrors();
            GL.VertexPointer(2, VertexPointerType.Float, GLVertex.Size, 0);
            CheckErrors();
            GL.TexCoordPointer(2, TexCoordPointerType.Float, GLVertex.Size, Vector2.SizeInBytes);
            CheckErrors();
            GL.ColorPointer(4, ColorPointerType.Float, GLVertex.Size, Vector2.SizeInBytes * 2);
            CheckErrors();
            var buf2 = GL.GenBuffer();
            CheckErrors();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, buf2);
            CheckErrors();
        }

        private void DrawSkia(FrameEventArgs e)
        {
            var renderCanvas = renderSurface.Canvas;

            renderCanvas.Clear(SKColors.CornflowerBlue);

            using (var paint = new SKPaint { IsAntialias = true, TextSize = 100, TextAlign = SKTextAlign.Center })
            {
                renderCanvas.DrawText("Hello World!", renderDesc.Width / 2, 150, paint);
            }
            using (var paint = new SKPaint { IsAntialias = true, TextSize = 24, Typeface = SKTypeface.FromFamilyName(null, SKTypefaceStyle.Italic) })
            {
                renderCanvas.DrawText($"V-Sync: {VSync}", 16, 16 + paint.TextSize, paint);
                renderCanvas.DrawText($"FPS: {1f / e.Time:0}", 16, 16 + paint.TextSize + 8 + paint.TextSize, paint);
            }

            renderCanvas.DrawSurface(textureSurface, (renderDesc.Width - textureDesc.Width) / 2, 200);

            context.Flush();
        }

        private void DrawDirect()
        {
            CheckErrors();
            GL.MatrixMode(MatrixMode.Projection);
            CheckErrors();
            GL.LoadIdentity();
            CheckErrors();
            GL.Ortho(0, 1200, 0, 800, -1, 1);
            CheckErrors();
            GL.MatrixMode(MatrixMode.Modelview);
            CheckErrors();
            GL.LoadIdentity();
            CheckErrors();
            GL.UseProgram(shaderProgram);
            CheckErrors();

            _quad[0] = new GLVertex(new Vector2(500,200), _bottomLeft, SKColors.Blue);
            _quad[1] = new GLVertex(new Vector2(900,200), _bottomRight, SKColors.Blue);
            _quad[2] = new GLVertex(new Vector2(900, 500), _topRight, SKColors.Blue);
            _quad[3] = new GLVertex(new Vector2(500, 500), _topLeft, SKColors.Blue);

            GL.BindTexture(TextureTarget.Texture2D, textureIdDirect);
            CheckErrors();

            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(GLVertex.Size * _quad.Length),
                          _quad, BufferUsageHint.StreamDraw);
            CheckErrors();

            GL.VertexPointer(2, VertexPointerType.Float, GLVertex.Size, 0);
            CheckErrors();
            GL.TexCoordPointer(2, TexCoordPointerType.Float, GLVertex.Size, Vector2.SizeInBytes);
            CheckErrors();
            GL.ColorPointer(4, ColorPointerType.Float, GLVertex.Size, Vector2.SizeInBytes * 2);
            CheckErrors();

            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(short) * _quadIndices.Length),
                          _quadIndices, BufferUsageHint.StreamDraw);
            CheckErrors();
            
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedShort, IntPtr.Zero);
            CheckErrors();

            GL.UseProgram(0);
            CheckErrors();
        }

        private void CheckErrors()
        {
            var error = GL.GetError();
            if (error != ErrorCode.NoError)
            {
                throw new Exception(error.ToString());
            }
        }

		private void UpdateTexture(bool isDown)
		{
			var textureCanvas = textureSurface.Canvas;

			textureCanvas.Clear(SKColors.SeaGreen);

			using (var paint = new SKPaint { IsAntialias = true, TextSize = 32, TextAlign = SKTextAlign.Center })
			{
				var y = (textureDesc.Height + paint.TextSize) / 2;
				textureCanvas.DrawText("Texture!", textureDesc.Width / 2, y, paint);

				paint.Typeface = SKTypeface.FromFamilyName(null, SKTypefaceStyle.Italic);
				textureCanvas.DrawText(isDown ? "(mouse down)" : "(try clicking)", textureDesc.Width / 2, y + paint.TextSize + 8, paint);
			}

			context.Flush();
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct GRGlTextureInfo
		{
			public uint Target;
			public uint Id;
		}

        public struct GLVertex
        {
            private readonly Vector2 _position, _texCoord;
            private readonly Vector4 _color;

            public GLVertex(Vector2 position, Vector2 texCoord, SKColor color)
                : this(position, texCoord, color.Red / 255f, color.Green / 255f, color.Blue / 255f, color.Alpha / 255f)
            {
            }

            public GLVertex(Vector2 position, Vector2 texCoord, float r, float g, float b, float a)
                : this(position, texCoord, new Vector4(r, g, b, a))
            {
            }

            public GLVertex(Vector2 position, Vector2 texCoord, Vector4 color)
            {
                _position = position;
                _texCoord = texCoord;
                _color = color;
            }

            static GLVertex()
            {
                unsafe
                {
                    Size = sizeof(GLVertex);
                }
            }

            public static int Size;

            public Vector2 Position => _position;
            public Vector2 TexCoord => _texCoord;
            public Vector4 Color => _color;
        }

        private string GetStandardVertexShader()
        {
            return @"#version 120
            varying vec4 gl_FrontColor;
            void main(void)
            {
                gl_FrontColor = gl_Color;
                gl_TexCoord[0] = gl_MultiTexCoord0;
                gl_Position = ftransform();
            }
            ";
        }

        private string GetStandardFragmentShader()
        {
            return @"#version 120
            uniform sampler2D texture;
            varying vec4 gl_Color;
            void main()
            {
                vec2 pos = gl_TexCoord[0].xy;
                vec4 col = texture2D(texture, pos);
                gl_FragColor = col * gl_Color;
            }";
        }

	}
}
