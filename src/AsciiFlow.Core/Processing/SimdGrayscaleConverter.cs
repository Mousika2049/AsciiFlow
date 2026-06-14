using System;
using System.Numerics;

namespace AsciiFlow.Core.Processing;

/// <summary>
/// 使用 SIMD 向量化指令的灰度转换器（高性能版本）
/// 使用 System.Numerics.Vectors 实现向量化加速
/// </summary>
public class SimdGrayscaleConverter : IGrayscaleConverter
{
    // BT.709 标准灰度转换系数（整数形式）
    // 灰度 = (54 * R + 183 * G + 19 * B) >> 8
    private const int R_COEFF = 54;
    private const int G_COEFF = 183;
    private const int B_COEFF = 19;

    /// <summary>
    /// 将 RGB 数据转换为灰度数据（SIMD 优化版本）
    /// </summary>
    public byte[] ConvertToGrayscale(byte[] rgbData, int width, int height)
    {
        if (rgbData == null)
            throw new ArgumentNullException(nameof(rgbData));

        if (width <= 0 || height <= 0)
            throw new ArgumentException($"Width 和 height 必须是正数，当前：{width}x{height}");

        int expectedLength = width * height * 3;
        if (rgbData.Length != expectedLength)
            throw new ArgumentException(
                $"RGB 数据长度不匹配，期望 {expectedLength}，实际 {rgbData.Length}");

        byte[] grayData = new byte[width * height];
        
        // 获取 SIMD 向量宽度（通常是 4、8 或 16 字节）
        int vectorWidth = Vector<byte>.Count;
        int pixelCount = width * height;
        
        // 计算可以 SIMD 处理的像素数量（必须是向量宽度的倍数）
        int simdPixelLimit = (pixelCount / vectorWidth) * vectorWidth;
        
        // 预分配向量化系数
        var rVector = new Vector<int>(R_COEFF);
        var gVector = new Vector<int>(G_COEFF);
        var bVector = new Vector<int>(B_COEFF);

        // SIMD 向量化处理
        int pixelIndex = 0;
        int vectorPixelWidth = Vector<int>.Count;
        int simdByteLimit = (simdPixelLimit / vectorPixelWidth) * vectorPixelWidth;

        // 使用 int 向量处理（避免字节溢出）
        while (pixelIndex < simdByteLimit)
        {
            // 读取 R、G、B 分量
            int rgbBaseIndex = pixelIndex * 3;
            
            // 创建 int 向量
            int[] rValues = new int[vectorPixelWidth];
            int[] gValues = new int[vectorPixelWidth];
            int[] bValues = new int[vectorPixelWidth];

            for (int i = 0; i < vectorPixelWidth; i++)
            {
                int offset = rgbBaseIndex + i * 3;
                rValues[i] = rgbData[offset];
                gValues[i] = rgbData[offset + 1];
                bValues[i] = rgbData[offset + 2];
            }

            var rVec = new Vector<int>(rValues);
            var gVec = new Vector<int>(gValues);
            var bVec = new Vector<int>(bValues);

            // SIMD 乘加运算
            var grayVec = rVec * rVector + gVec * gVector + bVec * bVector;
            
            // 右移 8 位（除以 256）
            grayVec = Vector.ShiftRightArithmetic(grayVec, 8);

            // 提取结果
            int[] grayValues = new int[vectorPixelWidth];
            for (int i = 0; i < vectorPixelWidth; i++)
            {
                grayValues[i] = Math.Clamp(grayVec[i], 0, 255);
            }

            // 写入灰度数据
            for (int i = 0; i < vectorPixelWidth; i++)
            {
                grayData[pixelIndex + i] = (byte)grayValues[i];
            }

            pixelIndex += vectorPixelWidth;
        }

        // 标量处理剩余像素
        while (pixelIndex < pixelCount)
        {
            int rgbBaseIndex = pixelIndex * 3;
            int r = rgbData[rgbBaseIndex];
            int g = rgbData[rgbBaseIndex + 1];
            int b = rgbData[rgbBaseIndex + 2];

            int gray = (R_COEFF * r + G_COEFF * g + B_COEFF * b) >> 8;
            grayData[pixelIndex] = (byte)Math.Clamp(gray, 0, 255);
            pixelIndex++;
        }

        return grayData;
    }
}