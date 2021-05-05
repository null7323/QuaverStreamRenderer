using System;
using System.IO;
using System.Diagnostics;

namespace QQSConsole
{
    class Program
    {
        static void ShowHelp()
        {
            Console.WriteLine("Quaver Stream Renderer (Weekly Build)");
            Console.WriteLine("请注意: 这个版本并非稳定版，可能存在较多的Bug，但也有更多的新特性。");
            Console.WriteLine("程序作者: qishipai.\n");
            Console.WriteLine("命令行参数:                         参数描述:");
            Console.WriteLine("-f, --mid                           Midi路径");
            Console.WriteLine("-w, --width                         输出视频的宽度。单位为像素，默认为1920。");
            Console.WriteLine("-h, --height                        输出视频的高度。单位为像素，默认为1080。");
            Console.WriteLine("-ns, --notesize                     音符的长度。默认为1。");
            Console.WriteLine("-crf                                视频质量 (0-51)，数值越小视频越大，默认为17。");
            Console.WriteLine("-fps, --fps                         每秒视频帧数。默认为60。");
            //Console.WriteLine("-bar                                琴键分隔条的颜色。");
            Console.WriteLine("-o, --out                           输出视频的路径");
            Console.WriteLine("-nfl, --nofflog                     阻止FFMpeg输出日志信息");
            Console.WriteLine("-q, --quiet                         不输出任何信息");
            Console.WriteLine("-st, --singlethread                 使用单线程渲染，默认使用多线程。");
            Console.WriteLine("-nor, --disableor                   不对Midi进行OR处理，仅对多线程渲染有效。默认对Midi进行OR。");
            Console.WriteLine("-tc, --threadcount                  设置多线程渲染的最大线程数，默认为CPU线程数。");
            Console.WriteLine("-bc, --barcolor                     设置分隔条的颜色。这个参数接收4个不大于255的无符号整数，顺序为R，G，B，A。");
            Console.WriteLine("-p, --preview                       启用同步预览。\n");
            Console.WriteLine("示例用法: {0} --mid \"ouranos.mid\" --out \"ouranos.mp4\" --notesize 1.2", 
                Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName));
            Console.WriteLine("或者: {0} -f \"ouranos.mid\" -o \"ouranos.mp4\" -ns 1.2\n", 
                Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName));
        }
        static void Main(string[] args)
        {
            int argLen = args.Length;
            if (argLen == 0)
            {
                ShowHelp();
                return;
            }
            if (argLen < 4)
            {
                ShowHelp();
                return;
            }
            string filePath = null;
            string outPath = null;
            int crf = 17;
            int width = 1920;
            int height = 1080;
            int fps = 60;
            uint barcolor = 0xFF000080;
            double noteSpeed = 1;
            bool mt = true;
            bool noFFMpegLog = false;
            bool OR = true;
            bool quiet = false;
            bool preview = false;
            int tc = Environment.ProcessorCount;
            for (int i = 0; i < argLen; ++i)
            {
                switch (args[i])
                {
                    case "--mid" or "-f":
                        if (i + 1 == argLen)
                        {
                            Console.WriteLine("找不到Midi路径参数。\n");
                            return;
                        }
                        filePath = args[++i];
                        if (!File.Exists(filePath))
                        {
                            Console.WriteLine("找不到文件: {0}\n请确认文件路径是否正确。\n", filePath);
                            return;
                        }
                        if (!filePath.TrimEnd().EndsWith(".mid"))
                        {
                            Console.WriteLine("不合法文件: 这不是一个Midi文件。\n");
                            return;
                        }
                        break;
                    case "-o" or "--out":
                        if (i + 1 == argLen)
                        {
                            Console.WriteLine("找不到Midi路径参数。\n");
                            return;
                        }
                        outPath = args[++i];
                        if (File.Exists(outPath)) Console.WriteLine("警告: 输出文件已经存在，可能会被覆盖。");
                        break;
                    case "-crf":
                        if (i + 1 == argLen)
                        {
                            Console.WriteLine("找不到CRF参数。\n");
                            return;
                        }
                        if (int.TryParse(args[++i], out crf))
                        {
                            if (crf < 0 || crf > 51)
                            {
                                Console.WriteLine("crf参数应当是0-51之间的一个整数。\n");
                                return;
                            }
                        }
                        else
                        {
                            Console.WriteLine("crf参数应当是0-51之间的一个整数。\n");
                            return;
                        }
                        break;
                    case "-q" or "--quiet":
                        quiet = true;
                        break;
                    case "-p" or "--preview":
                        preview = true;
                        break;
                    case "-fps" or "--fps":
                        if (i + 1 == argLen)
                        {
                            Console.WriteLine("找不到FPS参数。\n");
                            return;
                        }
                        if (int.TryParse(args[++i], out fps))
                        {
                            if (crf < 24)
                            {
                                Console.WriteLine("FPS不应低于24。\n");
                                return;
                            }
                        }
                        else
                        {
                            Console.WriteLine("FPS参数应当是不低于24的整数。\n");
                            return;
                        }
                        break;
                    case "-w" or "--width":
                        if (i + 1 == argLen)
                        {
                            Console.WriteLine("找不到视频宽度参数。\n");
                            return;
                        }
                        if (int.TryParse(args[++i], out width))
                        {
                            if (width <= 0)
                            {
                                Console.WriteLine("视频宽度应当是大于0的整数。\n");
                                return;
                            }
                        }
                        else
                        {
                            Console.WriteLine("视频宽度应当是大于0的整数。\n");
                            return;
                        }
                        break;
                    case "-h" or "--height":
                        if (i + 1 == argLen)
                        {
                            Console.WriteLine("找不到视频高度参数。\n");
                            return;
                        }
                        if (int.TryParse(args[++i], out height))
                        {
                            if (height <= 0)
                            {
                                Console.WriteLine("视频高度应当是大于0的整数。\n");
                                return;
                            }
                        }
                        else
                        {
                            Console.WriteLine("视频高度应当是大于0的整数。\n");
                            return;
                        }
                        break;
                    case "-ns" or "--notesize":
                        if (i + 1 == argLen)
                        {
                            Console.WriteLine("找不到音符大小参数。\n");
                            return;
                        }
                        if (double.TryParse(args[++i], out noteSpeed))
                        {
                            if (noteSpeed < 0.25 || noteSpeed > 10)
                            {
                                Console.WriteLine("音符速度应当在0.25-10之间。\n");
                                return;
                            }
                        }
                        else
                        {
                            Console.WriteLine("音符速度应当在0.25-10之间。\n");
                            return;
                        }
                        break;
                    case "-st" or "--singlethread":
                        mt = false;
                        break;
                    case "-nfl" or "--nofflog":
                        noFFMpegLog = true;
                        break;
                    case "-nor" or "--disableor":
                        OR = false;
                        break;
                    case "-tc" or "--threadcount":
                        if (i + 1 == argLen)
                        {
                            Console.WriteLine("找不到线程数限制参数。\n");
                            return;
                        }
                        if (int.TryParse(args[++i], out tc))
                        {
                            if (tc <= 0 || tc > 2 * Environment.ProcessorCount)
                            {
                                Console.WriteLine("线程限制应当在1至逻辑处理器个数之间。\n");
                                return;
                            }
                        }
                        else
                        {
                            Console.WriteLine("线程限制应当是1至逻辑处理器个数之间的整数。\n");
                            return;
                        }
                        break;
                    case "-bc" or "--barcolor":
                        byte v;
                        uint col = 0x00000000;
                        if (i + 4 >= argLen)
                        {
                            Console.WriteLine("缺少RGBA参数。\n");
                            return;
                        }
                        for (int k = 0; k != 4; ++k)
                        {
                            if (byte.TryParse(args[++i], out v))
                            {
                                unsafe
                                {
                                    *((byte*)(&col) + k) = v;
                                }
                            }
                            else
                            {
                                Console.WriteLine("分割条颜色应当在0和255之间。\n");
                                return;
                            }
                        }
                        barcolor = col;
                        break;
                    default:
                        Console.WriteLine("不正确的参数类型。\n");
                        return;
                }
            }
            if (filePath is null)
            {
                Console.WriteLine("没有指定Midi路径。\n");
                return;
            }
            if (outPath is null)
            {
                Console.WriteLine("没有指定输出视频路径。\n");
                return;
            }
            Stopwatch sw;
            sw = Stopwatch.StartNew();
            RenderOptions options = new()
            {
                FPS = fps,
                NoFFMpegLog = noFFMpegLog,
                CRF = crf, 
                MaxThread = tc,
                BarColor = barcolor,
                Quiet = quiet,
                Preview = preview,
                Width = width,
                Height = height,
                KeyHeight = height * 15 / 100,
                NoteSpeed = (float)noteSpeed,
            };
            Console.WriteLine("参数解析完成。当前渲染参数: ");
            Console.WriteLine("视频 FPS: {0}", options.FPS);
            Console.WriteLine("使用FFMpeg日志: {0}", !options.NoFFMpegLog);
            Console.WriteLine("视频 CRF: {0}", options.CRF);
            Console.WriteLine("不输出任何日志: {0}", options.Quiet);
            Console.WriteLine("同步预览: {0}", options.Preview);
            Console.WriteLine("视频分辨率: {0} x {1}", options.Width, options.Height);
            Console.WriteLine("视频输出路径: {0}\n", outPath);
            try
            {
                if (!mt)
                {
                    Renderer renderer = new(new(filePath, quiet));
                    renderer.Run(options, outPath);
                }
                else
                {
                    MTRenderer renderer = new(new(filePath, quiet), OR);
                    renderer.Run(options, outPath);
                }
                if (!quiet) Console.WriteLine("渲染完成。全程耗时: {0} h {1} min {2} s\n", sw.Elapsed.Hours, sw.Elapsed.Minutes, sw.Elapsed.Seconds);
            }
            catch (Exception ex)
            {
                Console.Clear();
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
