﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Core;
using Core.Camera;
using Core.FX;
using Core.Model;
using SlimDX;
using SlimDX.Direct3D11;
using PresentFlags = SlimDX.DXGI.PresentFlags;

namespace HemisphericalAmbient {
    partial class HemisphericalAmbientDemo : D3DApp {
        private TextureManager _texMgr;
        private BasicModel _bunnyModel;
        private BasicModelInstance _bunnyInstance;

        private readonly LookAtCamera _camera;
        private Point _lastMousePos;
        private bool _disposed;

        private ForwardLightingEffect _effect;
        private InputLayout _layout;
        private Vector3 _ambientLower = new Vector3(0.1f, 0.5f, 0.1f);
        private Vector3 _ambientUpper = new Vector3(0.1f, 0.2f, 0.5f);
        


        private static Vector3 GammaToLinear(Vector3 c) {
            return new Vector3(c.X * c.X, c.Y * c.Y, c.Z * c.Z);
        }

        private HemisphericalAmbientDemo(IntPtr hInstance)
            : base(hInstance) {
            MainWindowCaption = "Hemispherical Ambient Light Demo";
            _lastMousePos = new Point();
            //Enable4XMsaa = true;

            _camera = new LookAtCamera() {
                Target = new Vector3(0, 3, 0),
                Position = new Vector3(0, 2, -15)
            };

            GammaCorrectedBackBuffer = true;
        }

        protected override void Dispose(bool disposing) {
            if (!_disposed) {
                if (disposing) {
                    Util.ReleaseCom(ref _bunnyModel);
                    Util.ReleaseCom(ref _texMgr);

                    Effects.DestroyAll();
                    InputLayouts.DestroyAll();
                    RenderStates.DestroyAll();

                    Util.ReleaseCom(ref _effect);
                    Util.ReleaseCom(ref _layout);
                }
                _disposed = true;
            }
            base.Dispose(disposing);

        }

        public override bool Init() {
            
            if (!base.Init()) return false;

            Effects.InitAll(Device);
            InputLayouts.InitAll(Device);
            RenderStates.InitAll(Device);

            _texMgr = new TextureManager();
            _texMgr.Init(Device);

            _bunnyModel = BasicModel.LoadSdkMesh(Device, _texMgr, "Models/bunny.sdkmesh", "Textures");

            _bunnyInstance = new BasicModelInstance(_bunnyModel) {
                World = Matrix.Scaling(0.1f, 0.1f, 0.1f)
            };

            _effect = new ForwardLightingEffect(Device, "FX/forwardLight.fxo");

            var passDesc = _effect.Ambient.GetPassByIndex(0).Description;
            _layout = new InputLayout(Device, passDesc.Signature, InputLayoutDescriptions.PosNormalTexTan);

            AddUIElements();

            return true;

        }



        public override void OnResize() {
            base.OnResize();
            _camera.SetLens(0.25f * MathF.PI, AspectRatio, 1.0f, 1000.0f);
        }
        public override void UpdateScene(float dt) {
            base.UpdateScene(dt);
            if (Util.IsKeyDown(Keys.Up)) {
                _camera.Walk(10.0f * dt);
            }
            if (Util.IsKeyDown(Keys.Down)) {
                _camera.Walk(-10.0f * dt);
            }
            if (Util.IsKeyDown(Keys.Left)) {
                _camera.Strafe(-10.0f * dt);
            }
            if (Util.IsKeyDown(Keys.Right)) {
                _camera.Strafe(10.0f * dt);
            }
            if (Util.IsKeyDown(Keys.PageUp)) {
                _camera.Zoom(-dt);
            }
            if (Util.IsKeyDown(Keys.PageDown)) {
                _camera.Zoom(+dt);
            }

        }
        public override void DrawScene() {
            base.DrawScene();
            ImmediateContext.ClearRenderTargetView(RenderTargetView, Color.Black);
            ImmediateContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

            ImmediateContext.InputAssembler.InputLayout = _layout;
            ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

            _camera.UpdateViewMatrix();

            var view = _camera.View;
            var proj = _camera.Proj;

            SetupAmbient();

            var activeTech = _effect.Ambient;
            for (var p = 0; p < activeTech.Description.PassCount; p++) {
                var pass = activeTech.GetPassByIndex(p);
                _bunnyInstance.Draw(ImmediateContext, pass, view, proj, DrawAmbient);
            }

            SwapChain.Present(0, PresentFlags.None);
        }

        private void SetupAmbient() {
            _effect.SetAmbientDown(GammaToLinear(_ambientLower));//lowerGamma);
            _effect.SetAmbientRange(GammaToLinear(_ambientUpper) - GammaToLinear(_ambientLower)); //range);
        }

        private void DrawAmbient(DeviceContext dc, EffectPass pass, Matrix view, Matrix proj) {
            var model = _bunnyInstance.Model;
            var world = _bunnyInstance.World;
            var wit = MathF.InverseTranspose(world);

            _effect.SetWorld(world);
            _effect.SetWorldViewProj(world * view * proj);
            _effect.SetWorldInvTranspose(wit);

            for (var i = 0; i < model.SubsetCount; i++) {
                _effect.SetDiffuseMap(model.DiffuseMapSRV[i]);
                pass.Apply(ImmediateContext);
                model.ModelMesh.Draw(ImmediateContext, i);
            }
        }

        protected override void OnMouseDown(object sender, MouseEventArgs mouseEventArgs) {
            _lastMousePos = mouseEventArgs.Location;
            Window.Capture = true;
        }
        protected override void OnMouseUp(object sender, MouseEventArgs e) {
            Window.Capture = false;
        }
        protected override void OnMouseMove(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                var dx = MathF.ToRadians(0.25f * (e.X - _lastMousePos.X));
                var dy = MathF.ToRadians(0.25f * (e.Y - _lastMousePos.Y));

                _camera.Pitch(dy);
                _camera.Yaw(dx);

            }
            _lastMousePos = e.Location;
        }

        static void Main() {
            Configuration.EnableObjectTracking = true;
            var app = new HemisphericalAmbientDemo(Process.GetCurrentProcess().Handle);
            if (!app.Init()) {
                return;
            }
            app.Run();
        }



    }
}
