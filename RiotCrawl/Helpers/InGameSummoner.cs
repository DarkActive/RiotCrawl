using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RiotCrawl.Helpers
{
    public class InGameSummoner
    {
        public SummonerCrawler summonerCr;
        public bool gameCompleted;
        public bool timedOut;

        public InGameSummoner(SummonerCrawler sCr)
        {
            summonerCr = sCr;
            gameCompleted = false;
            timedOut = false;
        }

        public InGameSummoner(SummonerCrawler sCr, bool gameComp)
        {
            summonerCr = sCr;
            gameCompleted = gameComp;
            timedOut = true;
        }
    }
}