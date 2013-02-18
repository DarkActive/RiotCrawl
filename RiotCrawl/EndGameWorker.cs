using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;

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
    public class EndGameWorker
    {
        #region CONSTANTS

        private const int RECFREQ = 5;
        private const int MAXWAITASYNC = 10000;

        #endregion

        #region Class Variables

        public Dictionary<string, InGameSummoner> inGameSummoners;
        public List<string> inGameKeys;
        public RiotConnect pvpnet;
        public bool pause;
        public Region region;
        public string endLoginUser;
        public string endLoginPass;
        public bool forceDC;
        public Database db;
        public string logPath;

        public int pvpnetReconnects;
        public double startTime;
        public AutoResetEvent stopWaitHandle;
        private bool gameUpdatedElsewhere;
        public int endTotalPlayers;
        public SummonerCrawler reportingSummoner;
        public List<EndSummonerGameStats> endSummonerStats;
        public List<PublicSummoner> endPSummoners;
        public List<PlayerLifetimeStats> endLifeStats;

        #endregion

        #region Worker Methods

        public EndGameWorker(ref Dictionary<string, InGameSummoner> iGS, ref List<string> keys, Region reg, string endUser, string endPass)
        {
            logPath = endUser + ".log";
            /** Clear log file */
            File.WriteAllText(logPath, String.Empty);

            inGameSummoners = iGS;
            inGameKeys = keys;
            db = new Database();
            region = reg;
            endLoginUser = endUser;
            endLoginPass = endPass;
            stopWaitHandle = new AutoResetEvent(false);
            gameUpdatedElsewhere = false;
            pvpnetReconnects = 0;

            pause = true;

            endSummonerStats = new List<EndSummonerGameStats>();
            endPSummoners = new List<PublicSummoner>();
            endLifeStats = new List<PlayerLifetimeStats>();
            forceDC = false;
        }

        public void doWork()
        {
            int ndx;
            double lastPrint = 0;
            double startTime;
            RecentGames recGames;
            InGameSummoner liveGame;
            int i;

            while (!forceDC)
            {
                pvpnet = new RiotConnect(region, endLoginUser, endLoginPass);

                if (waitForPvpnet(pvpnet))
                {
                    while (pvpnet.Connected)
                    {
                        while (pause)
                        {
                            ConsoleOut("Thread is paused....");
                            Thread.Sleep(10 * 1000);
                        }

                        for (ndx = 0; ndx < inGameKeys.Count && pvpnet.Connected; ndx++)
                        {
                            liveGame = inGameSummoners[inGameKeys.ElementAt(ndx)];
                            // Game Completed, begin end game stats
                            if (liveGame != null && liveGame.gameCompleted)
                            {
                                ConsoleOut("GameUpdate: Game completed and updating started - ID = " + liveGame.summonerCr.lastGameId);
                                startTime = getUnixTimestamp();
                                startEndGameStats(liveGame.summonerCr);

                                if (stopWaitHandle.WaitOne(MAXWAITASYNC))
                                {
                                    // This is a boolean that specifies if other thread updated End game
                                    if (!gameUpdatedElsewhere)
                                        finishEndGameStats();

                                    gameUpdatedElsewhere = false;
                                    inGameSummoners.Remove(inGameKeys.ElementAt(ndx));
                                    inGameKeys.RemoveAt(ndx);
                                }
                                else
                                {
                                    ConsoleOut("GameUpdate: RETRY Game completed and updating started - ID = " + liveGame.summonerCr.lastGameId + " RETRY");
                                    startEndGameStats(liveGame.summonerCr);

                                    if (stopWaitHandle.WaitOne(MAXWAITASYNC))
                                    {
                                        // This is a boolean that specifies if other thread updated End game
                                        if (!gameUpdatedElsewhere)
                                            finishEndGameStats();

                                        gameUpdatedElsewhere = false;
                                        inGameSummoners.Remove(inGameKeys.ElementAt(ndx));
                                        inGameKeys.RemoveAt(ndx);
                                    }
                                    // On 2 timeouts, remove all game entries from database and add as an old game.
                                    else
                                    {
                                        ConsoleOut("GameUpdate: REMOVING GAME AND ADDING AS OLD GAME ID = " + liveGame.summonerCr.lastGameId);
                                        db.removeLiveGame(liveGame.summonerCr.lastGameId);
                                        db.removePreGame(liveGame.summonerCr.lastGameId);

                                        // Sort recent games and add correct to DB
                                        if ((recGames = pvpnet.getRecentGamesByAccount(liveGame.summonerCr.accountId)) != null)
                                        {
                                            recGames.GameStatsList.Sort(CompareGames);
                                            for (i = 0; i < recGames.GameStatsList.Count; i++)
                                            {
                                                if (recGames.GameStatsList.ElementAt(i).GameID == liveGame.summonerCr.lastGameId)
                                                {
                                                    if (!db.gameStatsExists(liveGame.summonerCr.lastGameId))
                                                    {
                                                        db.addOldGame(recGames.GameStatsList.ElementAt(i), liveGame.summonerCr.accountId);
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                        inGameSummoners.Remove(inGameKeys.ElementAt(ndx));
                                        inGameKeys.RemoveAt(ndx);
                                    }

                                    liveGame.timedOut = true;
                                    ConsoleOut("TIMEOUT: Timedout twice on game update");
                                }
                            }

                            if (getUnixTimestamp() - lastPrint >= 10)
                            {
                                ConsoleOut("...... " + inGameSummoners.Count);
                                lastPrint = getUnixTimestamp();
                            }
                        }
                    }
                    pvpnetReconnects++;
                }
            }
        }

        public void startEndGameStats(SummonerCrawler curSummoner)
        {
            // Clear all endGameLists to start new endGame
            endTotalPlayers = 10;                       // Set to max, until other async call back sets it
            endSummonerStats.Clear();
            endPSummoners.Clear();
            endLifeStats.Clear();
            reportingSummoner = curSummoner;
            startTime = getUnixTimestamp();

            RecentGames recentGames = pvpnet.getRecentGamesByAccount(curSummoner.accountId);
            PlayerLifetimeStats lifeStats = pvpnet.retrievePlayerStatsByAccount(curSummoner.accountId, Seasons.CURRENT);
            PublicSummoner summoner = pvpnet.getSummonerByName(curSummoner.summonerName);

            endPSummoners.Add(summoner);
            endRecentGamesResponder(recentGames);
            endPlayerLifeStatsResponder(lifeStats);
        }

        public void finishEndGameStats()
        {
            PlayerGameStats pStats;
            PublicSummoner pSumm;
            PlayerLifetimeStats lifeStats;
            GameResult gRes;
            bool error = false;

            db.updateEndGame(reportingSummoner.lastGameId, reportingSummoner.accountId);

            ConsoleOut("\tGameUpdate: Updating end game done, now doing fellow players - FIRST Time = " + (getUnixTimestamp() - startTime).ToString());

            for (int i = 0; i < endSummonerStats.Count; i++)
            {

                pStats = endSummonerStats.ElementAt(i).gameStats;
                gRes = new GameResult(pStats);

                if ((lifeStats = findLifeStats(endSummonerStats.ElementAt(i).accountId)) == null)
                    error = true;

                if ((pSumm = findPublicSummoner(endSummonerStats.ElementAt(i).accountId)) == null)
                    error = true;

                if (!error)
                    db.addGameStats(endSummonerStats.ElementAt(i).accountId, pStats, gRes, lifeStats, pSumm);
            }


            if (error)
            {
                ConsoleOut("ERROR: Updating 1 or more summoner's life stats and other stats");
            }
            db.removeLiveGame(reportingSummoner.lastGameId);
            ConsoleOut("\tGameUpdate: Finished Updating End of Game Stats ----- Time = " + (getUnixTimestamp() - startTime).ToString());
        }

        #endregion

        #region Async Responder Methods

        public void endRecentGamesResponder(RecentGames rGames)
        {
            int ndx, sNdx, i;
            bool tempExists;
            PlayerGameStats pgStats;
            int[] fellowSummonerIds = new int[10];
            string[] summonerNames;

            if (rGames == null)
                return;

            // Sort recent games by newest game first
            rGames.GameStatsList.Sort(CompareGames);

            for (ndx = 0; ndx < rGames.GameStatsList.Count; ndx++)
            {
                pgStats = rGames.GameStatsList.ElementAt(ndx);

                /* Check if game is current game we are updating */
                if (pgStats.GameID == reportingSummoner.lastGameId)
                {
                    if (db.gameStatsIncomplete(pgStats.GameID))
                    {
                        endSummonerStats.Add(new EndSummonerGameStats(pgStats, rGames.AccountID));

                        // Set GLOBAL variable for endGame total players
                        endTotalPlayers = 1 + pgStats.FellowPlayers.Count;

                        for (sNdx = 0; sNdx < pgStats.FellowPlayers.Count; sNdx++)
                        {
                            fellowSummonerIds[sNdx] = pgStats.FellowPlayers.ElementAt(sNdx).SummonerID;
                        }

                        summonerNames = pvpnet.getSummonerNames(fellowSummonerIds);
                        endSummonerNamesResponder(summonerNames);

                        // Async call to get all names by summoner IDs, continues in responder function
                        /*
                        pvpnet.RPC.GetSummonerNamesAsync(fellowSummonerIds, new FluorineFx.Net.Responder<List<string>>(endSummonerNamesResponder));
                         */
                    }
                    else
                    {
                        // This is a boolean that specifies if other thread updated End game
                        gameUpdatedElsewhere = true;
                        stopWaitHandle.Set();
                    }
                    break;
                }
            }

            for (i = (ndx + 1); i < rGames.GameStatsList.Count; i++)
            {
                pgStats = rGames.GameStatsList.ElementAt(i);
                tempExists = db.gameStatsExists(pgStats.GameID);

                /* Check if it should add an old game */
                if (!tempExists)
                {
                    ConsoleOut("\t\tOld Game Added, id = " + pgStats.GameID);
                    db.addOldGame(pgStats, rGames.AccountID);
                }
                /* Check if it should skip rest of loop because older games should already be in database */
                else if (tempExists)
                {
                    break;
                }
            }
        }

        public void endPublicSummonerResponder(PublicSummoner summoner)
        {
            RecentGames games;
            PlayerLifetimeStats stats;

            endPSummoners.Add(summoner);

            if (summoner != null)
            {
                while ((games = pvpnet.getRecentGamesByAccount(summoner.AccountId)) == null) ;

                while ((stats = pvpnet.retrievePlayerStatsByAccount(summoner.AccountId, Seasons.CURRENT)) == null) ;

                endFellowRecentGamesResponder(games);
                endPlayerLifeStatsResponder(stats);
            }
        }


        public void endSummonerNamesResponder(string[] summNames)
        {
            PublicSummoner summoner;

            for (int ndx = 0; ndx < summNames.Count(); ndx++)
            {
                while ((summoner = pvpnet.getSummonerByName(summNames.ElementAt(ndx))) == null) ;

                endPublicSummonerResponder(summoner);
            }
        }

        public void endFellowRecentGamesResponder(RecentGames rGames)
        {
            int ndx;
            // Sort recent games by newest game first
            if (rGames == null)
                return;

            rGames.GameStatsList.Sort(CompareGames);

            for (ndx = 0; ndx < rGames.GameStatsList.Count; ndx++)
            {
                if (rGames.GameStatsList.ElementAt(ndx).GameID == reportingSummoner.lastGameId)
                {
                    endSummonerStats.Add(new EndSummonerGameStats(rGames.GameStatsList.ElementAt(ndx), rGames.AccountID));
                    break;
                }
            }

            if (ndx >= rGames.GameStatsList.Count)
            {
                return;
            }

            //ConsoleOut("\tGameUpdate: EndRecentGames");

            if (endLifeStats.Count >= endTotalPlayers && endSummonerStats.Count >= endTotalPlayers)
            {
                stopWaitHandle.Set();
            }
        }

        public void endPlayerLifeStatsResponder(PlayerLifetimeStats pStats)
        {
            endLifeStats.Add(pStats);

            //ConsoleOut("\tGameUpdate: EndLifeStats");
            if (endLifeStats.Count >= endTotalPlayers && endSummonerStats.Count >= endTotalPlayers)
            {
                stopWaitHandle.Set();
            }
        }

        #endregion

        #region Helper Methods

        private PlayerLifetimeStats findLifeStats(long acctId)
        {
            for (int i = 0; i < endLifeStats.Count; i++)
            {
                if (endLifeStats.ElementAt(i) != null && endLifeStats.ElementAt(i).AccountID == acctId)
                    return endLifeStats.ElementAt(i);
            }

            return null;
        }

        private PublicSummoner findPublicSummoner(long acctId)
        {
            for (int i = 0; i < endPSummoners.Count; i++)
            {
                if (endPSummoners.ElementAt(i) != null && endPSummoners.ElementAt(i).AccountId == acctId)
                    return endPSummoners.ElementAt(i);
            }

            return null;
        }

        private long findSummIdInPublicSumm(long acctId)
        {
            for (int i = 0; i < endPSummoners.Count; i++)
            {
                if (endPSummoners.ElementAt(i) != null && endPSummoners.ElementAt(i).AccountId == acctId)
                    return endPSummoners.ElementAt(i).SummonerId;
            }

            return 0;
        }

        private string findSummNameInPublicSumm(long acctId)
        {
            for (int i = 0; i < endPSummoners.Count; i++)
            {
                if (endPSummoners.ElementAt(i).AccountId == acctId)
                    return endPSummoners.ElementAt(i).InternalName;
            }

            return "";
        }

        private void ConsoleOut(string str)
        {
            string now;
            using (StreamWriter outfile = File.AppendText(logPath))
            {
                now = DateTime.Now.ToString("yy/MM/dd HH:mm:ss");
                outfile.WriteLine(str);
            }
        }

        private static double getUnixTimestamp()
        {
            DateTime epoch = new DateTime(1970, 1, 1).ToLocalTime();
            TimeSpan span = (DateTime.Now - epoch);

            return span.TotalSeconds;
        }

        private static bool waitForPvpnet(RiotConnect pvpnet)
        {
            int time = 0;

            while (!pvpnet.Connected && time < RECFREQ)
            {
                Thread.Sleep(1000);
                time++;
            }

            if (time >= RECFREQ)
                return false;

            return true;
        }

        private static int CompareGames(PlayerGameStats x, PlayerGameStats y)
        {
            return -x.CreateDate.CompareTo(y.CreateDate);
        }

        #endregion
    }
}
