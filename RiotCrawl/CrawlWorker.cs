using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;

using RiotCrawl.Helpers;

using PVPNetConnect;
using PVPNetConnect.Assets;
using PVPNetConnect.RiotObjects;
using PVPNetConnect.RiotObjects.Catalog;
using PVPNetConnect.RiotObjects.Client;
using PVPNetConnect.RiotObjects.Game;
using PVPNetConnect.RiotObjects.Leagues;
using PVPNetConnect.RiotObjects.Statistics;
using PVPNetConnect.RiotObjects.Summoner;

namespace RiotCrawl
{
    public class CrawlWorker
    {
        # region Constants

        private const int GameStatusFreq = 60;
        private const int RecFreq = 5;
        private const int OUTOFGAME = 0;
        private const int INGAME = 1;
        private const int GAMEDONE = 2;
        private const int WAITINTERVAL = 3000;

        # endregion

        #region Class Variables

        public RiotConnect pvpnet;
        public bool pause;
        private EndGameWorker endGameWorker;
        public Database db;
        public CrawlWorkerData data;
        public List<SummonerCrawler> summonerList;
        public Dictionary<string, InGameSummoner> liveGames;
        public List<string> liveGameKeys;
        public double lastUpdateSummList;

        private AutoResetEvent stopWaitHandle;
        public double startTime;
        public int pvpnetReconnects;

        public bool forceDC;
        private string logPath;

        #endregion

        #region Worker Methods

        public CrawlWorker(CrawlWorkerData cData)
        {
            pause = false;
            data = cData;
            forceDC = false;
            db = new Database();

            summonerList = cData.summonerList;
            liveGames = new Dictionary<string, InGameSummoner>();
            liveGameKeys = new List<string>();
            stopWaitHandle = new AutoResetEvent(false);
            pvpnetReconnects = 0;

            logPath = data.loginUser + ".log";
            /** Clear log file */
            File.WriteAllText(logPath, String.Empty);
        }

        public static void doWork(object sender)
        {
            CrawlWorker worker = (CrawlWorker)sender;

            /** Persistent connection */
            while (!worker.forceDC)
            {
                /* Establish Connection to PvPNet */
                worker.pvpnet = new RiotConnect(worker.data.region, worker.data.loginUser, worker.data.loginPass);

                /** Wait for PvP.net Server connection */
                if (waitForPvpnet(worker.pvpnet))
                {
                    /* Initialise the PvPNet Crawler */
                    worker.crawlPvpnet();
                }
            }
        }

