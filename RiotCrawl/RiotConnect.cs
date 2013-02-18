using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;

using PVPNetConnect;
using PVPNetConnect.Assets;
using PVPNetConnect.RiotObjects;
using PVPNetConnect.RiotObjects.Summoner;
using PVPNetConnect.RiotObjects.Statistics;
using PVPNetConnect.RiotObjects.Game;

namespace RiotCrawl
{
    public class RiotConnect
    {
        #region Member Properties & Constructors

        public PVPNetConnection connection;

        public bool Connected;
        public string pvpnet_Username;
        public string pvpnet_Password;
        public Region pvpnet_Region;
        public string pvpnet_IpAddress;
        public string logPath;

        public object returnObject;
        public object[] returnArray;
        public AutoResetEvent callbackEvent;

        private const int MAXWAITMS = 1500;

        public RiotConnect(Region region, string username, string password)
        {
            Connected = false;
            pvpnet_Username = username;
            pvpnet_Password = password;
            pvpnet_Region = region;
            logPath = username + ".log";

            connection = new PVPNetConnection();
            connection.OnConnect += new PVPNetConnection.OnConnectHandler(connection_OnConnect);
            connection.OnLogin += new PVPNetConnection.OnLoginHandler(connection_OnLogin);
            connection.OnDisconnect += new PVPNetConnection.OnDisconnectHandler(connection_OnDisconnect);
            connection.OnLoginQueueUpdate += new PVPNetConnection.OnLoginQueueUpdateHandler(connection_OnLoginQueueUpdate);
            connection.OnError += new PVPNetConnection.OnErrorHandler(connection_OnError);

            connection.Connect(username, password, region, "3.01.");
        }

        #endregion

        #region Connection CallBacks

        public void connection_OnLoginQueueUpdate(object sender, int positionInLine)
        {
            ConsoleOut("Waiting in queue, position: " + positionInLine);
        }

        public void connection_OnError(object sender, Error error)
        {
            ConsoleOut("Error: " + error.Message);
        }

        public void connection_OnConnect(object sender, EventArgs eventArguments)
        {
            ConsoleOut("Successfully connected to PvPNet. Attempting login...");
        }

        public void connection_OnDisconnect(object sender, EventArgs eventArguments)
        {
            Connected = false;
            ConsoleOut("Disconnected from PvPNet: " + eventArguments.ToString());
        }

        public void connection_OnLogin(object sender, string username, string ipAddress)
        {
            if (sender == null)
                return;

            ConsoleOut("Login successful and connected to PvPNet.");
            Connected = true;
            pvpnet_IpAddress = ipAddress;
        }

        #endregion

        #region PvPNet Blocking Calls

        public AllPublicSummonerData getAllPublicSummonerDataByAccount(int accountID)
        {
            callbackEvent = new AutoResetEvent(false);

            connection.GetAllPublicSummonerDataByAccount(accountID, new AllPublicSummonerData.Callback((AllPublicSummonerData result) =>
            {
                returnObject = (object)result;
                callbackEvent.Set();
            }));

            callbackEvent.WaitOne(MAXWAITMS);

            try
            {
                return (AllPublicSummonerData) returnObject;
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                return null;
            }
        }

        public PublicSummoner getSummonerByName(string name)
        {
            callbackEvent = new AutoResetEvent(false);

            connection.GetSummonerByName(name, new PublicSummoner.Callback((PublicSummoner result) =>
            {
                returnObject = (object)result;
                callbackEvent.Set();
            }));

            callbackEvent.WaitOne(MAXWAITMS);

            try
            {
                return (PublicSummoner)returnObject;
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                return null;
            }
        }

        public PublicSummoner getSummonerBySummID(int summonerID)
        {
            callbackEvent = new AutoResetEvent(false);

            string[] name = getSummonerNames(new int[] { summonerID });

            connection.GetSummonerByName(name[0], new PublicSummoner.Callback((PublicSummoner result) =>
            {
                returnObject = (object)result;
                callbackEvent.Set();
            }));

            callbackEvent.WaitOne(MAXWAITMS);

            try
            {
                return (PublicSummoner)returnObject;
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                return null;
            }
        }

        public PlatformGameLifecycle getSpectatorGameByName(string name)
        {
            callbackEvent = new AutoResetEvent(false);

            connection.RetrieveInProgressSpectatorGameInfo(name, new PlatformGameLifecycle.Callback((PlatformGameLifecycle result) =>
            {
                    returnObject = (object)result;
                    callbackEvent.Set();
            }));

            callbackEvent.WaitOne(MAXWAITMS);

            try
            {
                return (PlatformGameLifecycle)returnObject;
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                return null;
            }
        }

        public RecentGames getRecentGamesByAccount(int accountID)
        {
            callbackEvent = new AutoResetEvent(false);

            connection.GetRecentGames(accountID, new RecentGames.Callback((RecentGames result) =>
            {
                returnObject = (object)result;
                callbackEvent.Set();
            }));

            callbackEvent.WaitOne(MAXWAITMS);

            try
            {
                return (RecentGames)returnObject;
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                return null;
            }
        }

        public AggregatedStats getAggregatedStatsByAccount(int accountID, GameModes gameMode, Seasons season)
        {
            callbackEvent = new AutoResetEvent(false);

            connection.GetAggregatedStats(accountID, gameMode, season, new AggregatedStats.Callback((AggregatedStats result) =>
            {
                returnObject = (object)result;
                callbackEvent.Set();
            }));

            callbackEvent.WaitOne(MAXWAITMS);

            try
            {
                return (AggregatedStats)returnObject;
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                return null;
            }
        }

        public PlayerLifetimeStats retrievePlayerStatsByAccount(int accountID, Seasons season)
        {
            callbackEvent = new AutoResetEvent(false);

            connection.RetrievePlayerStatsByAccountId(accountID, season, new PlayerLifetimeStats.Callback((PlayerLifetimeStats result) =>
            {
                returnObject = (object)result;
                callbackEvent.Set();
            }));

            callbackEvent.WaitOne(MAXWAITMS);

            try
            {
                return (PlayerLifetimeStats)returnObject;
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                return null;
            }
        }

        public string[] getSummonerNames(int[] summonerIDs)
        {
            callbackEvent = new AutoResetEvent(false);

            connection.GetSummonerNames(intToObjArray(summonerIDs), new SummonerNames.Callback((object[] result) =>
            {
                returnArray = result;
                callbackEvent.Set();
            }));

            callbackEvent.WaitOne(MAXWAITMS);

            try
            {
                return objToStringArray(returnArray);
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                return null;
            }
        }

        #endregion

        #region Private Helper Methods

        private void ConsoleOut(string str)
        {
            using (StreamWriter outfile = File.AppendText(logPath))
            {
                outfile.WriteLine(str);
            }
        }

        private object[] intToObjArray(int[] data)
        {
            object[] ret = ((IEnumerable)data).Cast<object>()
                                    .ToArray();

            return ret;
        }

        private string[] objToStringArray(object[] data)
        {
            string[] ret = ((IEnumerable)returnArray).Cast<object>()
                                    .Select(x => x.ToString())
                                    .ToArray();

            return ret;
        }

        #endregion
    }
}
