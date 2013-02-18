using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySql.Data;
using MySql.Data.MySqlClient;

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
    public class Database
    {
        public MySqlConnection conn;
        public bool connected;

        public Database()
        {
            connected = false;
            String myConnectionString = "server=198.167.239.201;"
                + "uid=wesa;"
                + "pwd=sfnLBKAuY57UhJtp;"
                + "database=lolcloud;";

            try
            {
                conn = new MySql.Data.MySqlClient.MySqlConnection();
                conn.ConnectionString = myConnectionString;
                conn.Open();
                connected = true;
            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                string s = ex.ToString();
                Console.WriteLine("Can not connect to LOLData DB, please try again later.");
                connected = false;
            }
        }

        public bool gameStatsExists(long gameId)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;

            cmd.CommandText = "SELECT COUNT(*) FROM games WHERE riotGameId = \"" + gameId + "\"";

            if (int.Parse(cmd.ExecuteScalar().ToString()) == 0)
                return false;

            return true;
        }

        public bool gameStatsIncomplete(long gameId)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;

            cmd.CommandText = "SELECT COUNT(*) FROM games WHERE riotGameId = \"" + gameId + "\" AND prematchInfo = \"1\"";

            if (int.Parse(cmd.ExecuteScalar().ToString()) == 0)
                return false;

            return true;
        }

        public int getTotalTrackedSummoners()
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;

            cmd.CommandText = "SELECT COUNT(*) FROM summoners WHERE track = \"1\"";

            return int.Parse(cmd.ExecuteScalar().ToString());
        }

        public bool getRiotLogins(ref List<RiotLogin> logins, ref List<RiotLogin> eLogins)
        {
            MySqlCommand cmd = new MySqlCommand();
            MySqlDataReader reader;
            cmd.Connection = conn;

            cmd.CommandText = "SELECT `username`, `password` FROM riotAccounts";
            reader = cmd.ExecuteReader();

            if (!reader.HasRows)
            {
                reader.Close();
                return false;
            }

            while (reader.Read())
            {
                logins.Add(new RiotLogin(reader.GetString(0), reader.GetString(1)));
                eLogins.Add(new RiotLogin("end" + reader.GetString(0), reader.GetString(1)));
            }
            reader.Close();

            return true;
        }

        public void removeLiveGame(long gameId)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;

            cmd.CommandText = "DELETE FROM liveGames WHERE gameId =\"" + gameId + "\"";
            cmd.ExecuteNonQuery();
        }

        public bool getTrackedSummoners(ref List<SummonerCrawler> summoners)
        {
            SummonerCrawler temp;
            MySqlCommand cmd = new MySqlCommand();
            MySqlDataReader reader;
            cmd.Connection = conn;

            cmd.CommandText = "SELECT `summoner_name`, `summoner_id`, `account_id` FROM summoners";
            reader = cmd.ExecuteReader();

            if (!reader.HasRows)
            {
                reader.Close();
                return false;
            }

            while (reader.Read())
            {
                temp = new SummonerCrawler(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2));
                summoners.Add(temp);
            }
            reader.Close();

            return true;
        }

        public bool getTrackedSummoners(ref List<SummonerCrawler> summoners, long startNdx)
        {
            SummonerCrawler temp;
            MySqlCommand cmd = new MySqlCommand();
            MySqlDataReader reader;
            cmd.Connection = conn;

            cmd.CommandText = "SELECT `summoner_name`, `summoner_id`, `account_id` FROM summoners WHERE id > \"" + startNdx + "\"";
            reader = cmd.ExecuteReader();

            if (!reader.HasRows)
            {
                reader.Close();
                return false;
            }

            while (reader.Read())
            {
                temp = new SummonerCrawler(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2));
                summoners.Add(temp);
            }
            reader.Close();

            return true;
        }

        private static int findChampPickByName(GameDTO game, string summName)
        {
            PlayerChampionSelection champSelection;
            champSelection = game.PlayerChampionSelections.Find(
                delegate(PlayerChampionSelection ch)
                {
                    return ch.SummonerInternalName == summName;
                }
                );

            return champSelection.ChampionID;
        }

        private bool isGameDraft(PlayerGameStats game)
        {
            if (game.QueueType == "RANKED_SOLO_5x5")
                return true;

            if (game.QueueType == "RANKED_TEAM_5x5")
                return true;

            if (game.QueueType == "RANKED_TEAM_3x3")
                return true;

            return false;
        }

        private bool isGameDraft(GameDTO game)
        {
            if (game.QueueTypeName == "RANKED_SOLO_5x5")
                return true;

            if (game.QueueTypeName == "RANKED_TEAM_5x5")
                return true;

            if (game.QueueTypeName == "RANKED_TEAM_3x3")
                return true;

            if (game.GameType == "PRACTICE_GAME" && (game.GameTypeConfigId == 2 || game.GameTypeConfigId == 6))
                return true;

            return false;
        }

        private bool isGameRanked(PlayerGameStats game)
        {
            if (game.QueueType == "RANKED_SOLO_5x5")
                return true;

            if (game.QueueType == "RANKED_TEAM_5x5")
                return true;

            if (game.QueueType == "RANKED_TEAM_3x3")
                return true;

            return false;
        }

        private bool isGameRanked(GameDTO game)
        {
            if (game.QueueTypeName == "RANKED_SOLO_5x5")
                return true;

            if (game.QueueTypeName == "RANKED_TEAM_5x5")
                return true;

            if (game.QueueTypeName == "RANKED_TEAM_3x3")
                return true;

            return false;
        }

        public void addInProgressGame(PlatformGameLifecycle specGame, int accountID)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;
            MySqlCommand eCmd = new MySqlCommand();
            eCmd.Connection = conn;
            MySqlDataReader reader;

            GameDTO game;
            int ndx, i;
            long[] teammate = new long[4];
            long[] opponent = new long[5];
            int[] bans = new int[6];
            int[] picks = new int[10];
            long[] pickOrder = new long[10];
            DateTime date = DateTime.Now;

            // Set GameDTO variable
            game = specGame.Game;

            // Do nothing if duplicate
            eCmd.CommandText = "SELECT count(*) FROM games WHERE riotGameId = \"" + game.ID + "\"";
            if (int.Parse(eCmd.ExecuteScalar().ToString()) != 0)
                return;

            // If game is draft mode, sort pick order by draft order
            if (isGameDraft(game))
            {
                // Find pick order and champ picks for BLUE TEAM
                int tmpNdx;
                int three = 0;
                int five = 0;
                for (i = 0; i < game.TeamOne.Count(); i++)
                {
                    if (game.TeamOne[i].PickTurn == 3)
                    {
                        tmpNdx = (game.TeamOne[i].PickTurn + (three++));
                        picks[tmpNdx] = findChampPickByName(game, game.TeamOne[i].SummonerInternalName);
                    }
                    else if (game.TeamOne[i].PickTurn == 5)
                    {
                        tmpNdx = (game.TeamOne[i].PickTurn + 2 + (five++));
                        picks[tmpNdx] = findChampPickByName(game, game.TeamOne[i].SummonerInternalName);
                    }
                    else
                    {
                        tmpNdx = (game.TeamOne[i].PickTurn - 1);
                        picks[tmpNdx] = findChampPickByName(game, game.TeamOne[i].SummonerInternalName);
                    }
                }
                // Find pick order and champ picks for PURPLE TEAM
                int two = 0;
                int four = 0;
                for (i = 0; i < game.TeamTwo.Count(); i++)
                {
                    if (game.TeamTwo[i].PickTurn == 2)
                    {
                        tmpNdx = (game.TeamTwo[i].PickTurn - 1 + (two++));
                        picks[tmpNdx] = findChampPickByName(game, game.TeamTwo[i].SummonerInternalName);
                    }
                    else if (game.TeamTwo[i].PickTurn == 4)
                    {
                        tmpNdx = (game.TeamTwo[i].PickTurn + 1 + (four++));
                        picks[tmpNdx] = findChampPickByName(game, game.TeamTwo[i].SummonerInternalName);
                    }
                    else
                    {
                        tmpNdx = (game.TeamTwo[i].PickTurn + 3);
                        picks[tmpNdx] = findChampPickByName(game, game.TeamTwo[i].SummonerInternalName);
                    }
                }
            }
            // If game is not draft mode sort normally
            else
            {
                for (i = 0; i < game.TeamOne.Count(); i++)
                {
                    if (i == 0)
                        picks[0] = findChampPickByName(game, game.TeamOne[i].SummonerInternalName);
                    if (i == 1)
                        picks[3] = findChampPickByName(game, game.TeamOne[i].SummonerInternalName);
                    if (i == 2)
                        picks[4] = findChampPickByName(game, game.TeamOne[i].SummonerInternalName);
                    if (i == 3)
                        picks[7] = findChampPickByName(game, game.TeamOne[i].SummonerInternalName);
                    if (i == 4)
                        picks[8] = findChampPickByName(game, game.TeamOne[i].SummonerInternalName);
                }

                for (i = 0; i < game.TeamTwo.Count(); i++)
                {
                    if (i == 0)
                        picks[1] = findChampPickByName(game, game.TeamTwo[i].SummonerInternalName);
                    if (i == 1)
                        picks[2] = findChampPickByName(game, game.TeamTwo[i].SummonerInternalName);
                    if (i == 2)
                        picks[5] = findChampPickByName(game, game.TeamTwo[i].SummonerInternalName);
                    if (i == 3)
                        picks[6] = findChampPickByName(game, game.TeamTwo[i].SummonerInternalName);
                    if (i == 4)
                        picks[9] = findChampPickByName(game, game.TeamTwo[i].SummonerInternalName);
                }
            }

            // Determine Ban Order
            for (ndx = 0; ndx < game.BannedChampions.Count(); ndx++)
            {
                bans[(game.BannedChampions[ndx].PickTurn - 1)] = game.BannedChampions[ndx].ChampionID;
            }

            // Insert into DB
            cmd.CommandText = "INSERT INTO games SET " +
                "riotGameId = \"" + game.ID + "\", " +
                "reportAccountId = \"" + accountID + "\", " +
                "gameMode = \"" + game.GameMode + "\", " +
                "gameType = \"" + game.GameType + "\", " +
                "gameTypeConfigId = \"" + game.GameTypeConfigId + "\", " +
                "gameMapId = \"" + game.MapId + "\", " +
                "createDate = \"" + date.ToString("yyyy-MM-dd HH:mm:ss") + "\", " +
                "subType = \"" + game.QueueTypeName + "\", " +
                "gameName = \"" + game.Name.Replace("\"", "") + "\", " +
                "prematchInfo = \"" + 1 + "\", " +
                "bluePlayer0 = \"" + (game.TeamOne.Count >= 1 ? game.TeamOne.ElementAt(0).SummonerID : 0) + "\", " +
                "bluePlayer1 = \"" + (game.TeamOne.Count >= 2 ? game.TeamOne.ElementAt(1).SummonerID : 0) + "\", " +
                "bluePlayer2 = \"" + (game.TeamOne.Count >= 3 ? game.TeamOne.ElementAt(2).SummonerID : 0) + "\", " +
                "bluePlayer3 = \"" + (game.TeamOne.Count >= 4 ? game.TeamOne.ElementAt(3).SummonerID : 0) + "\", " +
                "bluePlayer4 = \"" + (game.TeamOne.Count >= 5 ? game.TeamOne.ElementAt(4).SummonerID : 0) + "\", " +
                "purplePlayer0 = \"" + (game.TeamTwo.Count >= 1 ? game.TeamTwo.ElementAt(0).SummonerID : 0) + "\", " +
                "purplePlayer1 = \"" + (game.TeamTwo.Count >= 2 ? game.TeamTwo.ElementAt(1).SummonerID : 0) + "\", " +
                "purplePlayer2 = \"" + (game.TeamTwo.Count >= 3 ? game.TeamTwo.ElementAt(2).SummonerID : 0) + "\", " +
                "purplePlayer3 = \"" + (game.TeamTwo.Count >= 4 ? game.TeamTwo.ElementAt(3).SummonerID : 0) + "\", " +
                "purplePlayer4 = \"" + (game.TeamTwo.Count >= 5 ? game.TeamTwo.ElementAt(4).SummonerID : 0) + "\", " +
                "ban0 = \"" + bans[0] + "\", " +
                "ban1 = \"" + bans[1] + "\", " +
                "ban2 = \"" + bans[2] + "\", " +
                "ban3 = \"" + bans[3] + "\", " +
                "ban4 = \"" + bans[4] + "\", " +
                "ban5 = \"" + bans[5] + "\", " +
                "pick0 = \"" + picks[0] + "\", " +
                "pick1 = \"" + picks[1] + "\", " +
                "pick2 = \"" + picks[2] + "\", " +
                "pick3 = \"" + picks[3] + "\", " +
                "pick4 = \"" + picks[4] + "\", " +
                "pick5 = \"" + picks[5] + "\", " +
                "pick6 = \"" + picks[6] + "\", " +
                "pick7 = \"" + picks[7] + "\", " +
                "pick8 = \"" + picks[8] + "\", " +
                "pick9 = \"" + picks[9] + "\"";

            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO liveGames SET " +
                "gameId = \"" + game.ID + "\", " +
                "participantStatus = \"" + specGame.Game.StatusOfParticipants + "\", " +
                "observable = \"" + game.SpectatorsAllowed + "\", " +
                "observerDelay = \"" + game.SpectatorDelay + "\", " +
                "observerServerIp = \"" + specGame.PlayerCredentials.ObserverServerIP + "\", " +
                "observerServerPort = \"" + specGame.PlayerCredentials.ObserverServerPort + "\", " +
                "observerEncryptionKey = \"" + specGame.PlayerCredentials.ObserverEncryptionKey + "\", " +
                "createDate = \"" + date.ToString("yyyy-MM-dd HH:mm:ss") + "\"";

            cmd.ExecuteNonQuery();
        }

        public void addOldGame(PlayerGameStats game, long accountId)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;
            MySqlCommand eCmd = new MySqlCommand();
            eCmd.Connection = conn;
            int[] picks = new int[10];
            DateTime date = DateTime.Now;

            int i;
            int blueNdx;
            long[] blueTeam = new long[5];
            int purpleNdx;
            long[] purpleTeam = new long[5];

            blueNdx = 0;
            purpleNdx = 0;
            for (i = 0; i < game.FellowPlayers.Count; i++)
            {
                if (game.FellowPlayers.ElementAt(i).TeamID == 100)
                {
                    if (blueNdx == 0)
                        picks[0] = game.FellowPlayers.ElementAt(i).ChampionID;
                    if (blueNdx == 1)
                        picks[3] = game.FellowPlayers.ElementAt(i).ChampionID;
                    if (blueNdx == 2)
                        picks[4] = game.FellowPlayers.ElementAt(i).ChampionID;
                    if (blueNdx == 3)
                        picks[7] = game.FellowPlayers.ElementAt(i).ChampionID;
                    if (blueNdx == 4)
                        picks[8] = game.FellowPlayers.ElementAt(i).ChampionID;

                    blueTeam[blueNdx++] = game.FellowPlayers.ElementAt(i).SummonerID;
                }
                else
                {
                    if (purpleNdx == 0)
                        picks[1] = game.FellowPlayers.ElementAt(i).ChampionID;
                    if (purpleNdx == 1)
                        picks[2] = game.FellowPlayers.ElementAt(i).ChampionID;
                    if (purpleNdx == 2)
                        picks[5] = game.FellowPlayers.ElementAt(i).ChampionID;
                    if (purpleNdx == 3)
                        picks[6] = game.FellowPlayers.ElementAt(i).ChampionID;
                    if (purpleNdx == 4)
                        picks[9] = game.FellowPlayers.ElementAt(i).ChampionID;

                    purpleTeam[purpleNdx++] = game.FellowPlayers.ElementAt(i).SummonerID;
                }
            }

            // Insert into DB
            cmd.CommandText = "INSERT INTO games SET " +
                "riotGameId = \"" + game.GameID + "\", " +
                "reportAccountId = \"" + accountId + "\", " +
                "gameMode = \"" + game.GameMode + "\", " +
                "gameType = \"" + game.GameType + "\", " +
                "gameTypeConfigId = \"" + 1 + "\", " +
                "gameMapId = \"" + game.GameMapID + "\", " +
                "createDate = \"" + date.ToString("yyyy-MM-dd HH:mm:ss") + "\", " +
                "subType = \"" + game.QueueType + "\", " +
                "gameName = \"" + "oldgame" + "\", " +
                "prematchInfo = \"" + 0 + "\", " +
                "bluePlayer0 = \"" + blueTeam[0] + "\", " +
                "bluePlayer1 = \"" + blueTeam[1] + "\", " +
                "bluePlayer2 = \"" + blueTeam[2] + "\", " +
                "bluePlayer3 = \"" + blueTeam[3] + "\", " +
                "bluePlayer4 = \"" + blueTeam[4] + "\", " +
                "purplePlayer0 = \"" + purpleTeam[0] + "\", " +
                "purplePlayer1 = \"" + purpleTeam[0] + "\", " +
                "purplePlayer2 = \"" + purpleTeam[0] + "\", " +
                "purplePlayer3 = \"" + purpleTeam[0] + "\", " +
                "purplePlayer4 = \"" + purpleTeam[0] + "\", " +
                "pick0 = \"" + picks[0] + "\", " +
                "pick1 = \"" + picks[1] + "\", " +
                "pick2 = \"" + picks[2] + "\", " +
                "pick3 = \"" + picks[3] + "\", " +
                "pick4 = \"" + picks[4] + "\", " +
                "pick5 = \"" + picks[5] + "\", " +
                "pick6 = \"" + picks[6] + "\", " +
                "pick7 = \"" + picks[7] + "\", " +
                "pick8 = \"" + picks[8] + "\", " +
                "pick9 = \"" + picks[9] + "\"";

            // Do nothing if duplicate
            eCmd.CommandText = "SELECT count(*) FROM games WHERE riotGameId = \"" + game.GameID + "\"";
            if (int.Parse(eCmd.ExecuteScalar().ToString()) != 0)
                return;

            cmd.ExecuteNonQuery();

            GameResult gameRes = new GameResult(game);
            addOldGameStats(game.GameID, accountId, game, gameRes);
        }

        public void updateEndGame(long gameId, long accountId)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;

            DateTime time = DateTime.Now;

            cmd.CommandText = "UPDATE games SET " +
                "reportAccountId = \"" + accountId + "\", " +
                "endDate = \"" + time.ToString("yyyy-MM-dd HH:mm:ss") + "\", " +
                "prematchInfo = \"" + 0 + "\" WHERE riotGameId = \"" + gameId + "\"";

            cmd.ExecuteNonQuery();
        }

        public void addGameStats(long accountId, PlayerGameStats pStats, GameResult stats, PlayerLifetimeStats lifeStats, PublicSummoner pSumm)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;
            MySqlDataReader reader;
            MySqlCommand eCmd = new MySqlCommand();
            eCmd.Connection = conn;
            string neutralMinions, turretsDest, inhibsDest;

            int oldElo = 0, eloChange = 0, newElo = 0;

            // Get Current Elo of Ranked games
            if (isGameRanked(pStats) && lifeStats != null)
            {
                if (pStats.QueueType == "RANKED_SOLO_5x5")
                    eCmd.CommandText = "SELECT `solo5x5_elo` FROM summoners WHERE account_id = \"" + accountId + "\" LIMIT 1";
                else if (pStats.QueueType == "RANKED_TEAM_5x5")
                    eCmd.CommandText = "SELECT `team5x5_elo` FROM summoners WHERE account_id = \"" + accountId + "\" LIMIT 1";
                else if (pStats.QueueType == "RANKED_TEAM_3x3")
                    eCmd.CommandText = "SELECT `team3x3_elo` FROM summoners WHERE account_id = \"" + accountId + "\" LIMIT 1";

                reader = eCmd.ExecuteReader();

                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        oldElo = reader.GetInt32(0);
                    }
                }
                else
                {
                    oldElo = 0;
                }
                reader.Close();

                //newElo = findPlayerStatSummaryType(lifeStats.PlayerStatSummaries.PlayerStatSummaryList, pStats.QueueType);

                if (oldElo == 400)
                    oldElo = 0;

                if (newElo == 400)
                    newElo = 0;

                if (oldElo != 0 && newElo != 0)
                {
                    eloChange = newElo - oldElo;
                }
            }

            if (stats.TurretsDestroyed == null)
                turretsDest = "0";
            else
                turretsDest = stats.TurretsDestroyed.ToString();

            if (stats.InhibitorsDestroyed == null)
                inhibsDest = "0";
            else
                inhibsDest = stats.InhibitorsDestroyed.ToString();

            if (stats.NeutralMinionsKilled == null)
                neutralMinions = "0";
            else
                neutralMinions = stats.NeutralMinionsKilled.ToString();

            cmd.CommandText = "INSERT INTO gamestats SET " +
                "riotGameId = \"" + pStats.GameID + "\", " +
                "accountId = \"" + accountId + "\", " +
                "championId = \"" + pStats.ChampionID + "\", " +
                "skinId = \"" + pStats.SkinIndex + "\", " +
                "spell1Id = \"" + pStats.SummSpellD + "\", " +
                "spell2Id = \"" + pStats.SummSpellF + "\", " +
                "summonerLevel = \"" + pStats.Level + "\", " +
                "ipEarned = \"" + pStats.IPEarned + "\", " +
                "boostIpEarned = \"" + pStats.BoostIPEarned + "\", " +
                "xpEarned = \"" + pStats.ExperienceEarned + "\", " +
                "boostXpEarned = \"" + pStats.BoostXPEarned + "\", " +
                "premadeSize = \"" + pStats.PremadeSize + "\", " +
                "createDate = \"" + pStats.CreateDate.ToString("yyyy-MM-dd HH:mm") + "\", " +
                "afk = \"" + (pStats.IsAFK ? "1" : "0") + "\", " +
                "leaver = \"" + (pStats.IsLeaver ? "1" : "0") + "\", " +
                "invalid = \"" + (pStats.IsInvalid ? "1" : "0") + "\", " +
                "win = \"" + (stats.Win ? "1" : "0") + "\", " +               // FIX
                "ranked = \"" + (pStats.IsRanked ? "1" : "0") + "\", " +
                "oldElo = \"" + oldElo + "\", " +
                "newElo = \"" + newElo + "\", " +
                "eloChange = \"" + eloChange + "\", " +
                "serverPing = \"" + pStats.UserServerPing + "\", " +
                "kills = \"" + stats.Kills + "\", " +
                "deaths = \"" + stats.Deaths + "\", " +
                "assists = \"" + stats.Assists + "\", " +
                "level = \"" + stats.Level + "\", " +
                "minionsKilled = \"" + stats.MinionsKilled + "\", " +
                "neutralMinionsKilled = \"" + neutralMinions + "\", " +
                "goldEarned = \"" + stats.GoldEarned + "\", " +
                "magicDamageDealt = \"" + stats.MagicalDamageDealt + "\", " +
                "physicalDamageDealt = \"" + stats.PhysicalDamageDealt + "\", " +
                "totalDamageDealt = \"" + stats.TotalDamageDealt + "\", " +
                "magicDamageTaken = \"" + stats.MagicalDamageTaken + "\", " +
                "physicalDamageTaken = \"" + stats.PhysicalDamageTaken + "\", " +
                "totalDamageTaken = \"" + stats.TotalDamageTaken + "\", " +
                "totalHealingDone = \"" + stats.TotalHealingDone + "\", " +
                "largestMultiKill = \"" + stats.LargestMultiKill + "\", " +
                "largestKillingSpree = \"" + stats.LargestKillingSpree + "\", " +
                "timeSpentDead = \"" + stats.TimeSpentDead + "\", " +
                "turretsDestroyed = \"" + turretsDest + "\", " +
                "inhibitorsDestroyed = \"" + inhibsDest + "\", " +
                "item0 = \"" + stats.Items[0] + "\", " +
                "item1 = \"" + stats.Items[1] + "\", " +
                "item2 = \"" + stats.Items[2] + "\", " +
                "item3 = \"" + stats.Items[3] + "\", " +
                "item4 = \"" + stats.Items[4] + "\", " +
                "item5 = \"" + stats.Items[5] + "\"";

            cmd.ExecuteNonQuery();

            updateSummonerStats(lifeStats, pSumm);
        }

        public void addSummoner(long summonerId, long accountId, string summonerName)
        {
            MySqlCommand cmd = new MySqlCommand();
            MySqlCommand cCmd = new MySqlCommand();
            MySqlDataReader read;
            cmd.Connection = conn;
            cCmd.Connection = conn;

            cCmd.CommandText = "SELECT * FROM summoners WHERE account_id = \"" + accountId + "\"";
            read = cCmd.ExecuteReader();

            if (read.HasRows)
            {
                read.Close();
                return;
            }
            read.Close();

            cmd.CommandText = "INSERT INTO summoners SET " +
                "summoner_id = \"" + summonerId + "\", " +
                "account_id = \"" + accountId + "\", " +
                "summoner_name = \"" + summonerName + "\"";

            cmd.ExecuteNonQuery();
        }

        public void updateSummonerStats(PlayerLifetimeStats lifeStats, PublicSummoner pSumm)
        {
            MySqlDataReader reader;
            MySqlCommand eCmd = new MySqlCommand();
            eCmd.Connection = conn;
            long oldSummId = 0;

            long accountId = pSumm.AccountId;
            long summonerId = pSumm.SummonerId;

            eCmd.CommandText = "SELECT `summoner_id` FROM summoners WHERE account_id = \"" + accountId + "\" LIMIT 1";

            reader = eCmd.ExecuteReader();

            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    oldSummId = reader.GetInt32(0);
                }

                if (oldSummId != summonerId)
                {
                    reader.Close();
                    updateSummoner("summoner_id", summonerId.ToString(), accountId);
                    updateSummonerId(oldSummId.ToString(), accountId);
                }
                else
                {
                    reader.Close();
                }
            }
            // Add summoner who is non existant in DB
            else
            {
                reader.Close();
                addSummoner(summonerId, accountId, pSumm.InternalName);
            }

            updateSummoner("summonerLevel", pSumm.SummonerLevel.ToString(), accountId);
            updateSummoner("profileIconId", pSumm.ProfileIconId.ToString(), accountId);
        }

        public List<long> getFellowPlayersIds(long gameId, long summonerId)
        {
            List<long> players = new List<long>();
            MySqlCommand cmd = new MySqlCommand();
            MySqlDataReader reader;
            cmd.Connection = conn;

            cmd.CommandText = "SELECT `teammate1`, `teammate2`, `teammate3`, `teammate4`, `opponent0`, `opponent1`, `opponent2`, `opponent3`, `opponent4` FROM games WHERE " +
                "riotGameId = \"" + gameId + "\" AND summonerId = \"" + summonerId + "\"";
            reader = cmd.ExecuteReader();
            reader.Read();

            if (reader.GetInt64(0) != 0)
                players.Add(reader.GetInt64(0));

            if (reader.GetInt64(1) != 0)
                players.Add(reader.GetInt64(1));

            if (reader.GetInt64(2) != 0)
                players.Add(reader.GetInt64(2));

            if (reader.GetInt64(3) != 0)
                players.Add(reader.GetInt64(3));

            if (reader.GetInt64(4) != 0)
                players.Add(reader.GetInt64(4));

            if (reader.GetInt64(5) != 0)
                players.Add(reader.GetInt64(5));

            if (reader.GetInt64(6) != 0)
                players.Add(reader.GetInt64(6));

            if (reader.GetInt64(7) != 0)
                players.Add(reader.GetInt64(7));

            if (reader.GetInt64(8) != 0)
                players.Add(reader.GetInt64(8));

            reader.Close();

            return players;
        }

        private static PlayerStatSummary findPlayerStatSummaryType(List<PlayerStatSummary> summaries, string subType)
        {
            PlayerStatSummary psSummary;

            char[] removeChar = { '_' };
            string findSubType = subType.Replace("_", "");

            psSummary = summaries.Find(
                delegate(PlayerStatSummary pss)
                {
                    return pss.QueueType.ToUpper() == findSubType.ToUpper();
                }
            );

            return psSummary;
        }

        public void addOldGameStats(long rGameId, long accountId, PlayerGameStats pStats, GameResult stats)
        {
            MySqlCommand cmd = new MySqlCommand();

            string turretsDest;
            string inhibsDest;
            string neutralMinions;

            if (stats.TurretsDestroyed == null)
                turretsDest = "0";
            else
                turretsDest = stats.TurretsDestroyed.ToString();

            if (stats.InhibitorsDestroyed == null)
                inhibsDest = "0";
            else
                inhibsDest = stats.InhibitorsDestroyed.ToString();

            if (stats.NeutralMinionsKilled == null)
                neutralMinions = "0";
            else
                neutralMinions = stats.NeutralMinionsKilled.ToString();

            cmd.Connection = conn;
            cmd.CommandText = "INSERT INTO gamestats SET " +
                "riotGameId = \"" + rGameId + "\", " +
                "accountId = \"" + accountId + "\", " +
                "championId = \"" + pStats.ChampionID + "\", " +
                "skinId = \"" + pStats.SkinIndex + "\", " +
                "spell1Id = \"" + pStats.SummSpellD + "\", " +
                "spell2Id = \"" + pStats.SummSpellF + "\", " +
                "summonerLevel = \"" + pStats.Level + "\", " +
                "ipEarned = \"" + pStats.IPEarned + "\", " +
                "boostIpEarned = \"" + pStats.BoostIPEarned + "\", " +
                "xpEarned = \"" + pStats.ExperienceEarned + "\", " +
                "boostXpEarned = \"" + pStats.BoostXPEarned + "\", " +
                "premadeSize = \"" + pStats.PremadeSize + "\", " +
                "createDate = \"" + pStats.CreateDate.ToString("yyyy-MM-dd HH:mm") + "\", " +
                "afk = \"" + (pStats.IsAFK ? "1" : "0") + "\", " +
                "leaver = \"" + (pStats.IsLeaver ? "1" : "0") + "\", " +
                "invalid = \"" + (pStats.IsInvalid ? "1" : "0") + "\", " +
                "win = \"" + (stats.Win ? "1" : "0") + "\", " +               // FIX
                "ranked = \"" + (pStats.IsRanked ? "1" : "0") + "\", " +
                "oldElo = \"" + 0 + "\", " +
                "newElo = \"" + 0 + "\", " +
                "eloChange = \"" + 0 + "\", " +
                "serverPing = \"" + pStats.UserServerPing + "\", " +
                "kills = \"" + stats.Kills + "\", " +
                "deaths = \"" + stats.Deaths + "\", " +
                "assists = \"" + stats.Assists + "\", " +
                "level = \"" + stats.Level + "\", " +
                "minionsKilled = \"" + stats.MinionsKilled + "\", " +
                "neutralMinionsKilled = \"" + neutralMinions + "\", " +
                "goldEarned = \"" + stats.GoldEarned + "\", " +
                "magicDamageDealt = \"" + stats.MagicalDamageDealt + "\", " +
                "physicalDamageDealt = \"" + stats.PhysicalDamageDealt + "\", " +
                "totalDamageDealt = \"" + stats.TotalDamageDealt + "\", " +
                "magicDamageTaken = \"" + stats.MagicalDamageTaken + "\", " +
                "physicalDamageTaken = \"" + stats.PhysicalDamageTaken + "\", " +
                "totalDamageTaken = \"" + stats.TotalDamageTaken + "\", " +
                "totalHealingDone = \"" + stats.TotalHealingDone + "\", " +
                "largestMultiKill = \"" + stats.LargestMultiKill + "\", " +
                "largestKillingSpree = \"" + stats.LargestKillingSpree + "\", " +
                "timeSpentDead = \"" + stats.TimeSpentDead + "\", " +
                "turretsDestroyed = \"" + turretsDest + "\", " +
                "inhibitorsDestroyed = \"" + inhibsDest + "\", " +
                "item0 = \"" + stats.Items[0] + "\", " +
                "item1 = \"" + stats.Items[1] + "\", " +
                "item2 = \"" + stats.Items[2] + "\", " +
                "item3 = \"" + stats.Items[3] + "\", " +
                "item4 = \"" + stats.Items[4] + "\", " +
                "item5 = \"" + stats.Items[5] + "\"";

            cmd.ExecuteNonQuery();
        }

        #region Private Update Methods

        public void addSummonerId(string sumName, long sumId, long acctId)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;
            cmd.CommandText = "UPDATE summoners SET summoner_id = \"" + sumId + "\", account_id = \"" + acctId + "\" WHERE summoner_name = \"" + sumName + "\"";
            cmd.ExecuteNonQuery();
        }

        public void updateSummoner(string column, string value, long accountId)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;
            cmd.CommandText = "UPDATE summoners SET " + column + " = \"" + value + "\" WHERE account_id = \"" + accountId + "\"";
            cmd.ExecuteNonQuery();
        }

        public void updateSummonerId(string value, long accountId)
        {
            MySqlCommand eCmd = new MySqlCommand();
            MySqlDataReader reader;
            eCmd.Connection = conn;
            string temp;

            eCmd.CommandText = "SELECT `oldSummoner_ids` FROM summoners WHERE account_id = \"" + accountId + "\" LIMIT 1";

            reader = eCmd.ExecuteReader();
            reader.Read();

            temp = reader.GetString(0) + ";" + value;

            reader.Close();

            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;
            cmd.CommandText = "UPDATE summoners SET oldSummoner_ids = \"" + temp + "\" WHERE account_id = \"" + accountId + "\"";
            cmd.ExecuteNonQuery();
        }

        public List<SummonerCrawler> findPreGames(int accountId, int summonerId, string summonerName)
        {
            MySqlCommand eCmd = new MySqlCommand();
            MySqlDataReader reader;
            eCmd.Connection = conn;
            SummonerCrawler temp;
            List<SummonerCrawler> retList = new List<SummonerCrawler>();

            eCmd.CommandText = "SELECT `riotGameId` FROM games WHERE reportAccountId = \"" + accountId + "\" AND prematchInfo = \"1\"";

            reader = eCmd.ExecuteReader();

            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    temp = new SummonerCrawler(summonerName, summonerId, accountId, reader.GetInt32(0));
                    retList.Add(temp);
                }
            }

            reader.Close();

            return retList;
        }

        public void removePreGame(long gameId)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;

            cmd.CommandText = "DELETE FROM games WHERE riotGameId = \"" + gameId + "\"";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "DELETE FROM gamestats WHERE riotGameId = \"" + gameId + "\"";
            cmd.ExecuteNonQuery();
        }

        private void updateSummonerById(string column, string value, long id)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;
            cmd.CommandText = "UPDATE summoners SET " + column + " = \"" + value + "\" WHERE id = \"" + id + "\"";
            cmd.ExecuteNonQuery();
        }

        public void updateSummonerByName(string column, string value, string sumName)
        {
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = conn;
            cmd.CommandText = "UPDATE summoners SET " + column + " = \"" + value + "\" WHERE summoner_name = \"" + sumName + "\"";
            cmd.ExecuteNonQuery();
        }

        public List<string> getAllSummonerNames()
        {
            MySqlDataReader reader;
            MySqlCommand eCmd = new MySqlCommand();
            List<string> summoners = new List<string>();

            eCmd.Connection = conn;

            eCmd.CommandText = "SELECT `summoner_name` FROM summoners";

            reader = eCmd.ExecuteReader();

            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    summoners.Add(reader.GetString(0));
                }
            }
            reader.Close();

            return summoners;
        }

        #endregion
    }
}