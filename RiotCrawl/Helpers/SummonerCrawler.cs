using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RiotCrawl.Helpers
{
    public class SummonerCrawler : ICloneable
    {
        public int summonerId;
        public int accountId;
        public string summonerName;
        public double lastUpdate;
        public int gameStatus;
        public int lastGameId;
        public double lastGameCheck;

        /* Constants */
        private const int OUTOFGAME = 0;
        private const int INGAME = 1;
        private const int GAMEDONE = 2;

        public SummonerCrawler(string sumName, int actid)
        {
            summonerId = 0;
            accountId = actid;
            summonerName = sumName;
            lastUpdate = 0;
            gameStatus = OUTOFGAME;
            lastGameCheck = 0;
            lastGameId = 0;
        }

        public SummonerCrawler(string sumName, int sumId, int actId)
        {
            summonerId = sumId;
            accountId = actId;
            summonerName = sumName;
            lastUpdate = 0;
            gameStatus = OUTOFGAME;
            lastGameCheck = 0;
            lastGameId = 0;
        }

        public SummonerCrawler(string sumName, int sumId, int actId, int gameId)
        {
            summonerId = sumId;
            accountId = actId;
            summonerName = sumName;
            lastUpdate = 0;
            gameStatus = OUTOFGAME;
            lastGameCheck = 0;
            lastGameId = gameId;
        }

        public object Clone()
        {
            SummonerCrawler newSCrawler = new SummonerCrawler(this.summonerName, this.summonerId, this.accountId);

            newSCrawler.gameStatus = this.gameStatus;
            newSCrawler.lastGameId = this.lastGameId;
            newSCrawler.lastUpdate = this.lastUpdate;
            newSCrawler.lastGameCheck = this.lastGameCheck;

            return newSCrawler;
        }
    }
}
