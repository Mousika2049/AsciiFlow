namespace AsciiFlow.Core.Video;

/// <summary>
/// 视频解码器接口
/// </summary>
public interface IVideoDecoder : IDisposable
{
    /// <summary>视频宽度</summary>
    int Width { get; }

    /// <summary>视频高度</summary>
    int Height { get; }

    /// <summary>视频帧率（FPS）</summary>
    double FrameRate { get; }

    /// <summary>视频总帧数</summary>
    long FrameCount { get; }

    /// <summary>当前帧索引</summary>
    long CurrentFrame { get; }

    /// <summary>是否已初始化</summary>
    bool IsInitialized { get; }

    /// <summary>
    /// 初始化解码器
    /// </summary>
    /// <param name="videoPath">视频文件路径</param>
    void Initialize(string videoPath);

    /// <summary>
    /// 获取下一帧的原始像素数据
    /// </summary>
    /// <returns>像素数据数组（RGB24格式），如果没有更多帧则返回 null</returns>
    byte[]? GetNextFrame();

    /// <summary>
    /// 跳帧到指定位置
    /// </summary>
    /// <param name="frameNumber">目标帧号</param>
    void SeekToFrame(long frameNumber);

    /// <summary>
    /// 获取视频信息
    /// </summary>
    /// <returns>视频信息对象</returns>
    VideoInfo GetVideoInfo();

    /// <summary>
    /// 重置解码器到开始状态
    /// </summary>
    void Reset();
}