        private void crawlPvpnet()
        {
            PlatformGameLifecycle specGame;
            GameDTO liveGame;
            int summNdx;
            SummonerCrawler curSummoner;
            int queries = 0, totalQueries = 0;
            double lastItr = 0, time, lastPost = 0;
            bool firstItr = true;

            lastUpdateSummList = getUnixTimestamp();

            /* New thread in charge of checking endGame */
            /*
            if (pvpnetReconnects == 0)
            {
                endGameWorker = new EndGameWorker(ref liveGames, ref liveGameKeys, data.region, data.endLoginUser, data.endLoginPass);
                Thread endGameThread = new Thread(endGameWorker.doWork);
                endGameThread.Start();
            }

             */
            while (pvpnet.Connected)
            {
                // If pause is set, wait until unpaused
                while (pause)
                {
                    ConsoleOut("Thread is paused....");
                    Thread.Sleep(10 * 1000);
                }

                /* Signal start of first iteration */
                lastItr = getUnixTimestamp();

                for (summNdx = 0; summNdx < summonerList.Count && pvpnet.Connected && !pause; summNdx++)
                {
                    curSummoner = summonerList.ElementAt(summNdx);

                    if ((getUnixTimestamp() - curSummoner.lastGameCheck) < GameStatusFreq)
                    {
                        continue;
                    }

                    if (curSummoner.gameStatus == OUTOFGAME)
                    {
                        // Currently in a new game, add pregame info
                        if ((specGame = pvpnet.getSpectatorGameByName(curSummoner.summonerName)) != null)
                        {
                            liveGame = specGame.Game;
                            curSummoner.gameStatus = INGAME;

                            // Add to db and liveGame list if doesn't already exist
                            if (!db.gameStatsExists(liveGame.ID))
                            {
                                double pc = (double.Parse(summNdx.ToString()) / summonerList.Count) * 100;
                                int percent = int.Parse(Math.Round(pc).ToString());
                                ConsoleOut("\tGameUpdate: Adding In Progress Game - ID =  " + liveGame.ID + " ------- " + percent + "%");

                                curSummoner.lastGameId = liveGame.ID;

                                db.addInProgressGame(specGame, curSummoner.accountId);
                                liveGames.Add(getHashKey(liveGame.ID, curSummoner.accountId), new InGameSummoner((SummonerCrawler)curSummoner.Clone()));
                                liveGameKeys.Add(getHashKey(curSummoner.lastGameId, curSummoner.accountId));
                            }
                            curSummoner.lastGameId = liveGame.ID;
                        }

                        curSummoner.lastGameCheck = getUnixTimestamp();
                    }
                    else if (curSummoner.gameStatus == INGAME)
                    {
                        // Was in game and no longer. Set game Status to finshed.
                        if ((specGame = pvpnet.getSpectatorGameByName(curSummoner.summonerName)) == null)
                        {
                            curSummoner.gameStatus = OUTOFGAME;
                            if (liveGames.ContainsKey(getHashKey(curSummoner.lastGameId, curSummoner.accountId)))
                            {
                                endGameWorker.pause = false;
                                liveGames[getHashKey(curSummoner.lastGameId, curSummoner.accountId)].gameCompleted = true;
                                ConsoleOut("\tGameUpdate: Set game to completed - ID = " + curSummoner.lastGameId);
                            }
                            curSummoner.lastGameId = 0;
                        }
                        // Was in game and still in game, but check for different game
                        else
                        {
                            liveGame = specGame.Game;
                            if (curSummoner.lastGameId != liveGame.ID)
                            {
                                // Set old game to complete
                                if (liveGames.ContainsKey(getHashKey(curSummoner.lastGameId, curSummoner.accountId)))
                                {
                                    endGameWorker.pause = false;
                                    liveGames[getHashKey(curSummoner.lastGameId, curSummoner.accountId)].gameCompleted = true;
                                    ConsoleOut("\tGameUpdate: Set game to completed, but late - ID = " + curSummoner.lastGameId);
                                }

                                // Add to db and liveGame list if doesn't already exist
                                if (!db.gameStatsExists(liveGame.ID))
                                {
                                    double pc = (double.Parse(summNdx.ToString()) / summonerList.Count) * 100;
                                    int percent = int.Parse(Math.Round(pc).ToString());
                                    ConsoleOut("\tGameUpdate: Adding In Progress Game - ID =  " + liveGame.ID + " ------- " + percent + "%");

                                    curSummoner.lastGameId = liveGame.ID;

                                    db.addInProgressGame(specGame, curSummoner.accountId);
                                    liveGames.Add(getHashKey(curSummoner.lastGameId, curSummoner.accountId), new InGameSummoner((SummonerCrawler)curSummoner.Clone()));
                                    liveGameKeys.Add(getHashKey(curSummoner.lastGameId, curSummoner.accountId));
                                }
                            }
                            curSummoner.lastGameId = liveGame.ID;
                        }

                        curSummoner.lastGameCheck = getUnixTimestamp();
                    }

                    queries++;
                    totalQueries++;
                }
                time = getUnixTimestamp() - lastItr;
                ConsoleOut("TrackStatus: " + summonerList.Count + " Summoners tracked; " + liveGames.Count() + " Live Games; Iteration took " + time + " seconds");
                lastPost = getUnixTimestamp();
                firstItr = false;
            }
            pvpnetReconnects++;
        }

        #endregion

        #region Helper Methods

        private string getHashKey(long gameId, long acctId)
        {
            return gameId.ToString() + "_" + acctId.ToString();
        }

        private static double getUnixTimestamp()
        {
            DateTime epoch = new DateTime(1970, 1, 1).ToLocalTime();
            TimeSpan span = (DateTime.Now - epoch);

            return span.TotalSeconds;
        }

        private static int CompareGames(PlayerGameStats x, PlayerGameStats y)
        {
            return -x.CreateDate.CompareTo(y.CreateDate);
        }

        private void ConsoleOut(string str)
        {
            string now;
            using (StreamWriter outfile = File.AppendText(logPath))
            {
                now = DateTime.Now.ToString("yy/MM/dd HH:mm:ss");
                outfile.WriteLine("[" + now + "] " + str);
            }
        }

        /** Method to wait for connection or keep trying reconnect */
        private static bool waitForPvpnet(RiotConnect pvpnet)
        {
            int time = 0;

            while (!pvpnet.Connected && time < RecFreq)
            {
                Thread.Sleep(1000);
                time++;
            }

            if (time >= RecFreq)
                return false;

            return true;
        }

        #endregion
    }
}
