using System.Diagnostics;
using System.Timers;
using Avalonia;
using Avalonia.Logging;
using Avalonia.OpenGL.Surfaces;
using SkiaSharp;
using SkiaSharp.SceneGraph;
using SkiaSharp.Skottie;
using Timer = System.Timers.Timer;

namespace AvaloniaInside.SplashScreen;

public class LottieSplashRenderer : IDisposable
{
    private readonly IGlPlatformSurface _iglPlatformSurface;
    private readonly object _sync = new();
    private readonly Stopwatch _watch = new();
    private Animation? _animation;
    private int _count;
    private InvalidationController? _ic;
    private bool _isRunning;
    private int _repeatCount = -1;
    private Timer? _timer;

    public LottieSplashRenderer(IGlPlatformSurface iglPlatformSurface)
    {
        _iglPlatformSurface = iglPlatformSurface;
    }

    public Size Size { get; set; }
    public float Scale { get; set; } = 1;
    public int RepeatCount { get; set; } = -1;

    public void Dispose()
    {
        DisposeImpl();
    }

    public void Load(Animation? animation)
    {
        if (animation is null)
        {
            DisposeImpl();
            return;
        }

        DisposeImpl();

        try
        {
            _repeatCount = RepeatCount;
            _animation = animation;
            Start();
        }
        catch (Exception e)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to load animation: " + e);
            _animation = null;
        }
    }

    private void Start()
    {
        if (_animation is null)
            return;

        if (_repeatCount == 0)
            return;

        _count = 0;
        _isRunning = true;
        _timer = new Timer
        {
            Interval = Math.Max(1 / 60.0, 1 / _animation.Fps) * 1000
        };
        _timer.Elapsed += Tick;
        _timer.Enabled = true;
        _timer.Start();

        _watch.Start();
    }

    private void Tick(object? sender, ElapsedEventArgs e)
    {
        if (_timer is null) return;

        if (_repeatCount == 0 || (_repeatCount > 0 && _count >= _repeatCount))
        {
            _isRunning = false;
            _timer.Stop();
            _watch.Stop();
            UpdateFrame();
        }

        if (_isRunning)
            UpdateFrame();
        else
            _timer.Stop();
    }

    private void UpdateFrame()
    {
        if (_animation is null) return;

        lock (_sync)
        {
            using var splashRender = new SplashRenderer(_iglPlatformSurface);
            Size = splashRender.Size;

            var viewPort = new Rect(Size);
            var sourceSize = new Size(_animation.Size.Width, _animation.Size.Height);
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0) return;

            var scaledSize = sourceSize * Scale;
            var destRect = viewPort
                .CenterRect(new Rect(scaledSize))
                .Intersect(viewPort);
            var sourceRect = new Rect(sourceSize)
                .CenterRect(new Rect(destRect.Size / Scale));

            var bounds = SKRect.Create(new SKPoint(), _animation.Size);
            var scaleMatrix = Matrix.CreateScale(
                destRect.Width / sourceRect.Width,
                destRect.Height / sourceRect.Height);

            splashRender.Canvas.SetMatrix(SKMatrix.CreateScale(
                (float)(destRect.Width / sourceRect.Width),
                (float)(destRect.Height / sourceRect.Height)));
            splashRender.Canvas.SetMatrix(SKMatrix.CreateTranslation(
                (float)(-sourceRect.X + destRect.X - bounds.Top),
                (float)(-sourceRect.Y + destRect.Y - bounds.Left)));

            Draw(splashRender.Canvas);
        }
    }

    private double GetFrameTime()
    {
        if (_animation is null || _timer is null) return 0f;

        var frameTime = _watch.Elapsed.TotalSeconds;

        if (_watch.Elapsed.TotalSeconds > _animation.Duration.TotalSeconds)
        {
            _watch.Restart();
            _ic?.End();
            _ic?.Begin();
            _count++;
        }

        return frameTime;
    }

    protected virtual void Draw(SKCanvas canvas)
    {
        var animation = _animation;
        if (animation is null)
            return;

        if (_ic is null)
        {
            _ic = new InvalidationController();
            _ic.Begin();
        }

        var ic = _ic;
        if (_repeatCount == 0) return;
        var t = GetFrameTime();
        if (!_isRunning)
            t = (float)animation.Duration.TotalSeconds;
        var dst = new SKRect(0, 0, animation.Size.Width, animation.Size.Height);
        animation.SeekFrameTime(t, ic);
        canvas.Save();
        animation.Render(canvas, dst);
        canvas.Restore();
        ic.Reset();
        canvas.Flush();
    }

    private void DisposeImpl()
    {
        lock (_sync)
        {
            Stop();
            _animation?.Dispose();
            _animation = null;
            _ic?.End();
            _ic?.Dispose();
            _ic = null;
        }
    }

    private void Stop()
    {
        _isRunning = false;
        _timer?.Stop();
        _timer = null;
        _watch.Reset();
        _count = 0;
    }
}