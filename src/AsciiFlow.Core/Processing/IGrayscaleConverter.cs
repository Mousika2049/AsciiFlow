using System;

namespace AsciiFlow.Core.Processing;

/// <summary>
/// 灰度转换器接口
/// 将 RGB 视频帧转换为灰度图像
/// </summary>
public interface IGrayscaleConverter
{
    /// <summary>
    /// 将 RGB 数据转换为灰度数据
    /// </summary>
    /// <param name="rgbData">RGB 数据数组（格式：R,G,B,R,G,B...）</param>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <returns>灰度数据数组（每个字节表示一个像素的灰度值 0-255）</returns>
    byte[] ConvertToGrayscale(byte[] rgbData, int width, int height);
}