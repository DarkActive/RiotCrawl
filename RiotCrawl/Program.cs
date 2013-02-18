using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using PVPNetConnect;
using PVPNetConnect.Assets;
using PVPNetConnect.RiotObjects.Summoner;
using PVPNetConnect.RiotObjects.Game;
using PVPNetConnect.RiotObjects.Statistics;

namespace RiotCrawl
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 0)
            {
                switch (args[0])
                {
                    case "crawl":
                        if (args[1] == "NA")
                            crawlPvPNet(Region.NA);
                        else if (args[1] == "EUW")
                            crawlPvPNet(Region.EUW);
                        else if (args[2] == "EUNE")
                            crawlPvPNet(Region.EUN);
                        break;

                    case "test":
                        Test();
                        break;
                }
            }
        }

        private static void crawlPvPNet(Region region)
        {
            RiotCrawler crawler = new RiotCrawler(region);
        }

        private static void Test()
        {
            Console.Write("Hello");
            RiotConnect pvpnet = new RiotConnect(Region.NA, "wesa001", "baylife13");

            while (!pvpnet.Connected)
                ;

            Console.WriteLine("test");

            RecentGames summ;
            Console.Write("Hello");
            double starttime = getUnixTimestamp();
            for (int i = 0; i < 100; i++)
            {
                pvpnet.getSummonerByName("wesa001");
            }

            Console.WriteLine("DONE: " + (getUnixTimestamp() - starttime));

            while (1 == 1) ;
        }

        static double getUnixTimestamp()
        {
            DateTime epoch = new DateTime(1970, 1, 1).ToLocalTime();
            TimeSpan span = (DateTime.Now - epoch);

            return span.TotalSeconds;
        }
    }
}