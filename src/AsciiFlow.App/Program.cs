using System.Diagnostics;
using AsciiFlow.Core.AsciiMapping;

namespace AsciiFlow.App;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== 阶段 4: 查找表字符映射模块测试 ===\n");

        // 测试参数
        const int SRC_WIDTH = 1920;
        const int SRC_HEIGHT = 1080;
        const int TARGET_WIDTH = 80;
        const int TARGET_HEIGHT = 40;

        Console.WriteLine($"源图像尺寸: {SRC_WIDTH}x{SRC_HEIGHT}");
        Console.WriteLine($"目标字符画尺寸: {TARGET_WIDTH}x{TARGET_HEIGHT}");
        Console.WriteLine($"压缩比: {(SRC_WIDTH * SRC_HEIGHT) / (TARGET_WIDTH * TARGET_HEIGHT):N0}x\n");

        // 生成测试灰度数据（模拟从左到右的渐变）
        byte[] testGrayData = GenerateTestGrayData(SRC_WIDTH, SRC_HEIGHT);
        Console.WriteLine($"测试数据已生成（{testGrayData.Length:N0} 字节）\n");

        // 测试 Standard 字符集
        TestCharacterSet(
            "Standard 字符集 (69 字符)",
            LookupTableAsciiMapper.Standard,
            testGrayData,
            SRC_WIDTH,
            SRC_HEIGHT,
            TARGET_WIDTH,
            TARGET_HEIGHT);

        // 测试 Detailed 字符集
        TestCharacterSet(
            "Detailed 字符集 (25 字符)",
            LookupTableAsciiMapper.Detailed,
            testGrayData,
            SRC_WIDTH,
            SRC_HEIGHT,
            TARGET_WIDTH,
            TARGET_HEIGHT);

        // 显示示例输出
        Console.WriteLine("=== 示例输出（Standard 字符集，10 行预览）===\n");
        var mapper = new LookupTableAsciiMapper();
        string asciiArt = mapper.MapToAscii(
            testGrayData,
            SRC_WIDTH,
            SRC_HEIGHT,
            TARGET_WIDTH,
            10);  // 只输出 10 行用于预览
        
        Console.WriteLine(asciiArt);

        Console.WriteLine("\n=== 阶段 4 测试完成 ===");
    }

    /// <summary>
    /// 测试特定字符集的性能
    /// </summary>
    static void TestCharacterSet(
        string name,
        string characterSet,
        byte[] grayData,
        int srcWidth,
        int srcHeight,
        int targetWidth,
        int targetHeight)
    {
        Console.WriteLine($"【{name}】");
        Console.WriteLine($"字符集: {characterSet}");
        Console.WriteLine($"字符数: {characterSet.Length}\n");

        var mapper = new LookupTableAsciiMapper(characterSet);

        // 预热
        mapper.MapToAscii(grayData, srcWidth, srcHeight, targetWidth, targetHeight);

        // 性能测试
        const int ITERATIONS = 10;
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < ITERATIONS; i++)
        {
            string asciiArt = mapper.MapToAscii(
                grayData,
                srcWidth,
                srcHeight,
                targetWidth,
                targetHeight);
        }

        stopwatch.Stop();
        double avgTimeMs = stopwatch.Elapsed.TotalMilliseconds / ITERATIONS;
        double fps = 1000.0 / avgTimeMs;

        Console.WriteLine($"平均处理时间: {avgTimeMs:F3} ms");
        Console.WriteLine($"预计 FPS: {fps:N0}");
        Console.WriteLine($"字符查找开销: O(1) 每像素\n");
    }

    /// <summary>
    /// 生成测试灰度数据（从左到右的渐变）
    /// </summary>
    static byte[] GenerateTestGrayData(int width, int height)
    {
        var data = new byte[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 从左到右：0（黑）→ 255（白）
                byte grayValue = (byte)((x * 255) / (width - 1));
                data[y * width + x] = grayValue;
            }
        }

        return data;
    }
}