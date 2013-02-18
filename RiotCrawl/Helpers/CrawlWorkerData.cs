using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using PVPNetConnect;

namespace RiotCrawl.Helpers
{
    public class CrawlWorkerData
    {
        public List<SummonerCrawler> summonerList;
        public Region region;
        public string loginUser;
        public string loginPass;
        public string endLoginUser;
        public string endLoginPass;

        public CrawlWorkerData(Region reg, List<SummonerCrawler> list, string user, string pass, string eUser, string ePass)
        {
            region = reg;
            summonerList = list;
            loginUser = user;
            loginPass = pass;
            endLoginUser = eUser;
            endLoginPass = ePass;
        }
    }
}
