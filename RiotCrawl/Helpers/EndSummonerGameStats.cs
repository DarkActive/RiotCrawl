using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using PVPNetConnect.RiotObjects.Statistics;

namespace RiotCrawl.Helpers
{
    public class EndSummonerGameStats
    {
        public PlayerGameStats gameStats;
        public long accountId;

        public EndSummonerGameStats(PlayerGameStats pStats, long actId)
        {
            gameStats = pStats;
            accountId = actId;
        }
    }
}
