﻿using System;

namespace WebsiteCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            Crawler crawler = new Crawler("https://www.apple.com/"); //https://www.automobile.tn
            crawler.LoadCrawler();
        }
    }
}
