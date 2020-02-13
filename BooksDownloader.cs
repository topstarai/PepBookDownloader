using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    sealed class BoosDownloaderService
    {
        private static readonly BooksDownloader booksDownloader = new BooksDownloader();
        private static async Task RunDownload(string directory, string category, List<string> urls)
        {
            Console.WriteLine($"正在下载“{category}”中的{urls.Count}个学科的书籍，请等待...");
            await booksDownloader.DownloadMultipleSubjectBooksAsync(directory, category, urls,(title,count)=> {
                Console.WriteLine($"{title},共有{count}个文件下载完成");
            });
        }

        public static async Task ExecuteAsync(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException();
            }

            //获取每个大类的下的学科页面的地址
            var bookUrls = await booksDownloader.GetSubjectPageUrlsAsync();

            Console.WriteLine("请输入下列编号，开始下载指定分类书籍。");
            Console.WriteLine("0 : 所有书籍");

            for (int i = 0; i < bookUrls.Count; i++)
            {
                Console.WriteLine($"{i + 1} : {bookUrls.ElementAt(i).Key}");
            }

            if (int.TryParse(Console.ReadLine(), out int index) && index <= bookUrls.Count && index >= 0)
            {
                if (index > 0)
                {
                    string category = bookUrls.ElementAt(--index).Key;

                    var filterUrls = bookUrls[category];
                    await RunDownload(directory, category, filterUrls);
                }
                if (index == 0)
                {
                    foreach (var item in bookUrls)
                    {
                        await RunDownload(directory, item.Key, item.Value);
                    }
                }
            }
            else
            {
                Console.WriteLine("输入的编号错误");
            }
        }

        sealed class BooksDownloader
        {
            const string BASE_URL = "http://bp.pep.com.cn/jc/";
            private static string FixPath(string path)
            {
                StringBuilder rBuilder = new StringBuilder(path);
                foreach (char rInvalidChar in Path.GetInvalidPathChars())
                    rBuilder.Replace(rInvalidChar.ToString(), string.Empty);
                return rBuilder.ToString();
            }

            private static string FixFileName(string strFileName)
            {
                StringBuilder rBuilder = new StringBuilder(strFileName);
                foreach (char rInvalidChar in Path.GetInvalidFileNameChars())
                    rBuilder.Replace(rInvalidChar.ToString(), string.Empty);
                return rBuilder.ToString();
            }

            //下载多个指定科目的下所有书籍
            public async Task DownloadMultipleSubjectBooksAsync(string directory, string category, List<string> subjectUrls, Action<string,string> callback)
            {
                var dir = Path.Combine(directory, $@"{category}");
                dir = FixPath(dir);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                foreach (var url in subjectUrls)
                {
                    var subjectBooks = await GetSubjectBooksAsync(url);
                    await DownloadBooksAsync(dir,url,subjectBooks, callback);
                }
            }

            //获取各学科各页面地址
            public async Task<Dictionary<string, List<string>>> GetSubjectPageUrlsAsync()
            {
                var url = BASE_URL;
                Dictionary<string, List<string>> bookUrls = new Dictionary<string, List<string>>();

                var categoryXpath = "//*[@id=\"container\"]/div[@class=\"list_sjzl_jcdzs2020\"]";

                //获取指定地址的html页面内容
                WebClient webClient = new WebClient();
                var content = await webClient.DownloadStringTaskAsync(url);

                //加载html内容到HtmlDocument以便处理内容
                HtmlDocument htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(content);

                //获取指定路径的节点集合
                HtmlNodeCollection booksListEle = htmlDocument.DocumentNode.SelectNodes(categoryXpath);

                if (booksListEle != null)
                {
                    foreach (var item in booksListEle)
                    {
                        //获取中学，小学等这些分类名称
                        string title = string.Empty;
                        var titleNode = item.SelectSingleNode(".//div[@class=\"container_title_jcdzs2020\"]");
                        if (titleNode != null)
                        {
                            title = titleNode?.InnerText;
                        }

                        //获取中学，小学等这些分类下的各学科页面所在地址
                        HtmlNodeCollection urlsNodes = item.SelectNodes(".//a");
                        if (urlsNodes?.Count > 0)
                        {
                            var list = new List<string>();
                            foreach (HtmlNode urlItem in urlsNodes)
                            {
                                var fullUrl = url + urlItem.Attributes["href"].Value.Substring(2);
                                list.Add(fullUrl);
                            }

                            if (!string.IsNullOrEmpty(title) && list.Count > 0)
                            {
                                bookUrls.Add(title, list);
                            }
                        }
                    }
                }
                return bookUrls;
            }

            //获取各学科页面中的电子书地址
            private async Task<(string Subject, List<(string BookName, string BookUrl)> Books)> GetSubjectBooksAsync(string url)
            {
                const string contentRootXpath = "//*[@id=\"container\"]/div[@class=\"con_list_jcdzs2020\"]";

                //Get html content
                WebClient client = new WebClient();
                string webcontent = await client.DownloadStringTaskAsync(url);

                //load html string with HtmlDocument
                HtmlDocument htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(webcontent);

                HtmlNode rootNode = htmlDocument.DocumentNode.SelectSingleNode(contentRootXpath);

                //Get the subject.获取学科名称
                HtmlNode titleEle = rootNode.SelectSingleNode(".//div[@class=\"con_title_jcdzs2020\"]");
                string subject = string.Concat(titleEle?.InnerText.Where(c => !char.IsWhiteSpace(c)));

                //Get all books of the subject. 
                //获取学科下所有书列表并开始下载
                HtmlNodeCollection bookNodes = rootNode.SelectNodes(".//li");
                List<(string BookName, string BookUrl)> books = new List<(string BookName, string BookUrl)>();
                if (bookNodes != null && bookNodes.Count>0)
                {
                    string bookName = null;
                    string bookUrl = null;

                    foreach (HtmlNode liItem in bookNodes)
                    {
                        bookName = FixFileName(string.Concat(liItem.ChildNodes["h6"].InnerText.Where(c => !char.IsWhiteSpace(c))));//get book's name
                        bookUrl = liItem.ChildNodes["div"].ChildNodes[3].Attributes["href"].Value;//get the url of ebook

                        books.Add((bookName, bookUrl));
                    }
                }
                return (subject,books);
            }

            //下载单个科目下的所有书籍
            private async Task DownloadBooksAsync(string dir, string baseUrl, (string Subject, List<(string BookName, string BookUrl)> Books) books,Action<string, string> callback)
            {
                //Create the subdirectory under the specified directory.
                //创建子目录
                dir = Path.Combine(dir, books.Subject);
                dir = FixPath(dir);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                //构建下载任务列表
                List<Task> downloadTasks = new List<Task>();
                int count = 0;
                foreach (var book in books.Books)
                {
                    WebClient wc = new WebClient();
                    Uri.TryCreate(baseUrl + book.BookUrl[2..], UriKind.Absolute, out Uri bookUri);
                    var path = Path.Combine(dir, @$"{book.BookName}.pdf");
                    var fi = new FileInfo(path);
                    if (!fi.Exists || fi.Length == 0)
                    {
                        var task = wc.DownloadFileTaskAsync(bookUri, path);
                        downloadTasks.Add(task);
                        count++;
                    }
                }

                //等待所有下载任务执行完后，执行回调函数
                await Task.WhenAll(downloadTasks).ContinueWith((task) => { callback(books.Subject ?? string.Empty, count.ToString()); });
            }
        }
    }
}
