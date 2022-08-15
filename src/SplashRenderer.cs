using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Surfaces;
using SkiaSharp;

namespace AvaloniaInside.SplashScreen;

internal sealed class SplashRenderer : IDisposable
{
    private readonly IGlPlatformSurface _iglPlatformSurface;
    private IGlPlatformSurfaceRenderingSession? _draw;
    private GRContext? _grContext;
    private GRGlInterface? _iFace;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;
    private IGlPlatformSurfaceRenderTarget? _target;

    public SplashRenderer(IGlPlatformSurface drmOutput)
    {
        _iglPlatformSurface = drmOutput;
        Initialise();
    }

    public SKCanvas Canvas => _surface?.Canvas ?? throw new Exception("Cannot init splash");

    public Size Size => _draw != null ? new Size(_draw.Size.Width, _draw.Size.Height) : new Size(0, 0);

    public void Dispose()
    {
        _surface?.Canvas?.Dispose();
        _surface?.Dispose();
        _renderTarget?.Dispose();
        _grContext?.Dispose();
        _iFace?.Dispose();
        _draw?.Dispose();
        _target?.Dispose();
    }

    private void Initialise()
    {
        _target = _iglPlatformSurface.CreateGlRenderTarget();
        _draw = _target.BeginDraw();

        var context = _draw.Context;
        context.GlInterface.Clear(GlConsts.GL_COLOR_BUFFER_BIT | GlConsts.GL_STENCIL_BUFFER_BIT);

        _iFace = context.Version.Type == GlProfileType.OpenGL
            ? GRGlInterface.CreateOpenGl(proc => context.GlInterface.GetProcAddress(proc))
            : GRGlInterface.CreateGles(proc => context.GlInterface.GetProcAddress(proc));
        _grContext = GRContext.CreateGl(
            _iFace,
            new GRContextOptions { AvoidStencilBuffers = true });

        var size = _draw.Size;
        var drawContext = _draw.Context;
        drawContext.GlInterface.GetIntegerv(GlConsts.GL_FRAMEBUFFER_BINDING, out var fb);

        _renderTarget =
            new GRBackendRenderTarget(size.Width, size.Height, drawContext.SampleCount, drawContext.StencilSize,
                new GRGlFramebufferInfo((uint)fb, SKColorType.Rgba8888.ToGlSizedFormat()));

        _surface = SKSurface.Create(
            _grContext,
            _renderTarget,
            _draw.IsYFlipped ? GRSurfaceOrigin.TopLeft : GRSurfaceOrigin.BottomLeft,
            SKColorType.Rgba8888);
    }
}