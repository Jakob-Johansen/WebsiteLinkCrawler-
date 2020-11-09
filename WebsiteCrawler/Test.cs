﻿using HtmlAgilityPack;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebsiteCrawler.Models;
using WebsiteCrawler.ReadSiteMapFolder;

namespace WebsiteCrawler
{
    public class Test
    {
        private readonly string _url;

        private readonly DbStuff _dbStuff = new DbStuff();

        private readonly Timer _timer;

        private readonly HttpClient _httpClient;

        private readonly Logs _log;

        private Browser _browser;

        private readonly ReadRobots _readRobots;

        private bool _browserRunning = false;

        public Test(string url)
        {
            //AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            
            _url = url.Trim();
            _httpClient = new HttpClient();
            _log = new Logs();
            _timer = new Timer();
            _readRobots = new ReadRobots();

            new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);

            _timer.StartTimer();
            Console.WriteLine("Crawler Starting\n");
        }

        public async Task LoadCrawler()
        {
            if (_url == null || _url.Length == 0)
            {
                Console.WriteLine("Dit link er ugyldit");
                return;
            }

            if (_dbStuff.CheckIfExist(_url))
            {
                var nextLink = _dbStuff.GetNextNotCrawled();

                if (nextLink.Id == 0)
                {
                    if (_browserRunning == true)
                    {
                        await _browser.CloseAsync();
                        await _browser.DisposeAsync();
                    }

                    _timer.StopTimer();
                    Console.WriteLine("Crawler Done");
                    return;
                }

                //ValidateFilter(nextLink.Link) && !nextLink.Link.Contains("//") && !nextLink.Link.Contains("http") && !nextLink.Link.Contains("jpg") && !nextLink.Link.Contains("jpeg") && !nextLink.Link.Contains("png") && nextLink.Crawled == 0
                if (ValidateFilter(nextLink) && !nextLink.Link.Contains("//") && !nextLink.Link.Contains("http") && !nextLink.Link.Contains(".jpg") && !nextLink.Link.Contains(".jpeg") && !nextLink.Link.Contains(".png") && !nextLink.Link.Contains(".mp3") && !nextLink.Link.Contains(".mp4") && !nextLink.Link.Contains(".exe"))
                {
                    Console.WriteLine("Crawled Id: " + nextLink.Id);
                    Console.WriteLine("Crawled Link: " + nextLink.Link);
                    _dbStuff.UpdateLink(nextLink.Id, 1);

                    await StartWebCrawler(nextLink);
                }
                else
                {
                    _dbStuff.UpdateLink(nextLink.Id, 2);
                    await LoadCrawler();
                }
            }
            else
            {
                if (await _readRobots.StartReadRobots(_url) == true)
                {
                   await LoadCrawler();
                    return;
                }
                else
                {
                    Console.WriteLine("Starting Manually Crwaler\n");
                    await SortLinks(_url);
                }
            }
        }

        private async Task StartWebCrawler(Links thisModel)
        {
            string url;

            // ---
            if (_url == thisModel.Link)
                url = _url;
            else
                url = _url + thisModel.Link.Trim();
            // ---

            try
            {
                HttpResponseMessage httpResponseMessage = await _httpClient.GetAsync(url);

                if (httpResponseMessage.StatusCode == HttpStatusCode.MovedPermanently)
                {
                    _dbStuff.UpdateLink(thisModel.Id, 2);
                    await LoadCrawler();
                }
                else
                {
                    if (_browserRunning == false)
                    {
                        _browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
                        _browserRunning = true;
                    }

                    using Page page = await _browser.NewPageAsync();

                    await page.GoToAsync(url);
                    string pageContent = await page.GetContentAsync();

                    HtmlDocument htmlDocument = new HtmlDocument();
                    htmlDocument.LoadHtml(pageContent);

                    var filteredLinks = htmlDocument.DocumentNode.Descendants().Where(node => node.Name.Equals("a")).ToList().Where(x => x.Attributes.Any(t => t.Name.Equals("href")));

                    await page.CloseAsync();

                    List<Links> linkList = new List<Links>();

                    foreach (var item in filteredLinks)
                    {
                        var itemLink = item.ChildAttributes("href").FirstOrDefault().Value;

                        if (!_dbStuff.CheckIfExist(itemLink))
                        {
                            Links linkModel = new Links()
                            {
                                Link = itemLink,
                                Crawled = 0
                            };

                            linkList.Add(linkModel);
                        }
                    }

                    if (linkList == null)
                    {
                        await LoadCrawler();
                    }

                    await SortLinks(linkList);
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("Der skete en fejl");
                Console.WriteLine(url);

                _log.CreateLog(url, e.Message);

                await LoadCrawler();
            }

            _httpClient.Dispose();
        }

        private async Task SortLinks(string link)
        {
            if (link.Contains("http"))
            {
                Links linkModel = new Links()
                {
                    Link = link,
                    Crawled = 1
                };

                _dbStuff.InputLink(linkModel, 1);
                await StartWebCrawler(linkModel);
            }
        }

        private async Task SortLinks(List<Links> links)
        {
            foreach (var item in links)
            {
                if (!_dbStuff.CheckIfExist(item.Link) && item.Link.Length > 1)
                {
                    string extension = Path.GetExtension(item.Link);

                    if (item.Link.Contains("/") || extension.Length > 2)
                    {
                        if (item.Link.Contains(":") && !item.Link.Contains("/"))
                        {
                            _dbStuff.InputLink(item, 2);
                        }
                        else
                        {
                            _dbStuff.InputLink(item, 0);
                        }
                    }
                }
            }

            await LoadCrawler();
        }

        private bool ValidateFilter(Links item)
        {
            string extension = Path.GetExtension(item.Link);

            if (item.Link.Contains("/") || extension.Length > 2 && item.Crawled == 0)
            {
                return true;
            }

            return false;
        }

        private void OnProcessExit(object sender, EventArgs e)
        {

            if (_browserRunning == true)
            {
                _browser.CloseAsync();
                _browser.DisposeAsync();
            }

            //Console.WriteLine("LÆS LIGE OP PÅ DET HER");
        }
    }
}
