using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace AsciiFlow.Core.Video;

/// <summary>
/// 基于 FFmpeg.AutoGen 的高性能视频解码器
/// 支持 H.264/H.265/VP9 等主流视频格式
/// 修复版本：适配 FFmpeg.AutoGen 8.1.0 API
/// </summary>
public unsafe class FFmpegVideoDecoder : IVideoDecoder
{
    private AVFormatContext* _formatContext;
    private AVCodecContext* _codecContext;
    private AVFrame* _frame;
    private AVPacket* _packet;
    private SwsContext* _swsContext;

    private string? _videoPath;
    private int _width;
    private int _height;
    private double _frameRate;
    private long _frameCount;
    private long _currentFrame;
    private bool _initialized;
    private bool _disposed;
    private int _streamIndex = -1;

    private byte[]? _frameBuffer;
    private int _frameBufferSize;

    /// <summary>
    /// 获取视频宽度
    /// </summary>
    public int Width
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _width;
    }

    /// <summary>
    /// 获取视频高度
    /// </summary>
    public int Height
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _height;
    }

    /// <summary>
    /// 获取视频帧率（FPS）
    /// </summary>
    public double FrameRate
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _frameRate;
    }

    /// <summary>
    /// 获取视频总帧数
    /// </summary>
    public long FrameCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _frameCount;
    }

    /// <summary>
    /// 获取当前帧索引
    /// </summary>
    public long CurrentFrame
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _currentFrame;
    }

    /// <summary>
    /// 获取是否已初始化
    /// </summary>
    public bool IsInitialized
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _initialized;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public FFmpegVideoDecoder()
    {
        FFmpegInitializer.Initialize();
    }

    /// <summary>
    /// 初始化视频解码器
    /// </summary>
    /// <param name="videoPath">视频文件路径</param>
    public void Initialize(string videoPath)
    {
        if (_initialized)
        {
            throw new InvalidOperationException("解码器已初始化，请先调用 Reset() 方法重置");
        }

        if (string.IsNullOrEmpty(videoPath))
        {
            throw new ArgumentNullException(nameof(videoPath), "视频文件路径不能为空");
        }

        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException($"找不到视频文件：{videoPath}", videoPath);
        }

        try
        {
            _videoPath = videoPath;

            // 1. 打开视频文件
            AVFormatContext* formatContext = null;
            fixed (byte* pathBytes = System.Text.Encoding.UTF8.GetBytes(videoPath + "\0"))
            {
                int ret = ffmpeg.avformat_open_input(&formatContext, (sbyte*)pathBytes, null, null);
                if (ret < 0)
                {
                    throw new FFmpegDecoderException($"无法打开视频文件：{videoPath}", ret);
                }
            }

            _formatContext = formatContext;

            // 2. 查找流信息
            int findStreamRet = ffmpeg.avformat_find_stream_info(_formatContext, null);
            if (findStreamRet < 0)
            {
                throw new FFmpegDecoderException("无法获取视频流信息", findStreamRet);
            }

            // 3. 查找视频流
            _streamIndex = FindVideoStream();
            if (_streamIndex < 0)
            {
                throw new FFmpegDecoderException("未找到视频流");
            }

            AVStream* videoStream = _formatContext->streams[_streamIndex];
            AVCodecParameters* codecParams = videoStream->codecpar;

            // 4. 查找解码器
            AVCodec* codec = ffmpeg.avcodec_find_decoder(codecParams->codec_id);
            if (codec == null)
            {
                throw new FFmpegDecoderException($"找不到解码器，编解码器ID: {codecParams->codec_id}");
            }

            // 5. 分配解码器上下文
            _codecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (_codecContext == null)
            {
                throw new FFmpegDecoderException("无法分配解码器上下文");
            }

            // 6. 复制编解码器参数
            int copyParamsRet = ffmpeg.avcodec_parameters_to_context(_codecContext, codecParams);
            if (copyParamsRet < 0)
            {
                throw new FFmpegDecoderException("无法复制编解码器参数", copyParamsRet);
            }

            // 7. 打开解码器
            int openCodecRet = ffmpeg.avcodec_open2(_codecContext, codec, null);
            if (openCodecRet < 0)
            {
                throw new FFmpegDecoderException("无法打开解码器", openCodecRet);
            }

            // 8. 获取视频信息
            _width = _codecContext->width;
            _height = _codecContext->height;
            
            // 获取帧率 - 使用 AVRational 的正确访问方式
            if (videoStream->avg_frame_rate.den > 0)
            {
                _frameRate = (double)videoStream->avg_frame_rate.num / videoStream->avg_frame_rate.den;
            }
            else if (videoStream->r_frame_rate.den > 0)
            {
                _frameRate = (double)videoStream->r_frame_rate.num / videoStream->r_frame_rate.den;
            }
            else
            {
                _frameRate = 30.0; // 默认帧率
            }

            // 获取总帧数
            if (videoStream->nb_frames > 0)
            {
                _frameCount = videoStream->nb_frames;
            }
            else if (_formatContext->duration > 0 && _frameRate > 0)
            {
                _frameCount = (long)(_formatContext->duration * _frameRate / AV_TIME_BASE);
            }
            else
            {
                _frameCount = 0;
            }

            // 9. 分配帧和包
            _frame = ffmpeg.av_frame_alloc();
            if (_frame == null)
            {
                throw new FFmpegDecoderException("无法分配 AVFrame");
            }

            _packet = ffmpeg.av_packet_alloc();
            if (_packet == null)
            {
                throw new FFmpegDecoderException("无法分配 AVPacket");
            }

            // 10. 创建缩放上下文（用于像素格式转换）
            CreateSwsContext();

            // 11. 初始化帧缓冲区
            InitializeFrameBuffer();

            _currentFrame = 0;
            _initialized = true;

            Console.WriteLine($"[解码器] 初始化成功！视频信息：{_width}x{_height}, {_frameRate:F2} FPS, {_frameCount} 帧");
        }
        catch (Exception ex)
        {
            // 清理已分配的资源
            Cleanup();
            throw new FFmpegDecoderException($"初始化视频解码器失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 查找视频流
    /// </summary>
    private int FindVideoStream()
    {
        if (_formatContext == null)
            return -1;

        for (uint i = 0; i < _formatContext->nb_streams; i++)
        {
            AVStream* stream = _formatContext->streams[i];
            if (stream->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                return (int)i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 创建缩放上下文
    /// </summary>
    private void CreateSwsContext()
    {
        if (_codecContext == null || _frame == null)
            return;

        _swsContext = ffmpeg.sws_getContext(
            _width, _height,
            _codecContext->pix_fmt,
            _width, _height,
            AVPixelFormat.AV_PIX_FMT_RGB24,
            ffmpeg.SWS_BILINEAR,
            null, null, null);

        if (_swsContext == null)
        {
            throw new FFmpegDecoderException("无法创建 SWS 缩放上下文");
        }
    }

    /// <summary>
    /// 初始化帧缓冲区
    /// </summary>
    private void InitializeFrameBuffer()
    {
        _frameBufferSize = _width * _height * 3; // RGB24: 3 bytes per pixel
        _frameBuffer = new byte[_frameBufferSize];
    }

    /// <summary>
    /// 获取下一帧的原始像素数据（RGB24格式）
    /// </summary>
    /// <returns>像素数据数组，如果没有更多帧则返回 null</returns>
    public byte[]? GetNextFrame()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("解码器未初始化，请先调用 Initialize() 方法");
        }

        if (_frameBuffer == null)
        {
            throw new InvalidOperationException("帧缓冲区未初始化");
        }

        try
        {
            while (true)
            {
                // 1. 读取数据包
                int readRet = ffmpeg.av_read_frame(_formatContext, _packet);

                if (readRet == FFmpegInitializer.ErrorCode.EOF || readRet < 0)
                {
                    // 到达文件末尾，尝试刷新解码器
                    return FlushDecoder();
                }

                // 2. 检查是否是视频流的数据包
                if (_packet->stream_index == _streamIndex)
                {
                    // 3. 发送数据包到解码器
                    int sendRet = ffmpeg.avcodec_send_packet(_codecContext, _packet);
                    
                    // 释放包
                    ffmpeg.av_packet_unref(_packet);

                    // 处理 EAGAIN 错误
                    if (sendRet == FFmpegInitializer.ErrorCode.EAGAIN)
                    {
                        continue;
                    }

                    if (sendRet < 0)
                    {
                        throw new FFmpegDecoderException($"发送数据包到解码器失败", sendRet);
                    }

                    // 4. 从解码器接收帧
                    return ReceiveFrame();
                }

                // 释放非视频流的包
                ffmpeg.av_packet_unref(_packet);
            }
        }
        catch (Exception ex)
        {
            throw new FFmpegDecoderException($"解码视频帧失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 从解码器接收帧
    /// </summary>
    private byte[]? ReceiveFrame()
    {
        if (_frame == null || _frameBuffer == null)
            return null;

        int receiveRet = ffmpeg.avcodec_receive_frame(_codecContext, _frame);

        // 处理 EAGAIN 错误（需要更多输入）
        if (receiveRet == FFmpegInitializer.ErrorCode.EAGAIN)
        {
            return null;
        }

        // 检查 EOF
        if (receiveRet == FFmpegInitializer.ErrorCode.EOF || receiveRet < 0)
        {
            return null;
        }

        // 转换像素格式并复制到缓冲区
        CopyFrameToBuffer();

        _currentFrame++;
        return _frameBuffer;
    }

    /// <summary>
    /// 刷新解码器
    /// </summary>
    private byte[]? FlushDecoder()
    {
        if (_codecContext == null)
            return null;

        try
        {
            // 发送空包刷新解码器
            ffmpeg.avcodec_send_packet(_codecContext, null);

            // 尝试接收剩余帧
            return ReceiveFrame();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 复制帧数据到缓冲区（使用缩放上下文进行像素格式转换）
    /// </summary>
    private void CopyFrameToBuffer()
    {
        if (_frame == null || _frameBuffer == null || _swsContext == null)
            return;

        // 使用缩放上下文转换像素格式
        fixed (byte* bufferPtr = _frameBuffer)
        {
            // 创建目标数据的指针数组
            byte_ptrArray4 dstData = new byte_ptrArray4();
            dstData[0] = bufferPtr;

            int_array4 dstLinesize = new int_array4();
            dstLinesize[0] = _width * 3; // RGB24 每行字节数

            ffmpeg.sws_scale(
                _swsContext,
                _frame->data,
                _frame->linesize,
                0,
                _height,
                dstData,
                dstLinesize);
        }
    }

    /// <summary>
    /// 跳帧到指定位置
    /// </summary>
    /// <param name="frameNumber">目标帧号</param>
    public void SeekToFrame(long frameNumber)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("解码器未初始化，请先调用 Initialize() 方法");
        }

        if (_formatContext == null)
        {
            throw new InvalidOperationException("格式上下文为空");
        }

        if (frameNumber < 0 || (_frameCount > 0 && frameNumber >= _frameCount))
        {
            throw new ArgumentOutOfRangeException(nameof(frameNumber), 
                $"帧号 {frameNumber} 超出范围 [0, {_frameCount - 1}]");
        }

        try
        {
            // 计算时间戳
            long timestamp = CalculateTimestamp(frameNumber);

            // 执行跳转
            int seekRet = ffmpeg.av_seek_frame(
                _formatContext,
                _streamIndex,
                timestamp,
                ffmpeg.AVSEEK_FLAG_BACKWARD);

            if (seekRet < 0)
            {
                throw new FFmpegDecoderException($"跳转到帧 {frameNumber} 失败", seekRet);
            }

            // 刷新解码器缓冲
            ffmpeg.avcodec_flush_buffers(_codecContext);

            _currentFrame = frameNumber;
        }
        catch (Exception ex)
        {
            throw new FFmpegDecoderException($"跳转失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 计算时间戳
    /// </summary>
    private long CalculateTimestamp(long frameNumber)
    {
        if (_formatContext == null || _frameRate <= 0)
            return 0;

        // 使用 AV_TIME_BASE（通常为 1000000）
        const long AV_TIME_BASE = 1000000;
        
        // 计算时间戳
        double seconds = frameNumber / _frameRate;
        return (long)(seconds * AV_TIME_BASE);
    }

    /// <summary>
    /// 获取视频信息
    /// </summary>
    /// <returns></returns>
    public VideoInfo GetVideoInfo()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("解码器未初始化，请先调用 Initialize() 方法");
        }

        string codecName = _codecContext != null && _codecContext->codec != null
            ? Marshal.PtrToStringAnsi((IntPtr)_codecContext->codec->name) ?? "unknown"
            : "unknown";

        string pixelFormat = _codecContext != null
            ? ffmpeg.av_get_pix_fmt_name(_codecContext->pix_fmt).ToString()
            : "unknown";

        return new VideoInfo(_width, _height, _frameRate, _frameCount, codecName, pixelFormat);
    }

    /// <summary>
    /// 重置解码器到开始状态
    /// </summary>
    public void Reset()
    {
        if (_initialized)
        {
            Cleanup();
        }

        if (!string.IsNullOrEmpty(_videoPath))
        {
            Initialize(_videoPath);
        }
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    private void Cleanup()
    {
        // 释放帧
        if (_frame != null)
        {
            ffmpeg.av_frame_free(&_frame);
            _frame = null;
        }

        // 释放包
        if (_packet != null)
        {
            ffmpeg.av_packet_free(&_packet);
            _packet = null;
        }

        // 释放缩放上下文
        if (_swsContext != null)
        {
            ffmpeg.sws_freeContext(_swsContext);
            _swsContext = null;
        }

        // 释放解码器上下文
        if (_codecContext != null)
        {
            ffmpeg.avcodec_free_context(&_codecContext);
            _codecContext = null;
        }

        // 释放格式上下文
        if (_formatContext != null)
        {
            ffmpeg.avformat_close_input(&_formatContext);
            _formatContext = null;
        }

        _initialized = false;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源的虚方法
    /// </summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        Cleanup();

        if (disposing)
        {
            _frameBuffer = null;
            _videoPath = null;
        }

        _disposed = true;
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    ~FFmpegVideoDecoder()
    {
        Dispose(false);
    }

    // 常量定义
    private const long AV_TIME_BASE = 1000000;
}
