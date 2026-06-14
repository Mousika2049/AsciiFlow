namespace AsciiFlow.Core.Video;

/// <summary>
/// 视频信息类
/// </summary>
public class VideoInfo
{
    /// <summary>视频宽度</summary>
    public int Width { get; set; }

    /// <summary>视频高度</summary>
    public int Height { get; set; }

    /// <summary>视频帧率（FPS）</summary>
    public double FrameRate { get; set; }

    /// <summary>视频总帧数</summary>
    public long FrameCount { get; set; }

    /// <summary>视频时长（秒）</summary>
    public double DurationSeconds { get; set; }

    /// <summary>视频编码格式</summary>
    public string CodecName { get; set; } = string.Empty;

    /// <summary>像素格式</summary>
    public string PixelFormat { get; set; } = string.Empty;

    /// <summary>
    /// 获取视频分辨率字符串
    /// </summary>
    public string Resolution => $"{Width}x{Height}";

    /// <summary>
    /// 创建视频信息实例
    /// </summary>
    public VideoInfo() { }

    /// <summary>
    /// 创建视频信息实例
    /// </summary>
    public VideoInfo(int width, int height, double frameRate, long frameCount, string codecName = "", string pixelFormat = "")
    {
        Width = width;
        Height = height;
        FrameRate = frameRate;
        FrameCount = frameCount;
        CodecName = codecName;
        PixelFormat = pixelFormat;
        DurationSeconds = frameRate > 0 && frameCount > 0 ? frameCount / frameRate : 0;
    }

    /// <summary>
    /// 重写 ToString 方法
    /// </summary>
    public override string ToString()
    {
        return $"Video: {Resolution}, {FrameRate:F2} FPS, {DurationSeconds:F2}s, {FrameCount} frames, {CodecName}";
    }
}
