using BepInEx;
using RoR2;
using System;
using System.Collections.Generic;
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

    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.ghasttear1.rpgmod", "RPGMod", "1.0.0")]

    public class RPGMod : BaseUnityPlugin
    {
       
        // Misc params
        public System.Random random = new System.Random();
        public SpawnCard chest2 = Resources.Load<SpawnCard>("SpawnCards/InteractableSpawnCard/iscchest2");
        public GameObject targetBody;
        public bool isLoaded = false;
        public bool isDebug = false;

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

        // Director params
        public int amountMountainShrines;
        public int amountChanceShrines;
        public int amountCombatShrines;

        // UI params
        public Notification Notification { get; set; }
        public int screenPosX;
        public int screenPosY;
        public int titleFontSize;
        public int descriptionFontSize;
        public int sizeX;
        public int sizeY;
        public float sizeScale;
        public bool resetUI = false;
        public bool Persistent = true;
        public CharacterBody CachedCharacterBody;

        // Questing params
        public int questObjectiveFactor;
        public int questObjectiveLimit;
        public List<string> questList = new List<string>() { "<b>Kill</b>", "<b>Eliminate</b>" };
        public int questIndex;
        public bool stageChange;

        // Feature params
        public bool isChests;
        public bool isBossChests;
        public bool isEnemyDrops;
        public bool isQuesting;

        // Refreshes the config values from the config
        public void RefreshConfigValues(bool initLoad)
        {
            if (!initLoad)
            {
                Config.Reload();
            }

            // Chances
            chanceNormal = ConfigToFloat(Config.Wrap("Chances", "Chance Normal", "Base chance for a normal enemy to drop an item", "10").Value);
            chanceElite = ConfigToFloat(Config.Wrap("Chances", "Chance Elite", "Base chance for an elite enemy to drop an item", "12").Value);
            chanceBoss = ConfigToFloat(Config.Wrap("Chances", "Chance Boss", "Base chance for a boss enemy to drop an item", "35").Value);
            bossChestChanceLegendary = ConfigToFloat(Config.Wrap("Chances", "Chest Chance Legendary", "Chance for a legendary to drop from a boss chest", "0.3").Value);
            bossChestChanceUncommon = ConfigToFloat(Config.Wrap("Chances", "Chest Chance Uncommon", "Chance for a uncommon to drop from a boss chest", "0.7").Value);
            chanceQuestingCommon = ConfigToFloat(Config.Wrap("Chances", "Quest Chance Common", "Chance for quest drop to be common", "0").Value);
            chanceQuestingUnCommon = ConfigToFloat(Config.Wrap("Chances", "Quest Chance Uncommon", "Chance for quest drop to be uncommon", "0.85").Value);
            chanceQuestingLegendary = ConfigToFloat(Config.Wrap("Chances", "Quest Chance Legendary", "Chance for quest drop to be legendary", " 0.15").Value);
            dropsPlayerScaling = ConfigToFloat(Config.Wrap("Chances", "Drops Player Scaling", "Scaling per player ((1 - value)*(value * playerAmount))", " 0.35").Value);
            eliteChanceTier1 = ConfigToFloat(Config.Wrap("Chances", "eliteChanceTier1", "Chance for elite to drop a tier 1 item", "0.45").Value);
            eliteChanceTier2 = ConfigToFloat(Config.Wrap("Chances", "eliteChanceTier2", "Chance for elite to drop a tier 2 item", "0.2").Value);
            eliteChanceTier3 = ConfigToFloat(Config.Wrap("Chances", "eliteChanceTier3", "Chance for elite to drop a tier 3 item", "0.1").Value);
            eliteChanceTierLunar = ConfigToFloat(Config.Wrap("Chances", "eliteChanceTierLunar", "Chance for elite to drop a lunar item", "0.1").Value);
            normalChanceTier1 = ConfigToFloat(Config.Wrap("Chances", "normalChanceTier1", "Chance for normal enemy to drop a tier 1 item", "0.9").Value);
            normalChanceTier2 = ConfigToFloat(Config.Wrap("Chances", "normalChanceTier2", "Chance for normal enemy to drop a tier 2 item", "0.1").Value);
            normalChanceTier3 = ConfigToFloat(Config.Wrap("Chances", "normalChanceTier3", "Chance for normal enemy to drop a tier 3 item", "0.01").Value);
            normalChanceTierEquip = ConfigToFloat(Config.Wrap("Chances", "normalChanceTierEquip", "Chance for normal enemy to drop equipment", "0.1").Value);

            // Director params
            amountMountainShrines = Config.Wrap("Spawning", "Amount Mountain Shrines", "The amount of mountain shrines that spawn per stage", 2).Value;
            amountChanceShrines = Config.Wrap("Spawning", "Amount Chance Shrines", "The amount of chances shrines that spawn per stage", 0).Value;
            amountCombatShrines = Config.Wrap("Spawning", "Amount Combat Shrines", "The amount of combat shrines that spawn per stage", 2).Value;

            // UI params
            screenPosX = Config.Wrap("UI", "Screen Pos X", "UI screen x location", 89).Value;
            screenPosY = Config.Wrap("UI", "Screen Pos Y", "UI screen y location", 50).Value;
            titleFontSize = Config.Wrap("UI", "Title Font Size", "UI title font size", 18).Value;
            descriptionFontSize = Config.Wrap("UI", "Description Font Size", "UI description font size", 14).Value;
            sizeScale = ConfigToFloat(Config.Wrap("UI", "UI Scale", "Scale size of UI", "1.0").Value);
            sizeX = Config.Wrap("UI", "Size X", "Size of UI X axis", 250).Value;
            sizeY = Config.Wrap("UI", "Size Y", "Size of UI Y axis", 80).Value;

            // Questing params
            questObjectiveFactor = Config.Wrap("Questing", "Quest Objective Minimum", "The factor for quest objective values", 8).Value;
            questObjectiveLimit = Config.Wrap("Questing", "Quest Objective Limit", "The factor for the max quest objective value", 20).Value;

            // Feature params
            isChests = Convert.ToBoolean(Config.Wrap("Features", "Interactables", "Chests and other interactables (such as shrines and gold barrels)", "false").Value);
            isBossChests = Convert.ToBoolean(Config.Wrap("Features", "Boss Chests", "Boss loot chests (recommended to turn off when enabling interactables)", "true").Value);
            isQuesting = Convert.ToBoolean(Config.Wrap("Features", "Questing", "Questing system", "true").Value);
            isEnemyDrops = Convert.ToBoolean(Config.Wrap("Features", "Enemy Drops", "Enemies drop items", "true").Value);

            // force UI refresh and send message
            resetUI = true;
            Chat.AddMessage("<color=#13d3dd>RPGMod: </color> Config loaded");
        }

        // Handles questing
        public void CheckQuest()
        {
            if (!questMessage.Initialised)
            {
                if (NetworkServer.active)
                {
                    GetNewQuest();
                }
            }
            else
            {
                DisplayQuesting();
            }
        }

        // Sets quest parameters
        public void GetNewQuest()
        {
            int monstersAlive = TeamComponent.GetTeamMembers(TeamIndex.Monster).Count;

            if (monstersAlive > 0)
            {
                CharacterBody targetBody = TeamComponent.GetTeamMembers(TeamIndex.Monster)[random.Next(0, monstersAlive)].GetComponent<CharacterBody>();

                while (targetBody.isBoss)
                {
                    targetBody = TeamComponent.GetTeamMembers(TeamIndex.Monster)[random.Next(0, monstersAlive)].GetComponent<CharacterBody>();
                }

                questMessage.Target = targetBody.GetUserName();
                questMessage.TargetName = targetBody.name;
                int upperObjectiveLimit = (int)Math.Round(questObjectiveFactor * Run.instance.compensatedDifficultyCoefficient);

                if (upperObjectiveLimit >= questObjectiveLimit)
                {
                    upperObjectiveLimit = questObjectiveLimit;
                }

                if (!stageChange)
                {
                    serverQuestData.Objective = random.Next(questObjectiveFactor, upperObjectiveLimit);
                    serverQuestData.Progress = 0;
                    serverQuestData.Drop = GetQuestDrop();
                }

                questMessage.Initialised = true;
                questIndex = random.Next(0, questList.Count);
                questMessage.Description = GetDescription();
                stageChange = false;
                SendQuest();
            }
        }

        // Check if quest fulfilled
        public void CheckQuestStatus()
        {
            if (serverQuestData.Progress >= serverQuestData.Objective)
            {
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    var transform = player.master.GetBody().coreTransform;
                    PickupDropletController.CreatePickupDroplet(serverQuestData.Drop, transform.position, transform.forward * 10f);
                }
                questMessage.Initialised = false;
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

                Notification = CachedCharacterBody.gameObject.AddComponent<Notification>();
                Notification.transform.SetParent(CachedCharacterBody.transform);
                Notification.SetPosition(new Vector3((float)(Screen.width * screenPosX / 100f), (float)(Screen.height * screenPosY / 100f), 0));
                Notification.GetTitle = () => "QUEST";
                Notification.GetDescription = DisplayString;
                Notification.GenericNotification.fadeTime = 1f;
                Notification.GenericNotification.duration = 86400f;
                Notification.SetSize(sizeX * sizeScale, sizeY * sizeScale);
                Notification.SetFontSize(Notification.GenericNotification.titleText, titleFontSize);
                Notification.SetFontSize(Notification.GenericNotification.descriptionText, descriptionFontSize);
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
                if (this.Persistent || (localUser != null && localUser.inputPlayer != null && localUser.inputPlayer.GetButton("info")))
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

        // Send message
        public void SendQuest()
        {
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

        // Returns string for notification
        public string DisplayString()
        {
            return questMessage.Description.ToString();
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
                    Destroy(Notification);
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

            if (isEnemyDrops)
            {
                // Fix engi turret bug with changing OnCharacterDeath
                On.RoR2.HealthComponent.Suicide += (orig, self, killerOverride) =>
                {
                    if (!NetworkServer.active)
                    {
                        Debug.LogWarning("[Server] function 'System.Void RoR2.HealthComponent::Suicide(UnityEngine.GameObject)' called on client");
                        return;
                    }

                    if (self.alive)
                    {
                        DamageInfo damageInfo = new DamageInfo();
                        damageInfo.damage = self.health + self.shield;
                        damageInfo.position = base.transform.position;
                        damageInfo.damageType = DamageType.Generic;
                        damageInfo.procCoefficient = 1f;
                        if (killerOverride)
                        {
                            damageInfo.attacker = killerOverride;
                        }
                        self.Networkhealth = 0f;
                        base.BroadcastMessage("OnKilled", damageInfo, SendMessageOptions.DontRequireReceiver);
                    }
                };

                // Death drop hanlder
                On.RoR2.GlobalEventManager.OnCharacterDeath += (orig, self, damageReport) =>
                {
                    float chance;
                    CharacterBody enemyBody = damageReport.victim.GetComponent<CharacterBody>();
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

                    bool isElite = damageReport.victimBody.isElite || damageReport.victimBody.isChampion;
                    bool isBoss = damageReport.victimBody.isBoss;

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
                    orig(self, damageReport);
                };
            }

            // Handles scene director
            if (!isChests)
            {
                On.RoR2.SceneDirector.PlaceTeleporter += (orig, self) =>
                {
                    self.SetFieldValue("interactableCredit", 0);

                    DirectorPlacementRule placementMode = new DirectorPlacementRule();
                    placementMode.placementMode = DirectorPlacementRule.PlacementMode.Random;
                    Xoroshiro128Plus rng = new Xoroshiro128Plus((ulong)Run.instance.stageRng.nextUint);

                    DirectorCore myDirector = self.GetFieldValue<DirectorCore>("directorCore");

                    // Todo: change shrine spawns to reflect that of default but still without chests
                    for (int i = 0; i < random.Next(0, amountMountainShrines); i++)
                    { 
                        myDirector.TrySpawnObject(Resources.Load<SpawnCard>("SpawnCards/InteractableSpawnCard/iscShrineBoss"), placementMode, rng);
                    }

                    for (int i = 0; i < random.Next(0, amountChanceShrines); i++)
                    {
                        myDirector.TrySpawnObject(Resources.Load<SpawnCard>("SpawnCards/InteractableSpawnCard/iscShrineChance"), placementMode, rng);
                    }

                    for (int i = 0; i < random.Next(0, amountCombatShrines); i++)
                    {
                        myDirector.TrySpawnObject(Resources.Load<SpawnCard>("SpawnCards/InteractableSpawnCard/iscShrineCombat"), placementMode, rng);
                    }

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
                if (Input.GetKeyDown(KeyCode.F2))
                {
                    RefreshConfigValues(false);
                }

                if (Input.GetKeyDown(KeyCode.F3) && isDebug)
                {
                    serverQuestData.Progress = 15;
                }
            }
        }
    }
}