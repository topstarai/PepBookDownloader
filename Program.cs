using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.Unicode;
            
            try
            {
                bool flag = true;
                while (flag)
                {
                    try
                    {
                        Console.WriteLine("请输入保存目录,按q退出");

                        string cmd = Console.ReadLine();
                        if (cmd == "q")
                        {
                            Console.WriteLine("你确定要退出？(退出请输入y，否则，输入n。)");
                            var yesORno = Console.ReadKey().Key.ToString().ToUpper();
                            switch (yesORno)
                            {
                                case "Y":
                                    flag = false;
                                    break;
                                case "N":
                                    flag = true;
                                    break;
                            }
                        }
                        else
                        {
                            await BoosDownloaderService.ExecuteAsync(cmd);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
