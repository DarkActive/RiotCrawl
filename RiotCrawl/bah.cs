using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using PVPNetConnect;
using PVPNetConnect.Assets;

/* Add the namespaces as needed depending on riot objects needed. */
using PVPNetConnect.RiotObjects.Summoner;
using PVPNetConnect.RiotObjects.Game;
using PVPNetConnect.RiotObjects.Statistics;
using PVPNetConnect.RiotObjects.Leagues;
using PVPNetConnect.RiotObjects.Client;
using PVPNetConnect.RiotObjects.Catalog;

namespace RiotCrawl
{
    class Program
    {
        static void Main(string[] args)
        {
            PVPNetConnection connection = new PVPNetConnection();
            connection.OnConnect += new PVPNetConnection.OnConnectHandler(connection_OnConnect);
            connection.OnLogin += new PVPNetConnection.OnLoginHandler(connection_OnLogin);
            connection.OnDisconnect += new PVPNetConnection.OnDisconnectHandler(connection_OnDisconnect);
            connection.OnLoginQueueUpdate += new PVPNetConnection.OnLoginQueueUpdateHandler(connection_OnLoginQueueUpdate);
            connection.OnError += new PVPNetConnection.OnErrorHandler(connection_OnError);

            connection.Connect("**USERNAME_HERE**", "**PASSWORD_HERE**", Region.NA, "3.01.");
        }

        public static void connection_OnLogin(object sender, string username, string ipAddress)
        {
            /* Now logged in and can perform calls */
            PVPNetConnection connection = (PVPNetConnection) sender;

            PublicSummoner pubSummoner;

            /* This is an async call, have to make sure pubSummoner is set before you read it */
            connection.GetSummonerByName("TheOddOne", new PublicSummoner.Callback((PublicSummoner result) =>
            {
                pubSummoner = result;
            }));

            /* To do a blocking call (non async), meaning you want to use the return value right away
             * create/use a similar method like the following:
             */
            getSummonerByName(connection, "TheOddOne");
        }

        public static PublicSummoner getSummonerByName(PVPNetConnection connection, string name)
        {
            AutoResetEvent callbackEvent = new AutoResetEvent(false);
            PublicSummoner summ = null;
            const int MAXWAITMS = 3000;

            connection.GetSummonerByName(name, new PublicSummoner.Callback((PublicSummoner result) =>
            {
                summ = result;
                callbackEvent.Set();
            }));

            callbackEvent.WaitOne(MAXWAITMS);

            return summ;
        }

        public static void connection_OnLoginQueueUpdate(object sender, int positionInLine)
        {
            Console.WriteLine("Waiting in queue, position: " + positionInLine);
        }

        public static void connection_OnError(object sender, Error error)
        {
            Console.WriteLine("Error: " + error.Message);
        }

        public static void connection_OnConnect(object sender, EventArgs eventArguments)
        {
            Console.WriteLine("Successfully connected to PvPNet. Attempting login...");
        }

        public static void connection_OnDisconnect(object sender, EventArgs eventArguments)
        {
            Console.WriteLine("Disconnected from PvPNet: " + eventArguments.ToString());
        }
    }
}