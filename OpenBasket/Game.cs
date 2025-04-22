
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenBasket;
using OpenBasket.Classes;
using OpenTK.Audio.OpenAL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using StbImageSharp;

namespace OpenBasket
{
    class Program
    {
        static void Main(string[] args)
        {
            using (Game game = new Game(600, 600))
            {
                game.Run();
            }
        }
    }

    class Shaders
    {
        public int shaderHandle;
        public void LoadShader()
        {
            shaderHandle = GL.CreateProgram();
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, LoadShaderSource("shader.vert"));
            GL.CompileShader(vertexShader);
            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, LoadShaderSource("shader.frag"));
            GL.CompileShader(fragmentShader);

            GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int success1);
            if (success1 == 0)
            {

                string infoLog = GL.GetShaderInfoLog(vertexShader);
                Console.WriteLine(infoLog);
            }
            GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out
            int success2);
            if (success2 == 0)
            {
                string infoLog = GL.GetShaderInfoLog(fragmentShader);
                Console.WriteLine(infoLog);
            }

            GL.AttachShader(shaderHandle, vertexShader);
            GL.AttachShader(shaderHandle, fragmentShader);
            GL.LinkProgram(shaderHandle);
        }
        public static string LoadShaderSource(string filepath)
        {
            string shaderSource = "";
            try
            {
                using (StreamReader reader = new StreamReader(
                    @"C:\Users\Zongys\source\repos\OpenBasket\OpenBasket\Shaders\" + filepath))
                {
                    shaderSource = reader.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to load shader source file: " + e.Message);
            }
            return shaderSource;
        }
        public void UseShader()
        {
            GL.UseProgram(shaderHandle);
        }
        public void DeleteShader()
        {
            GL.DeleteProgram(shaderHandle);
        }

    }

    internal class Game : GameWindow
    {
        int width, height;


        private Vector3 handOffset = new Vector3(0.5f, -0.5f, 1f);
        private bool isBallThrown = false;
        private Vector3 ballVelocity = Vector3.Zero;
        private float gravity = -9.81f;
        private float throwForce = 20f;

        int modelLocation;
        int viewLocation;
        int projectionLocation;

        
        Shaders shaderProgram = new Shaders();
        Camera camera;
        Ring ring = new Ring();
        Floor floor = new Floor();
        Ball ball = new Ball();
        Vector3 ballPosition = new Vector3(0f, 0.5f, -3f);
        public Game(int width, int height) : base(GameWindowSettings.Default, NativeWindowSettings.Default)
        {
            this.CenterWindow(new Vector2i(width, height));
            this.height = height;
            this.width = width;
            throwForce = 15f;
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            ring.RingLoad();
            floor.FloorLoad();
            ball.BallLoad();
            shaderProgram.LoadShader();

            camera = new Camera(width, height, Vector3.Zero);
            CursorState = CursorState.Grabbed;
            GL.Enable(EnableCap.DepthTest);

        }
        
        

        protected override void OnUnload()
        {
            ring.RingUnload();
            floor.FloorUnload();
            ball.BallUnload();
            shaderProgram.DeleteShader();
           

            base.OnUnload();
        }
        protected override void OnRenderFrame(FrameEventArgs args)
        {   
            GL.ClearColor(0.3f, 0.3f, 1f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            shaderProgram.UseShader();
           
            RingDraw();
            DrawFloor();
            DrawBall();
            Context.SwapBuffers();
            base.OnRenderFrame(args);
        }
        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            if (KeyboardState.IsKeyDown(Keys.Escape))
            {
                Close();
            }
            MouseState mouse = MouseState;
            KeyboardState input = KeyboardState;

            base.OnUpdateFrame(args);
            camera.Update(input, mouse, args);

            if (mouse.IsButtonDown(MouseButton.Left) && !isBallThrown)
            {
                ballVelocity = camera.Front * throwForce;
                isBallThrown = true;
            }

            // Возврат мяча
            if (mouse.IsButtonDown(MouseButton.Right))
            {
                isBallThrown = false;
                ballPosition = CalculateHandPosition();
            }

            // Физика мяча
            if (isBallThrown)
            {
                ballVelocity.Y += gravity * (float)args.Time;
                ballPosition += ballVelocity * (float)args.Time;

                if (ballPosition.Y < -10f)
                {
                    isBallThrown = false;
                    ballPosition = CalculateHandPosition();
                }
            }


        }
        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, e.Width, e.Height);
            this.width = e.Width;
            this.height = e.Height;
        }

        protected void RingDraw()
        {
            ring.RingBind();
            
            Matrix4 ringtranslation = Matrix4.CreateTranslation(0f, 2.5f, -5f); 
            Matrix4 ringmodel = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(90f));
            Matrix4 ringsize = Matrix4.CreateScale(4f);
            ringmodel = ringsize * ringtranslation * ringmodel;

            int ringmodelLocation = GL.GetUniformLocation(shaderProgram.shaderHandle, "model");
            int ringviewLocation = GL.GetUniformLocation(shaderProgram.shaderHandle, "view");
            int ringprojectionLocation = GL.GetUniformLocation(shaderProgram.shaderHandle, "projection");

            Matrix4 bottomRingCircle2View = camera.GetViewMatrix();
            Matrix4 bottomRingCircle2Projection = camera.GetProjection();

            GL.UniformMatrix4(ringmodelLocation, true, ref ringmodel);
            GL.UniformMatrix4(ringviewLocation, true, ref bottomRingCircle2View);
            GL.UniformMatrix4(ringprojectionLocation, true, ref bottomRingCircle2Projection);

            GL.DrawElements(PrimitiveType.Triangles,
                           ring.ringIndices.Length,
                           DrawElementsType.UnsignedInt, 0);
        }
        protected void DrawFloor()
        {
            floor.FloorBind();
            Matrix4 floormodel = Matrix4.Identity;
            Matrix4 floorview = camera.GetViewMatrix();
            Matrix4 floorprojection = camera.GetProjection();
            Matrix4 floortranslation = Matrix4.CreateTranslation(0f, -0.5f, 0f);

            int floormodelLocation = GL.GetUniformLocation(shaderProgram.shaderHandle, "model");
            int floorviewLocation = GL.GetUniformLocation(shaderProgram.shaderHandle, "view");
            int floorprojectionLocation = GL.GetUniformLocation(shaderProgram.shaderHandle, "projection");

            // Комбинируем трансформации
            floormodel *= floortranslation;

            GL.UniformMatrix4(floormodelLocation, true, ref floormodel);
            GL.UniformMatrix4(floorviewLocation, true, ref floorview);
            GL.UniformMatrix4(floorprojectionLocation, true, ref floorprojection);

            GL.BindVertexArray(floor.floorVAO);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, floor.floorEBO);
            GL.DrawElements(PrimitiveType.Triangles,
                           floor.floorIndices.Length,
                           DrawElementsType.UnsignedInt, 0);
        }
        private Vector3 CalculateHandPosition()
        {
            return camera.position
                + camera.Right * handOffset.X
                + camera.Up * handOffset.Y
                + camera.Front * handOffset.Z;
        }
        protected void DrawBall()
        {
            ball.BallBind();

            Vector3 currentBallPos = isBallThrown ?
                ballPosition :
                CalculateHandPosition();

            Matrix4 model = Matrix4.CreateTranslation(currentBallPos);
            
            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjection();

            int modelLoc = GL.GetUniformLocation(shaderProgram.shaderHandle, "model");
            int viewLoc = GL.GetUniformLocation(shaderProgram.shaderHandle, "view");
            int projLoc = GL.GetUniformLocation(shaderProgram.shaderHandle, "projection");

            GL.UniformMatrix4(modelLoc, true, ref model);
            GL.UniformMatrix4(viewLoc, true, ref view);
            GL.UniformMatrix4(projLoc, true, ref projection);

            GL.DrawElements(PrimitiveType.Triangles,
                ball.ballindices.Length,
                DrawElementsType.UnsignedInt, 0);
        }
    }
}



