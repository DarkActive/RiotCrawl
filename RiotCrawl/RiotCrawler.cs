using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using PVPNetConnect;

using RiotCrawl.Helpers;

namespace RiotCrawl
{
    /// <summary>
    /// RiotCrawler class which is in charge of creating threads and beginning crawls.
    /// </summary>
    public class RiotCrawler
    {

        public RiotConnect pvpnet;
        public Database db;

        public List<SummonerCrawler> summonersList;

        private static int MaxSummoners = 25000;

        public RiotCrawler(Region region)
        {
            summonersList = new List<SummonerCrawler>();
            db = new Database();

            if (db.getTrackedSummoners(ref summonersList))
            {
                crawl(region);
            }
        }

        private void crawl(Region region)
        {
            int numThreads;
            int summNdx, thrStart;
            CrawlWorkerData curWorker;
            List<RiotLogin> riotLogins = new List<RiotLogin>();
            List<RiotLogin> endRiotLogins = new List<RiotLogin>();
            List<CrawlWorkerData> thrSummList = new List<CrawlWorkerData>();
            List<SummonerCrawler> temp;
            List<Thread> _threads = new List<Thread>();
            List<CrawlWorker> _workers = new List<CrawlWorker>();
            float dTemp;

            CrawlWorker worker;
            Thread curThread;

            // Minimum of 4 threads
            if (summonersList.Count < (MaxSummoners * 4))
            {
                numThreads = 4;
                MaxSummoners = (summonersList.Count / 4) + 4;
            }
            else
            {
                dTemp = (summonersList.Count / float.Parse(MaxSummoners.ToString()));
                numThreads = int.Parse(Math.Ceiling(dTemp).ToString());
            }

            numThreads = 4;

            db.getRiotLogins(ref riotLogins, ref endRiotLogins);

            summNdx = 0;
            for (int thr = 0; thr < numThreads; thr++)
            {
                thrStart = thr * MaxSummoners;
                temp = new List<SummonerCrawler>();
                for (summNdx = thrStart; summNdx < (thrStart + MaxSummoners) && summNdx < summonersList.Count; summNdx++)
                {
                    temp.Add(summonersList[summNdx]);
                }

                curWorker = new CrawlWorkerData(region, temp, riotLogins[thr].Username, riotLogins[thr].Password, endRiotLogins[thr].Username, endRiotLogins[thr].Password);
                thrSummList.Add(curWorker);
                riotLogins[thr].inUse = true;

                worker = new CrawlWorker(curWorker);
                ThreadPool.QueueUserWorkItem(new WaitCallback(CrawlWorker.doWork), worker);
                _workers.Add(worker);

                Thread.Sleep(2000);

                while (worker.pvpnet != null && !worker.pvpnet.Connected) ;
            }

            Console.WriteLine("Number of threads: " + numThreads);
            Console.WriteLine("First Summ Thread 1: " + thrSummList.ElementAt(0).loginUser);
            //Console.WriteLine("First Summ Thread 2: " + thrSummList.ElementAt(1).loginUser);
            //Console.WriteLine("First Summ Thread 3: " + thrSummList.ElementAt(2).loginUser);

            int command;
            string lineRead;
            Console.Write("$ ");

            while ((command = Console.Read()) != 'q')
            {
                switch (command)
                {
                    case 'p':

                        lineRead = Console.ReadLine();

                        try
                        {
                            int thNum = int.Parse(lineRead);
                            thNum = thNum - 1;
                            if (thNum < _workers.Count)
                            {
                                if (_workers.ElementAt(thNum).pause)
                                    _workers.ElementAt(thNum).pause = false;
                                else
                                    _workers.ElementAt(thNum).pause = true;
                            }
                            else
                            {
                                Console.WriteLine("Thread doesn't exist");
                            }
                        }
                        catch (Exception ex)
                        {
                            string e = ex.ToString();
                            Console.WriteLine("Invalid thread");
                        }

                        break;

                    case 'c':

                        lineRead = Console.ReadLine();

                        try
                        {
                            int thNum = int.Parse(lineRead);
                            thNum = thNum - 1;
                            if (thNum < _workers.Count)
                            {
                                Console.WriteLine("Thread " + (thNum + 1) + " has " + _workers.ElementAt(thNum).summonerList.Count + " summoners.");
                            }
                            else
                            {
                                Console.WriteLine("Thread doesn't exist");
                            }
                        }
                        catch (Exception ex)
                        {
                            string e = ex.ToString();
                            Console.WriteLine("Invalid thread number");
                        }

                        break;

                    case 'l':

                        lineRead = Console.ReadLine();

                        try
                        {
                            int thNum = int.Parse(lineRead);
                            thNum = thNum - 1;
                            if (thNum < _workers.Count)
                            {
                                Console.WriteLine("Thread " + (thNum + 1) + " has " + _workers.ElementAt(thNum).liveGames.Count + " live games.");
                            }
                            else
                            {
                                Console.WriteLine("Thread doesn't exist");
                            }
                        }
                        catch (Exception ex)
                        {
                            string e = ex.ToString();
                            Console.WriteLine("Invalid thread number");
                        }

                        break;

                    case 'd':

                        lineRead = Console.ReadLine();

                        try
                        {
                            int count = 0;
                            int thNum = int.Parse(lineRead);
                            thNum = thNum - 1;
                            if (thNum < _workers.Count)
                            {
                                List<string> keys = _workers.ElementAt(thNum).liveGameKeys;
                                Dictionary<string, InGameSummoner> games = _workers.ElementAt(thNum).liveGames;

                                for (int i = 0; i < keys.Count; i++)
                                {
                                    if (games[keys.ElementAt(i)].gameCompleted)
                                        count++;
                                }

                                double pc = (count / keys.Count) * 100;
                                int percent = int.Parse(Math.Ceiling(pc).ToString());

                                Console.WriteLine("Thread " + (thNum + 1) + " has " + count + " completed games ( " + percent + " ).");
                            }
                            else
                            {
                                Console.WriteLine("Thread doesn't exist");
                            }
                        }
                        catch (Exception ex)
                        {
                            string e = ex.ToString();
                            Console.WriteLine("Invalid thread number");
                        }

                        break;

                    default:

                        Console.ReadLine();

                        break;
                }

                Console.Write("$ ");
            }
            Console.WriteLine("Program terminating...");
            Console.WriteLine("Goodbye!");
        }
    }
}
