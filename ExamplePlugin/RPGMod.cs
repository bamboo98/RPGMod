using BepInEx;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RPGMod
{
    // Quest Message that gets sent to all clients
    public class QuestMessage : MessageBase
    {
        public bool Initialised;
        public string Description;
        public string Target;
        public string TargetName;

        public override void Deserialize(NetworkReader reader)
        {
            Initialised = reader.ReadBoolean();
            Description = reader.ReadString();
            Target = reader.ReadString();
            TargetName = reader.ReadString();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(Initialised);
            writer.Write(Description);
            writer.Write(Target);
            writer.Write(TargetName);
        }
    }

    // All server side data
    public struct ServerQuestData
    {
        public PickupIndex Drop;
        public int Objective;
        public int Progress;
    }

    [BepInPlugin("com.ghasttear1.rpgmod", "RPGMod", "1.1.2")]

    public class RPGMod : BaseUnityPlugin
    {
       
        // Misc params
        public System.Random random = new System.Random();
        public SpawnCard chest2 = Resources.Load<SpawnCard>("SpawnCards/InteractableSpawnCard/iscchest2");
        public GameObject targetBody;
        public bool isLoaded = false;
        public bool isDebug = false;
        public bool questFirst = true;
        public bool isSuicide = false;
        public String[] bannedDirectorSpawns;

        // Networking params
        public short msgQuestDrop = 1337;
        public bool isClientRegistered = false;
        public QuestMessage questMessage = new QuestMessage();
        public ServerQuestData serverQuestData;

        // Chance params
        public float chanceNormal;
        public float chanceElite;
        public float chanceBoss;
        public float bossChestChanceLegendary;
        public float bossChestChanceUncommon;
        public float chanceQuestingCommon;
        public float chanceQuestingUnCommon;
        public float chanceQuestingLegendary;
        public float dropsPlayerScaling;
        public float eliteChanceTier1;
        public float eliteChanceTier2;
        public float eliteChanceTier3;
        public float eliteChanceTierLunar;
        public float normalChanceTier1;
        public float normalChanceTier2;
        public float normalChanceTier3;
        public float normalChanceTierEquip;
        public float gameStartScaling;

        // UI params
        public Notification Notification { get; set; }
        public int screenPosX;
        public int screenPosY;
        //public int titleFontSize;
        //public int descriptionFontSize;
        public int sizeX;
        public int sizeY;
        public bool resetUI = false;
        public bool Persistent = true;
        public CharacterBody CachedCharacterBody;

        // Questing params
        public int questObjectiveFactor;
        public int questObjectiveLimit;
        public bool itemDroppingFromPlayers;
        public bool questInChat;
        public List<string> questList = new List<string>() { "<b>Kill</b>", "<b>Eliminate</b>" };
        public int questIndex;
        public bool stageChange = false;

        // Feature params
        public bool isChests;
        public bool isBossChests;
        public bool isEnemyDrops;
        public bool isQuesting;
        public bool isQuestResetting;

        // Refreshes the config values from the config
        public void RefreshConfigValues(bool initLoad)
        {
            if (!initLoad)
            {
                Config.Reload();
            }

            // Chances
            chanceNormal = ConfigToFloat(Config.Wrap("Chances", "chanceNormal", "Base chance for a normal enemy to drop an item (float)", "9.5").Value);
            chanceElite = ConfigToFloat(Config.Wrap("Chances", "chanceElite", "Base chance for an elite enemy to drop an item (float)", "11.0").Value);
            chanceBoss = ConfigToFloat(Config.Wrap("Chances", "chanceBoss", "Base chance for a boss enemy to drop an item (float)", "35.0").Value);
            bossChestChanceLegendary = ConfigToFloat(Config.Wrap("Chances", "bossChestChanceLegendary", "Chance for a legendary to drop from a boss chest (float)", "0.3").Value);
            bossChestChanceUncommon = ConfigToFloat(Config.Wrap("Chances", "bossChestChanceUncommon", "Chance for a uncommon to drop from a boss chest (float)", "0.7").Value);
            chanceQuestingCommon = ConfigToFloat(Config.Wrap("Chances", "chanceQuestingCommon", "Chance for quest drop to be common (float)", "0").Value);
            chanceQuestingUnCommon = ConfigToFloat(Config.Wrap("Chances", "chanceQuestingUnCommon", "Chance for quest drop to be uncommon (float)", "0.85").Value);
            chanceQuestingLegendary = ConfigToFloat(Config.Wrap("Chances", "chanceQuestingLegendary", "Chance for quest drop to be legendary (float)", "0.15").Value);
            dropsPlayerScaling = ConfigToFloat(Config.Wrap("Chances", "dropsPlayerScaling", "Scaling per player (drop chance percentage increase per player) (float)", "0.35").Value);
            eliteChanceTier1 = ConfigToFloat(Config.Wrap("Chances", "eliteChanceTier1", "Chance for elite to drop a tier 1 item (float)", "0.45").Value);
            eliteChanceTier2 = ConfigToFloat(Config.Wrap("Chances", "eliteChanceTier2", "Chance for elite to drop a tier 2 item (float)", "0.2").Value);
            eliteChanceTier3 = ConfigToFloat(Config.Wrap("Chances", "eliteChanceTier3", "Chance for elite to drop a tier 3 item (float)", "0.1").Value);
            eliteChanceTierLunar = ConfigToFloat(Config.Wrap("Chances", "eliteChanceTierLunar", "Chance for elite to drop a lunar item (float)", "0.1").Value);
            normalChanceTier1 = ConfigToFloat(Config.Wrap("Chances", "normalChanceTier1", "Chance for normal enemy to drop a tier 1 item (float)", "0.9").Value);
            normalChanceTier2 = ConfigToFloat(Config.Wrap("Chances", "normalChanceTier2", "Chance for normal enemy to drop a tier 2 item (float)", "0.1").Value);
            normalChanceTier3 = ConfigToFloat(Config.Wrap("Chances", "normalChanceTier3", "Chance for normal enemy to drop a tier 3 item (float)", "0.01").Value);
            normalChanceTierEquip = ConfigToFloat(Config.Wrap("Chances", "normalChanceTierEquip", "Chance for normal enemy to drop equipment (float)", "0.1").Value);
            gameStartScaling = ConfigToFloat(Config.Wrap("Chances", "gameStartScaling", "Scaling of chances for the start of the game, that goes away during later stages (float)", "1.5").Value);

            // UI params
            screenPosX = Config.Wrap("UI", "Screen Pos X", "UI location on the x axis (percentage of screen width) (int)", 89).Value;
            screenPosY = Config.Wrap("UI", "Screen Pos Y", "UI location on the y axis (percentage of screen height) (int)", 50).Value;
            //titleFontSize = Config.Wrap("UI", "Title Font Size", "UI title font size (int)", 18).Value;
            //descriptionFontSize = Config.Wrap("UI", "Description Font Size", "UI description font size (int)", 14).Value;
            sizeX = Config.Wrap("UI", "Size X", "Size of UI on the x axis (pixels)", 300).Value;
            sizeY = Config.Wrap("UI", "Size Y", "Size of UI on the x axis (pixels) (int)", 80).Value;

            // Questing params
            questObjectiveFactor = Config.Wrap("Questing", "Quest Objective Minimum", "The factor for quest objective values (int)", 8).Value;
            questObjectiveLimit = Config.Wrap("Questing", "Quest Objective Limit", "The factor for the max quest objective value (int)", 20).Value;
            itemDroppingFromPlayers = Convert.ToBoolean(Config.Wrap("Questing", "itemDroppingFromPlayers", "Items drop from player instead of popping up in inventory (bool)", "false").Value);
            questInChat = Convert.ToBoolean(Config.Wrap("Questing", "questInChat", "Quests show up in chat (useful when playing with unmodded players) (bool)", "true").Value);

            // Director params
            bannedDirectorSpawns = Config.Wrap("Director", "bannedDirectorSpawns", "A comma seperated list of banned spawns for director", "Chest,TripleShop,Chance,Equipment,Blood").Value.Split(',');
            isChests = Convert.ToBoolean(Config.Wrap("Director", "Interactables", "Use banned director spawns (bool)", "true").Value);

            // Feature params
            isBossChests = Convert.ToBoolean(Config.Wrap("Features", "Boss Chests", "Boss loot chests (recommended to turn off when enabling interactables) (bool)", "false").Value);
            isQuesting = Convert.ToBoolean(Config.Wrap("Features", "Questing", "Questing system (bool)", "true").Value);
            isEnemyDrops = Convert.ToBoolean(Config.Wrap("Features", "Enemy Drops", "Enemies drop items (bool)", "true").Value);
            isQuestResetting = Convert.ToBoolean(Config.Wrap("Features", "Quest Resetting", "Determines whether quests reset over stage advancement (bool)", "false").Value);

            // force UI refresh and send message
            resetUI = true;
            Chat.AddMessage("<color=#13d3dd>RPGMod: </color> Config loaded");
        }

        // Handles questing
        public void CheckQuest()
        {
            if (!questMessage.Initialised)
            {
                GetNewQuest();
            }
            else
            {
                DisplayQuesting();
            }
        }

        // Sets quest parameters
        public void GetNewQuest()
        {
            if (!NetworkServer.active) {
                return;
            }

            int monstersAlive = TeamComponent.GetTeamMembers(TeamIndex.Monster).Count;

            if (monstersAlive > 0)
            {
                CharacterBody targetBody = TeamComponent.GetTeamMembers(TeamIndex.Monster)[random.Next(0, monstersAlive)].GetComponent<CharacterBody>();

                if (targetBody.isBoss || SurvivorCatalog.FindSurvivorDefFromBody(targetBody.master.bodyPrefab) != null)
                {
                    return;
                }

                questMessage.Target = targetBody.GetUserName();
                questMessage.TargetName = targetBody.name;
                int upperObjectiveLimit = (int)Math.Round(questObjectiveFactor * Run.instance.compensatedDifficultyCoefficient);

                if (upperObjectiveLimit >= questObjectiveLimit)
                {
                    upperObjectiveLimit = questObjectiveLimit;
                }

                if (!stageChange || questFirst || isQuestResetting)
                {
                    serverQuestData.Objective = random.Next(questObjectiveFactor, upperObjectiveLimit);
                    serverQuestData.Progress = 0;
                    serverQuestData.Drop = GetQuestDrop();
                }
                questMessage.Initialised = true;
                questIndex = random.Next(0, questList.Count);
                questMessage.Description = GetDescription();
                if (questInChat)
                {
                    Chat.SimpleChatMessage message = new Chat.SimpleChatMessage();
                    message.baseToken = string.Format("Eliminate {0} {1}s to receive: <color=#{2}>{3}</color>",
                        serverQuestData.Objective,
                        questMessage.Target,
                        ColorUtility.ToHtmlStringRGBA(serverQuestData.Drop.GetPickupColor()),
                        Language.GetString(ItemCatalog.GetItemDef(serverQuestData.Drop.itemIndex).nameToken));
                    Chat.SendBroadcastChat(message);
                }
                questFirst = false;
                stageChange = false;
                SendQuest();
            }
        }

        // Check if quest fulfilled
        public void CheckQuestStatus()
        {
            if (!NetworkServer.active) {
                return;
            }
            if (serverQuestData.Progress >= serverQuestData.Objective)
            {
                if (questMessage.Initialised) {
                    foreach (var player in PlayerCharacterMasterController.instances)
                    {
                        if (player.master.alive)
                        {
                            var transform = player.master.GetBody().coreTransform;
                            if (itemDroppingFromPlayers)
                            {
                                PickupDropletController.CreatePickupDroplet(serverQuestData.Drop, transform.position, transform.forward * 10f);
                            }
                            else
                            {
                                player.master.inventory.GiveItem(serverQuestData.Drop.itemIndex);
                            }
                        }
                    }
                }
                GetNewQuest();
            }
        }

        // Handles the display of the UI
        public void DisplayQuesting()
        {
            LocalUser localUser = LocalUserManager.GetFirstLocalUser();

            if (CachedCharacterBody == null && localUser != null)
            {
                CachedCharacterBody = localUser.cachedBody;
            }

            if (Notification == null && CachedCharacterBody != null || resetUI)
            {
                if (resetUI)
                {
                    Destroy(Notification);
                }

                if (isDebug)
                {
                    Debug.Log(CachedCharacterBody);
                    Debug.Log(sizeX);
                    Debug.Log(sizeY);
                    Debug.Log(Screen.width * screenPosX / 100f);
                    Debug.Log(Screen.height * screenPosY / 100f);
                    Debug.Log(questMessage.Description);
                    //Debug.Log(titleFontSize);
                    //Debug.Log(descriptionFontSize);
                }

                Notification = CachedCharacterBody.gameObject.AddComponent<Notification>();
                Notification.transform.SetParent(CachedCharacterBody.transform);
                Notification.SetPosition(new Vector3((float)(Screen.width * screenPosX / 100f), (float)(Screen.height * screenPosY / 100f), 0));
                Notification.GetTitle = () => "QUEST";
                Notification.GetDescription = () => questMessage.Description;
                Notification.GenericNotification.fadeTime = 1f;
                Notification.GenericNotification.duration = 86400f;
                Notification.SetSize(sizeX, sizeY);
                resetUI = false;
            }

            if (questMessage.Initialised)
            {
                Notification.SetIcon(BodyCatalog.FindBodyPrefab(questMessage.TargetName).GetComponent<CharacterBody>().portraitIcon);
            }

            if (CachedCharacterBody == null && Notification != null)
            {
                Destroy(Notification);
            }

            if (Notification != null && Notification.RootObject != null)
            {
                if (Persistent || (localUser != null && localUser.inputPlayer != null && localUser.inputPlayer.GetButton("info")))
                {
                    Notification.RootObject.SetActive(true);
                    return;
                }

                Notification.RootObject.SetActive(false);
            }
        }

        // Gets the drop for the quest
        public PickupIndex GetQuestDrop()
        {
            WeightedSelection<List<PickupIndex>> weightedSelection = new WeightedSelection<List<PickupIndex>>(8);

            weightedSelection.AddChoice(Run.instance.availableTier1DropList, chanceQuestingCommon);
            weightedSelection.AddChoice(Run.instance.availableTier2DropList, chanceQuestingUnCommon);
            weightedSelection.AddChoice(Run.instance.availableTier3DropList, chanceQuestingLegendary);

            List<PickupIndex> list = weightedSelection.Evaluate(Run.instance.spawnRng.nextNormalizedFloat);
            PickupIndex item = list[Run.instance.spawnRng.RangeInt(0, list.Count)];
            
            return item;
        }

        // Set Client Handlers
        public void InitClientHanders()
        {
            Debug.Log("[RPGMod] Client Handlers Added");
            NetworkClient client = NetworkManager.singleton.client;

            client.RegisterHandler(msgQuestDrop, OnQuestRecieved);
            isClientRegistered = true;
        }

        // Send data message
        public void SendQuest()
        {
            if (!NetworkServer.active) {
                return;
            }
            NetworkServer.SendToAll(msgQuestDrop, questMessage);
        }

        // Handler function for quest drop message
        public void OnQuestRecieved(NetworkMessage netMsg) {
            QuestMessage message = netMsg.ReadMessage<QuestMessage>();
            questMessage = message;
        }

        // Builds the string for the quest description
        public string GetDescription()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format("{0} {1} {2}s.", questList[questIndex], serverQuestData.Objective, questMessage.Target));
            sb.AppendLine(string.Format("<b>Progress:</b> {0}/{1}", serverQuestData.Progress, serverQuestData.Objective));
            sb.AppendLine(string.Format("<b>Reward:</b> <color=#{0}>{1}</color>", ColorUtility.ToHtmlStringRGBA(serverQuestData.Drop.GetPickupColor()), Language.GetString(ItemCatalog.GetItemDef(serverQuestData.Drop.itemIndex).nameToken)));
            return sb.ToString();
        }

        // Converts string config to a float
        public float ConfigToFloat(string configline)
        {
            if (float.TryParse(configline, out float x))
            {
                return x;
            }
            return 0f;
        }

        // Drops Boss Chest
        public void DropBoss(SpawnCard spawnCard, Transform transform)
        {
            transform.Translate(Vector3.down * 0.5f);
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit))
            {
                transform.Translate(Vector3.down * hit.distance);
                spawnCard.DoSpawn(transform.position, transform.rotation);
            }
        }

        //The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {

            Chat.AddMessage("<color=#13d3dd>RPGMod: </color> Loaded Successfully!");

            // Refresh values initially
            RefreshConfigValues(true);

            On.RoR2.Run.Start += (orig, self) =>
            {
                isLoaded = true;
                questFirst = true;
                orig(self);
            };

            if (isQuesting)
            {
                On.RoR2.Run.OnClientGameOver += (orig, self, runReport) =>
                {
                    resetUI = true;
                    orig(self, runReport);
                };

                On.RoR2.Run.OnDisable += (orig, self) =>
                {
                    isLoaded = false;
                    serverQuestData = new ServerQuestData();
                    questMessage = new QuestMessage();

                    isClientRegistered = false;

                    CachedCharacterBody = null;

                    if (Notification != null)
                    {
                        Destroy(Notification);
                    }

                    orig(self);
                };

                On.RoR2.Run.OnServerSceneChanged += (orig, self, sceneName) =>
                {
                    questMessage.Initialised = false;
                    stageChange = true;
                    resetUI = true;
                    orig(self, sceneName);
                };
            }

            if (isBossChests)
            {
                // Edit chest behavior
                On.RoR2.ChestBehavior.ItemDrop += (orig, self) =>
                {
                    self.tier2Chance = bossChestChanceUncommon;
                    self.tier3Chance = bossChestChanceLegendary;
                    orig(self);
                };
            }

            // Death drop hanlder
            On.RoR2.GlobalEventManager.OnCharacterDeath += (orig, self, damageReport) =>
            {
                if (!isSuicide)
                {
                    float chance;
                    CharacterBody enemyBody = damageReport.victim.gameObject.GetComponent<CharacterBody>();
                    GameObject attackerMaster = damageReport.damageInfo.attacker.GetComponent<CharacterBody>().masterObject;
                    CharacterMaster attackerController = attackerMaster.GetComponent<CharacterMaster>();

                    if (isQuesting && questMessage.Initialised)
                    {
                        if (enemyBody.GetUserName() == questMessage.Target)
                        {
                            serverQuestData.Progress += 1;
                            CheckQuestStatus();
                            if (questMessage.Initialised)
                            {
                                questMessage.Description = GetDescription();
                                SendQuest();
                            }
                        }
                    }

                    if (isEnemyDrops)
                    {
                        bool isElite = enemyBody.isElite || enemyBody.isChampion;
                        bool isBoss = enemyBody.isBoss;

                        if (isBoss)
                        {
                            chance = chanceBoss;
                        }
                        else
                        {
                            if (isElite)
                            {
                                chance = chanceElite;
                            }
                            else
                            {
                                chance = chanceNormal;
                            }
                        }

                        chance *= (1 - dropsPlayerScaling + (dropsPlayerScaling * Run.instance.participatingPlayerCount));
                        if (gameStartScaling - (Run.instance.difficultyCoefficient - 1) > 1)
                        {
                            chance *= (gameStartScaling -= (Run.instance.difficultyCoefficient - 1));
                        }

                        // rng check
                        bool didDrop = Util.CheckRoll(chance, attackerController ? attackerController.luck : 0f, null);

                        // Gets Item and drops in world
                        if (didDrop)
                        {
                            if (!isBoss)
                            {
                                // Create a weighted selection for rng
                                WeightedSelection<List<PickupIndex>> weightedSelection = new WeightedSelection<List<PickupIndex>>(8);
                                // Check if enemy is boss, elite or normal
                                if (isElite)
                                {
                                    weightedSelection.AddChoice(Run.instance.availableTier1DropList, eliteChanceTier1);
                                    weightedSelection.AddChoice(Run.instance.availableTier2DropList, eliteChanceTier2);
                                    weightedSelection.AddChoice(Run.instance.availableTier3DropList, eliteChanceTier3);
                                    weightedSelection.AddChoice(Run.instance.availableLunarDropList, eliteChanceTierLunar);
                                }
                                else
                                {
                                    weightedSelection.AddChoice(Run.instance.availableTier1DropList, normalChanceTier1);
                                    weightedSelection.AddChoice(Run.instance.availableTier2DropList, normalChanceTier2);
                                    weightedSelection.AddChoice(Run.instance.availableTier3DropList, normalChanceTier3);
                                    weightedSelection.AddChoice(Run.instance.availableEquipmentDropList, normalChanceTierEquip);
                                }
                                // Get a Tier
                                List<PickupIndex> list = weightedSelection.Evaluate(Run.instance.spawnRng.nextNormalizedFloat);
                                // Pick random from tier
                                PickupIndex item = list[Run.instance.spawnRng.RangeInt(0, list.Count)];
                                // Spawn item
                                PickupDropletController.CreatePickupDroplet(item, enemyBody.transform.position, Vector3.up * 20f);
                            }
                            else
                            {
                                if (isBossChests)
                                {
                                    DropBoss(chest2, damageReport.victim.transform);
                                }
                            }
                        }
                    }
                }
                else {
                    isSuicide = false;
                }
                orig(self, damageReport);
            };

            On.RoR2.HealthComponent.Suicide += (orig, self, killerOverride) =>
            {
                if (self.gameObject.GetComponent<CharacterBody>().isBoss || self.gameObject.GetComponent<CharacterBody>().GetUserName() == "Engineer Turret")
                {
                    isSuicide = true;
                }
                orig(self, killerOverride);
            };

            if (isChests)
            {
                // Handles banned scene spawns
                On.RoR2.ClassicStageInfo.Awake += (orig, self) =>
                {
                    // Gets card catergories using reflection
                    DirectorCardCategorySelection cardSelection = self.GetFieldValue<DirectorCardCategorySelection>("interactableCategories");
                    for (int i = 0; i < cardSelection.categories.Length; i++)
                    {
                        // Makes copy of category to make changes
                        var cards3 = cardSelection.categories[i];
                        cards3.cards = cardSelection.categories[i].cards.Where(val => !bannedDirectorSpawns.Any(val.spawnCard.prefab.name.Contains)).ToArray();

                        // Sets category to new edited version
                        cardSelection.categories[i] = cards3;
                    }
                    // Sets new card categories
                    self.SetFieldValue("interactableCategories", cardSelection);

                    // Runs original function
                    orig(self);
                };

            }

        }

        public void Update()
        {
            if (isLoaded)
            {
                // Checks for quest
                if (isQuesting)
                {
                    CheckQuest();
                }

                // Registers Client Handlers
                if (!isClientRegistered)
                {
                    InitClientHanders();
                }

                //This if statement checks if the player has currently pressed F2, and then proceeds into the statement:
                if (Input.GetKeyDown(KeyCode.F6))
                {
                    RefreshConfigValues(false);
                }

                if (Input.GetKeyDown(KeyCode.F3) && isDebug)
                {
                    serverQuestData.Progress = serverQuestData.Objective - 1;
                    questMessage.Description = GetDescription();
                    SendQuest();
                }

            }
        }
    }
}