namespace AsciiFlow.Core.AsciiMapping;

/// <summary>
/// ASCII 字符映射器接口
/// </summary>
public interface IAsciiMapper
{
    /// <summary>
    /// 将灰度图像数据映射为 ASCII 字符字符串
    /// </summary>
    /// <param name="grayData">灰度数据数组（0-255）</param>
    /// <param name="width">图像宽度（像素）</param>
    /// <param name="height">图像高度（像素）</param>
    /// <param name="targetWidth">目标宽度（字符）</param>
    /// <param name="targetHeight">目标高度（字符）</param>
    /// <returns>ASCII 字符艺术字符串</returns>
    string MapToAscii(
        byte[] grayData,
        int width,
        int height,
        int targetWidth,
        int targetHeight);
}