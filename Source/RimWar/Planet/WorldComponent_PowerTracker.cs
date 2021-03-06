﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using RimWar;
using Verse;
using UnityEngine;

namespace RimWar.Planet
{
    public class WorldComponent_PowerTracker : WorldComponent
    {
        //Historic Variables
        public int objectsCreated = 0;
        public int creationAttempts = 0;
        public int settlementSearches = 0;
        public int globalActions = 0;

        //Do Once per load
        private bool factionsLoaded = false;
        private int nextEvaluationTick = 20;
        private int targetRangeDivider = 100;
        private int totalTowns = 10;
        public Faction victoryFaction = null;
        private bool victoryDeclared = false;
        private bool rwdInitialized = false;
        private bool rwdInitVictory = false;
        public List<CaravanTargetData> caravanTargetData = new List<CaravanTargetData>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref this.rwdInitialized, "rwdInitialized", false, false);
            Scribe_Values.Look<bool>(ref this.rwdInitVictory, "rwdInitVictory", false, false);
            Scribe_Values.Look<bool>(ref this.victoryDeclared, "victoryDeclared", false, false);
            Scribe_Values.Look<int>(ref this.objectsCreated, "objectsCreated", 0, false);
            Scribe_Values.Look<int>(ref this.creationAttempts, "creationAttewmpts", 0, false);
            Scribe_Values.Look<int>(ref this.settlementSearches, "settlementSearches", 0, false);
            Scribe_Values.Look<int>(ref this.globalActions, "globalActions", 0, false);
            Scribe_References.Look<Faction>(ref this.victoryFaction, "victoryFaction");
            Scribe_Collections.Look<RimWarData>(ref this.rimwarData, "rimwarData", LookMode.Deep);
            //Scribe_Collections.Look<WarObject>(ref this.allWarObjects, "allWarObjects", LookMode.Reference, new object[0]);
            Scribe_Collections.Look<Settlement>(ref this.allRimWarSettlements, "allRimWarSettlements", LookMode.Reference, new object[0]);
        }

        private List<WarObject> allWarObjects;
        public List<WarObject> AllWarObjects
        {
            get
            {
                bool flag = allWarObjects == null;
                if (flag)
                {
                    allWarObjects = new List<WarObject>();
                    allWarObjects.Clear();
                }
                return allWarObjects;
            }
            set
            {
                bool flag = allWarObjects == null;
                if(flag)
                {
                    allWarObjects = new List<WarObject>();
                    allWarObjects.Clear();
                }
                this.allWarObjects = value;
            }
        }

        private List<Settlement> allRimWarSettlements;
        public List<Settlement> AllRimWarSettlements
        {
            get
            {
                bool flag = allRimWarSettlements == null;
                if(flag)
                {
                    allRimWarSettlements = new List<Settlement>();
                    allRimWarSettlements.Clear();
                }
                return allRimWarSettlements;
            }
            set
            {
                bool flag = allRimWarSettlements == null;
                if (flag)
                {
                    allRimWarSettlements = new List<Settlement>();
                    allRimWarSettlements.Clear();
                }
                allRimWarSettlements = value;
            }
        }


        private List<WorldObject> worldObjects;
        public List<WorldObject> WorldObjects
        {
            get
            {
                bool flag = worldObjects == null;
                if (flag)
                {
                    worldObjects = new List<WorldObject>();
                    worldObjects.Clear();
                }
                return this.worldObjects;
            }
            set
            {
                bool flag = worldObjects == null;
                if (flag)
                {
                    worldObjects = new List<WorldObject>();
                    worldObjects.Clear();
                }
                this.worldObjects = value;
            }
        }
        List<WorldObject> worldObjectsOfPlayer = new List<WorldObject>();

        private List<RimWarData> rimwarData;
        public List<RimWarData> RimWarData
        {
            get
            {
                bool flag = this.rimwarData == null;
                if (flag)
                {
                    this.rimwarData = new List<RimWarData>();
                }
                return this.rimwarData;
            }
        }

        public WorldComponent_PowerTracker(World world) : base(world)
        {
            //Log.Message("world component power tracker init");
            //return;
        }

        public override void WorldComponentTick()
        {
            int currentTick = Find.TickManager.TicksGame;
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            if (currentTick >= 10 && !rwdInitialized)
            {
                Initialize();
                rwdInitialized = true;
            }
            if(currentTick % 60 == 0)
            {
                AdjustCaravanTargets();
            }
            if (currentTick % settingsRef.rwdUpdateFrequency == 0)
            {
                WorldUtility.ValidateFactions(true);
                UpdateFactions();
                if (settingsRef.useRimWarVictory && !victoryDeclared && this.rwdInitVictory)
                {
                    CheckVictoryConditions();
                }
            }
            if(currentTick % 60000 == 0)
            {
                DoGlobalRWDAction();                 
            }
            if (currentTick >= this.nextEvaluationTick)
            {                
                //Log.Message("checking events");
                this.nextEvaluationTick = currentTick + Rand.Range((int)(settingsRef.averageEventFrequency * .5f) , (int)(settingsRef.averageEventFrequency * 1.5f));
                //Log.Message("current tick: " + currentTick + " next evaluation at " + this.nextEvaluationTick);
                //Log.Message("rwd list " + (this.RimWarData != null));
                if (this.RimWarData != null && this.RimWarData.Count > 0)
                {
                    //Log.Message("rwd count " + this.RimWarData.Count);
                    RimWarData rwd = this.RimWarData.RandomElement();
                    if (rwd.FactionSettlements != null && rwd.FactionSettlements.Count > 0)
                    {
                        //Log.Message("detecting " + rwd.FactionSettlements.Count + " for faction " + rwd.RimWarFaction.Name);
                        Settlement rwdTown = rwd.FactionSettlements.RandomElement();
                        //Log.Message("town selected " + rwdTown.RimWorld_Settlement.Label + " next tick event " + rwdTown.nextEventTick + " current tick " + currentTick);
                        if(rwdTown.nextEventTick - currentTick > settingsRef.settlementEventDelay * 2)
                        {
                            rwdTown.nextEventTick = currentTick;
                        }
                        if (rwd.behavior != RimWarBehavior.Player && rwdTown.nextEventTick <= currentTick && ((!CaravanNightRestUtility.RestingNowAt(rwdTown.Tile) && !rwd.movesAtNight) || (CaravanNightRestUtility.RestingNowAt(rwdTown.Tile) && rwd.movesAtNight)))
                        {
                            
                            if(rwd.rwdNextUpdateTick < currentTick)
                            {
                                rwd.rwdNextUpdateTick = currentTick + settingsRef.rwdUpdateFrequency;
                                WorldUtility.UpdateRWDSettlementLists(rwd);                                
                            }
                            if (!settingsRef.forceRandomObject)
                            {
                                if (rwdTown.nextSettlementScan == 0 || rwdTown.nextSettlementScan <= Find.TickManager.TicksGame)
                                {
                                    rwdTown.OtherSettlementsInRange = WorldUtility.GetRimWarSettlementsInRange(rwdTown.Tile, Mathf.Min(Mathf.RoundToInt(rwdTown.RimWarPoints / (settingsRef.settlementScanRangeDivider)), (int)settingsRef.maxSettelementScanRange), this.RimWarData, rwd);
                                    rwdTown.nextSettlementScan = Find.TickManager.TicksGame + settingsRef.settlementScanDelay;
                                }
                            }
                            RimWarAction newAction = rwd.GetWeightedSettlementAction();
                            if(rwd.IsAtWar && !(newAction == RimWarAction.LaunchedWarband || newAction == RimWarAction.ScoutingParty || newAction == RimWarAction.Warband))
                            {
                                newAction = rwd.GetWeightedSettlementAction();
                            }
                            //Log.Message("attempting new action of " + newAction.ToString());
                            //newAction = RimWarAction.LaunchedWarband;
                            if (newAction != RimWarAction.None)
                            {
                                this.objectsCreated++;
                                if (Rand.Chance(.02f))
                                {
                                    rwdTown.RimWarPoints += Rand.Range(20, 200);
                                    this.globalActions++;
                                    //Log.Message("" + rwdTown.RimWorld_Settlement.Name + " had a burst of growth");
                                }
                                if (newAction == RimWarAction.Caravan)
                                {
                                    //Log.Message("Caravan attempt by " + rwd.RimWarFaction.Name);
                                    AttemptTradeMission(rwd, rwdTown);
                                }
                                else if (newAction == RimWarAction.Diplomat)
                                {
                                    //Log.Message("Diplomat attempt by " + rwd.RimWarFaction.Name);
                                    if (settingsRef.createDiplomats)
                                    {
                                        AttemptDiplomatMission(rwd, rwdTown);
                                    }
                                    else
                                    {
                                        this.creationAttempts++;
                                    }
                                }
                                else if (newAction == RimWarAction.LaunchedWarband)
                                {
                                    //Log.Message("Launched Warband attempt by " + rwd.RimWarFaction.Name);
                                    AttemptLaunchedWarbandAgainstTown(rwd, rwdTown);
                                }
                                else if (newAction == RimWarAction.ScoutingParty)
                                {
                                    //Log.Message("Scout attempt by " + rwd.RimWarFaction.Name);
                                    AttemptScoutMission(rwd, rwdTown);
                                }
                                else if (newAction == RimWarAction.Settler && rwd.FactionSettlements.Count < settingsRef.maxFactionSettlements)
                                {
                                    //Log.Message("Settler attempt by " + rwd.RimWarFaction.Name);
                                    AttemptSettlerMission(rwd, rwdTown);
                                }
                                else if (newAction == RimWarAction.Warband)
                                {
                                    //Log.Message("Warband attempt by " + rwd.RimWarFaction.Name);
                                    AttemptWarbandActionAgainstTown(rwd, rwdTown);
                                }
                                else
                                {
                                    if (rwd.FactionSettlements.Count >= settingsRef.maxFactionSettlements)
                                    {
                                        if(Rand.Chance(.3f))
                                        {
                                            AttemptTradeMission(rwd, rwdTown);
                                        }
                                    }
                                    else
                                    {
                                        Log.Warning("attempted to generate undefined RimWar settlement action");
                                    }
                                }
                                rwdTown.nextEventTick = currentTick + settingsRef.settlementEventDelay; //one day (60000) default
                            }
                        }
                        else
                        {
                            this.creationAttempts++;
                        }
                    }
                }
            }
            base.WorldComponentTick();
        }

        public void AdjustCaravanTargets()
        {
            if (this.caravanTargetData != null && this.caravanTargetData.Count > 0)
            {
                for (int i = 0; i < this.caravanTargetData.Count; i++)
                {
                    CaravanTargetData ctd = caravanTargetData[i];
                    if(ctd.IsValid())
                    {
                        if(ctd.CaravanTargetTile != 0 && ctd.CaravanDestination != ctd.CaravanTargetTile)
                        {
                            ctd.caravan.pather.StartPath(ctd.CaravanTargetTile, ctd.caravan.pather.ArrivalAction, true);
                        }
                    }
                    else
                    {
                        this.caravanTargetData.Remove(ctd);
                        break;
                    }
                }
            }
        }

        public void AssignCaravanTargets(Caravan caravan, WarObject warObject)
        {
            if(this.caravanTargetData == null)
            {
                this.caravanTargetData = new List<CaravanTargetData>();
                this.caravanTargetData.Clear();
            }
            bool existingCaravan = false;
            for(int i = 0; i < this.caravanTargetData.Count; i++)
            {
                if(caravanTargetData[i].caravan == caravan)
                {
                    caravanTargetData[i].caravanTarget = warObject;
                    existingCaravan = true;
                }
            }
            if(!existingCaravan)
            {
                CaravanTargetData ctd = new CaravanTargetData();
                ctd.caravan = caravan;
                ctd.caravanTarget = warObject;
                this.caravanTargetData.Add(ctd);
            }            
        }

        public void DoGlobalRWDAction()
        {
            RimWarData rwd = this.RimWarData.RandomElement();
            if (rwd.behavior != RimWarBehavior.Player)
            {
                this.globalActions++;
                RimWarAction newAction = rwd.GetWeightedSettlementAction();
                Options.SettingsRef settingsRef = new Options.SettingsRef();
                if (newAction == RimWarAction.Caravan)
                {
                    Settlement rwdSettlement = rwd.FactionSettlements.RandomElement();
                    rwdSettlement.RimWarPoints += Rand.Range(200, 500);
                }
                else if (newAction == RimWarAction.Diplomat)
                {
                    //try
                    //{
                    if (settingsRef.createDiplomats)
                    {
                        Settlement rwdSettlement = rwd.FactionSettlements.RandomElement();
                        Settlement rwdPlayerSettlement = WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer).FactionSettlements.RandomElement();
                        if (rwdSettlement != null && rwdPlayerSettlement != null)
                        {
                            WorldUtility.CreateDiplomat(WorldUtility.CalculateDiplomatPoints(rwdSettlement), rwd, rwdSettlement, rwdSettlement.Tile, rwdPlayerSettlement.Tile, WorldObjectDefOf.Settlement);
                        }
                    }
                    //}
                    //catch(NullReferenceException ex)
                    //{
                    //    Log.Warning("Failed global diplomatic actions");
                    //}
                }
                else if (newAction == RimWarAction.LaunchedWarband)
                {
                    //Log.Message("Global Launched Warband attempt by " + rwd.RimWarFaction.Name);
                    //try
                    //{
                        Settlement rwdTown = rwd.FactionSettlements.RandomElement();
                        Settlement rwdPlayerSettlement = WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer).FactionSettlements.RandomElement();
                        if (rwdTown != null && rwdPlayerSettlement != null && rwd.RimWarFaction.HostileTo(Faction.OfPlayerSilentFail))
                        {
                            //Log.Message("" + rwdTown.RimWorld_Settlement.Name + " with " + rwdTown.RimWarPoints + " evaluating " + targetTown.RimWorld_Settlement.Name + " with " + targetTown.RimWarPoints);
                            int pts = WorldUtility.CalculateWarbandPointsForRaid(rwdPlayerSettlement);
                            if (rwd.behavior == RimWarBehavior.Cautious)
                            {
                                pts = Mathf.RoundToInt(pts * 1.1f);
                            }
                            else if (rwd.behavior == RimWarBehavior.Warmonger)
                            {
                                pts = Mathf.RoundToInt(pts * 1.25f);
                            }
                            //Log.Message("sending warband from " + rwdTown.RimWorld_Settlement.Name);
                            WorldUtility.CreateLaunchedWarband(pts, rwd, rwdTown, rwdTown.Tile, rwdPlayerSettlement.Tile, WorldObjectDefOf.Settlement);
                            //rwdTown.RimWarPoints = rwdTown.RimWarPoints - pts;                          
                        }
                    //}
                    //catch (NullReferenceException ex)
                    //{
                    //    Log.Warning("Failed global launched warband actions");
                    //}
                    
                }
                else if (newAction == RimWarAction.ScoutingParty)
                {
                    //Log.Message("Global Scout attempt by " + rwd.RimWarFaction.Name);
                    //try
                    //{
                        Settlement rwdTown = rwd.FactionSettlements.RandomElement();
                        Settlement rwdPlayerSettlement = WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer).FactionSettlements.RandomElement();
                        if (rwdTown != null && rwdPlayerSettlement != null)
                        {
                            //Log.Message("" + rwdTown.RimWorld_Settlement.Name + " with " + rwdTown.RimWarPoints + " evaluating " + targetTown.RimWorld_Settlement.Name + " with " + targetTown.RimWarPoints);
                            int pts = WorldUtility.CalculateScoutMissionPoints(rwd, rwdPlayerSettlement.RimWarPoints);
                            //Log.Message("sending warband from " + rwdTown.RimWorld_Settlement.Name);
                            WorldUtility.CreateScout(pts, rwd, rwdTown, rwdTown.Tile, rwdPlayerSettlement.Tile, WorldObjectDefOf.Settlement);
                            //rwdTown.RimWarPoints = rwdTown.RimWarPoints - pts;                          
                        }
                    //}
                    //catch (NullReferenceException ex)
                    //{
                    //    Log.Warning("Failed global launched warband actions");
                    //}
                }
                else if (newAction == RimWarAction.Settler)
                {
                    //Log.Message("Allaince attempt by " + rwd.RimWarFaction.Name);
                    int factionAdjustment = Rand.Range(0, 20);
                    RimWarData rwdSecond = this.RimWarData.RandomElement();
                    if(rwdSecond.RimWarFaction != rwd.RimWarFaction && rwdSecond.RimWarFaction != Faction.OfPlayerSilentFail)
                    {
                        rwd.RimWarFaction.TryAffectGoodwillWith(rwdSecond.RimWarFaction, factionAdjustment, true, true, null, null);
                    }
                }
                else if (newAction == RimWarAction.Warband)
                {
                    //Log.Message("War declaration by " + rwd.RimWarFaction.Name);
                    int factionAdjustment = Rand.Range(-20, 0);
                    RimWarData rwdSecond = this.RimWarData.RandomElement();
                    if (rwdSecond.RimWarFaction != rwd.RimWarFaction && rwdSecond.RimWarFaction != Faction.OfPlayerSilentFail)
                    {
                        rwd.RimWarFaction.TryAffectGoodwillWith(rwdSecond.RimWarFaction, factionAdjustment, true, true, null, null);
                    }
                }
                else
                {
                    Log.Warning("attempted to generate undefined RimWar settlement action");
                }
            }
        }

        public void Initialize()
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            if (!factionsLoaded)
            {
                List<Faction> rimwarFactions = new List<Faction>();
                rimwarFactions.Clear();
                for (int i = 0; i < RimWarData.Count; i++)
                {
                    rimwarFactions.Add(RimWarData[i].RimWarFaction);
                }                
                if (settingsRef.randomizeFactionBehavior)
                {
                    RimWarFactionUtility.RandomizeAllFactionRelations();
                }
                List<Faction> allFactionsVisible = world.factionManager.AllFactionsVisible.ToList();
                if (allFactionsVisible != null && allFactionsVisible.Count > 0)
                {
                    for (int i = 0; i < allFactionsVisible.Count; i++)
                    {
                        if (!rimwarFactions.Contains(allFactionsVisible[i]))
                        {
                            AddRimWarFaction(allFactionsVisible[i]);
                        }                        
                        if (settingsRef.playerVS && allFactionsVisible[i] != Faction.OfPlayer)
                        {
                            allFactionsVisible[i].TryAffectGoodwillWith(Faction.OfPlayer, -80, true, true, "Rim War", null);
                            for(int j = 0; j <5; j++)
                            {
                                Faction otherFaction = allFactionsVisible.RandomElement();
                                if(otherFaction != allFactionsVisible[i] && otherFaction != Faction.OfPlayer)
                                {
                                    allFactionsVisible[i].TryAffectGoodwillWith(otherFaction, 50, true, true, "Rim War", null);
                                }
                            }
                            if(allFactionsVisible[i].PlayerGoodwill <= -80)
                            {
                                RimWarFactionUtility.DeclareWarOn(allFactionsVisible[i], Faction.OfPlayer);
                            }
                        }
                    }
                }
                this.factionsLoaded = true;
            }
            if (settingsRef.useRimWarVictory && this.victoryFaction == null)
            {
                GetFactionForVictoryChallenge();
            }
            this.rwdInitVictory = settingsRef.useRimWarVictory;
            this.AllRimWarSettlements = WorldUtility.GetRimWarSettlements(RimWarData);            
        }

        public void CheckVictoryConditions()
        {
            GetFactionForVictoryChallenge();
            CheckVictoryFactionForDefeat();
        }

        public void CheckVictoryFactionForDefeat()
        {
            List< RimWorld.Planet.Settlement> rivalBases = new List<RimWorld.Planet.Settlement>();
            rivalBases.Clear();
            List< RimWorld.Planet.Settlement> allBases = Find.World.worldObjects.SettlementBases;
            if (allBases != null && allBases.Count > 0)
            {
                for (int i = 0; i < allBases.Count; i++)
                {
                    if (allBases[i].Faction == this.victoryFaction)
                    {
                        rivalBases.Add(allBases[i]);
                    }
                }
            }
            if (rivalBases.Count <= 0)
            {
                this.victoryDeclared = true;
                AnnounceVictory();
            }
        }

        private void AnnounceVictory()
        {
            GenGameEnd.EndGameDialogMessage("RW_VictoryAchieved".Translate(this.victoryFaction));
        }

        private void GetFactionForVictoryChallenge()
        {
            if (this.victoryFaction == null)
            {
                if (RimWarData != null && RimWarData.Count > 0)
                {
                    List<Faction> potentialFactions = new List<Faction>();
                    potentialFactions.Clear();
                    for (int i = 0; i < RimWarData.Count; i++)
                    {
                        if (RimWarData[i].hatesPlayer && !RimWarData[i].RimWarFaction.def.hidden)
                        {
                            potentialFactions.Add(RimWarData[i].RimWarFaction);
                        }
                    }
                    if (potentialFactions.Count > 0)
                    {
                        this.victoryFaction = potentialFactions.RandomElement();

                    }
                    else
                    {
                        this.victoryFaction = RimWarData.RandomElement().RimWarFaction;
                    }
                    Find.LetterStack.ReceiveLetter("RW_VictoryChallengeLabel".Translate(), "RW_VictoryChallengeMessage".Translate(this.victoryFaction.Name), LetterDefOf.ThreatBig);
                }
            }
        }

        public void UpdateFactions()
        {
            IncrementSettlementGrowth();
            ReconstituteSettlements();
            UpdateFactionSettlements(this.RimWarData.RandomElement());
            this.AllRimWarSettlements = WorldUtility.GetRimWarSettlements(RimWarData);
        }

        public void IncrementSettlementGrowth()
        {
            this.totalTowns = 0;
            Options.SettingsRef settingsref = new Options.SettingsRef();
            for(int i =0; i < this.RimWarData.Count; i++)
            {
                RimWarData rwd = RimWarData[i];
                if (rwd.behavior != RimWarBehavior.Player)
                {
                    float mult = (settingsref.rwdUpdateFrequency/2500f);
                    if (rwd.behavior == RimWarBehavior.Expansionist)
                    {
                        mult = 1.1f;
                    }
                    for (int j = 0; j < rwd.FactionSettlements.Count; j++)
                    {
                        totalTowns++;
                        Settlement rwdTown = rwd.FactionSettlements[j];
                        float pts = (rwdTown.RimWarPoints / 1000) + 2 + WorldUtility.GetBiomeMultiplier(Find.WorldGrid[rwdTown.Tile].biome);
                        pts = pts * mult * WorldUtility.GetFactionTechLevelMultiplier(rwd.RimWarFaction) * Rand.Range(.2f, 1f);
                        rwdTown.RimWarPoints += Mathf.RoundToInt(pts);                        
                    }
                }
            }
        }

        public void ReconstituteSettlements()
        {
            for (int i = 0; i < this.RimWarData.Count; i++)
            {
                RimWarData rwd = RimWarData[i];
                if (rwd.FactionSettlements != null && rwd.FactionSettlements.Count > 0)
                {
                    for (int j = 0; j < rwd.FactionSettlements.Count; j++)
                    {
                        Settlement rwdTown = rwd.FactionSettlements[j];
                        if (rwdTown.SettlementPointGains != null && rwdTown.SettlementPointGains.Count > 0)
                        {
                            for (int k = 0; k < rwdTown.SettlementPointGains.Count; k++)
                            {
                                //Log.Message("reconstituting " + rwdTown.SettlementPointGains[k].points + " points for " + rwdTown.RimWorld_Settlement.Name + " in " + (rwdTown.SettlementPointGains[k].delay - Find.TickManager.TicksGame) + " ticks");
                                if(rwdTown.SettlementPointGains[k].delay <= Find.TickManager.TicksGame)
                                {
                                    rwdTown.RimWarPoints += rwdTown.SettlementPointGains[k].points;
                                    rwdTown.SettlementPointGains.Remove(rwdTown.SettlementPointGains[k]);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        public void AddRimWarFaction(Faction faction)
        {
            if(!CheckForRimWarFaction(faction))
            {
                RimWarData newRimWarFaction = new RimWarData(faction);
                if(faction != null)
                {
                    GenerateFactionBehavior(newRimWarFaction);
                    AssignFactionSettlements(newRimWarFaction);
                }
                this.RimWarData.Add(newRimWarFaction);
            }
        }

        private bool CheckForRimWarFaction(Faction faction)
        {
            if (this.RimWarData != null)
            {
                for (int i = 0; i < this.RimWarData.Count; i++)
                {
                    if(RimWarData[i].RimWarFaction == faction)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void GenerateFactionBehavior(RimWarData rimwarObject)
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            if (!settingsRef.randomizeFactionBehavior)
            {
                
                bool factionFound = false;
                List<RimWarDef> rwd = DefDatabase<RimWarDef>.AllDefsListForReading;
                //IEnumerable<RimWarDef> enumerable = from def in DefDatabase<RimWarDef>.AllDefs
                //                                    select def;
                //Log.Message("enumerable count is " + enumerable.Count());
                //Log.Message("searching for match to " + rimwarObject.RimWarFaction.def.ToString());
                for (int i = 0; i < rwd.Count; i++)
                {
                    //Log.Message("current " + rwd[i].defName);
                    //Log.Message("with count " + rwd[i].defDatas.Count);
                    for (int j = 0; j < rwd[i].defDatas.Count; j++)
                    {
                        RimWarDefData defData = rwd[i].defDatas[j];
                        //Log.Message("checking faction " + defData.factionDefname);
                        if (defData.factionDefname.ToString() == rimwarObject.RimWarFaction.def.ToString())
                        {
                            factionFound = true;
                            //Log.Message("found faction match in rimwardef for " + defData.factionDefname.ToString());
                            rimwarObject.movesAtNight = defData.movesAtNight;
                            rimwarObject.behavior = defData.behavior;
                            rimwarObject.createsSettlements = defData.createsSettlements;
                            rimwarObject.hatesPlayer = defData.hatesPlayer;
                            break;
                        }
                    }
                }
                if (!factionFound)
                {
                    RandomizeFactionBehavior(rimwarObject);
                }
            }
            else
            {
                RandomizeFactionBehavior(rimwarObject);
            }
            //Log.Message("generating faction behavior for " + rimwarObject.RimWarFaction);
            WorldUtility.CalculateFactionBehaviorWeights(rimwarObject);           

        }

        private void RandomizeFactionBehavior(RimWarData rimwarObject)
        {
            //Log.Message("randomizing faction behavior for " + rimwarObject.RimWarFaction.Name);
            if (rimwarObject.RimWarFaction.def.isPlayer)
            {
                rimwarObject.behavior = RimWarBehavior.Player;
                rimwarObject.createsSettlements = false;
                rimwarObject.hatesPlayer = false;
                rimwarObject.movesAtNight = false;
            }
            else
            {
                rimwarObject.behavior = (RimWarBehavior)Rand.RangeInclusive(0, 5);
                rimwarObject.createsSettlements = true;
                rimwarObject.hatesPlayer = rimwarObject.RimWarFaction.def.permanentEnemy;
            }
        }

        private void AssignFactionSettlements(RimWarData rimwarObject)
        {
            //Log.Message("assigning settlements to " + rimwarObject.RimWarFaction.Name);
            this.WorldObjects = Find.WorldObjects.AllWorldObjects.ToList();
            if (worldObjects != null && worldObjects.Count > 0)
            {
                for (int i = 0; i < worldObjects.Count; i++)
                {
                    //Log.Message("faction for " + worldObjects[i] + " is " + rimwarObject);
                    if (worldObjects[i].Faction == rimwarObject.RimWarFaction)
                    {
                        WorldUtility.CreateRimWarSettlement(rimwarObject, worldObjects[i]);
                    }
                }
            }
        }

        public void UpdateFactionSettlements(RimWarData rwd)
        {
            this.WorldObjects = Find.WorldObjects.AllWorldObjects.ToList();

            if (worldObjects != null && worldObjects.Count > 0 && rwd != null && rwd.RimWarFaction != null)
            {
                //look for settlements not assigned a RimWar Settlement
                for (int i = 0; i < worldObjects.Count; i++)
                {
                    RimWorld.Planet.Settlement wos = worldObjects[i] as RimWorld.Planet.Settlement;
                    if (wos != null && wos.Faction == rwd.RimWarFaction)
                    {
                        bool hasSettlement = false;
                        if (rwd.FactionSettlements != null && rwd.FactionSettlements.Count > 0)
                        {
                            for (int j = 0; j < rwd.FactionSettlements.Count; j++)
                            {
                                Settlement rwdTown = rwd.FactionSettlements[j];
                                if (rwdTown.Tile == wos.Tile)
                                {
                                    hasSettlement = true;
                                    break;
                                }
                            }
                        }
                        if(!hasSettlement)
                        {
                            WorldUtility.CreateRimWarSettlement(rwd, wos);
                        }
                    }
                }
                //look for settlements assigned without corresponding world objects
                for(int i =0; i < rwd.FactionSettlements.Count; i++)
                {
                    Settlement rwdTown = rwd.FactionSettlements[i];
                    bool hasWorldObject = false;
                    for (int j =0; j < worldObjects.Count; j++)
                    {
                        RimWorld.Planet.Settlement wos = worldObjects[j] as RimWorld.Planet.Settlement;
                        if(wos != null && wos.Tile == rwdTown.Tile && wos.Faction == rwdTown.Faction)
                        {
                            hasWorldObject = true;
                            break;
                        }
                    }
                    if(!hasWorldObject)
                    {
                        rwd.FactionSettlements.Remove(rwdTown);
                        break;
                    }
                }
            }
        }

       private void AttemptWarbandActionAgainstTown(RimWarData rwd, Settlement rwdTown)
        {
            //Log.Message("attempting warband action");
            if (rwd != null && rwdTown != null)
            {
                Options.SettingsRef settingsRef = new Options.SettingsRef();
                int targetRange = Mathf.RoundToInt(rwdTown.RimWarPoints / (1.25f * settingsRef.settlementScanRangeDivider));
                if (rwd.behavior == RimWarBehavior.Warmonger)
                {
                    targetRange = Mathf.RoundToInt(targetRange * 1.4f);
                }
                else if (rwd.behavior == RimWarBehavior.Cautious)
                {
                    targetRange = Mathf.RoundToInt(targetRange * .8f);
                }
                List<Settlement> tmpSettlements = new List<Settlement>();
                if (settingsRef.forceRandomObject)
                {
                    tmpSettlements = rwd.HostileSettlements;
                }
                else
                {
                    tmpSettlements = rwdTown.NearbyHostileSettlements;
                }
                if (rwd.IsAtWar)
                {
                    Settlement warSettlement = WorldUtility.GetClosestRimWarSettlementOfFaction(rwd.WarFactions.RandomElement(), rwdTown.Tile, Mathf.Min(Mathf.RoundToInt((rwdTown.RimWarPoints * 1.5f) / (settingsRef.settlementScanRangeDivider)), (int)settingsRef.maxSettelementScanRange));
                    if (warSettlement != null)
                    {
                        tmpSettlements.Add(warSettlement);
                    }
                    targetRange = Mathf.RoundToInt(targetRange * 1.5f);
                }
                if (tmpSettlements != null && tmpSettlements.Count > 0)
                {
                    Settlement targetTown = tmpSettlements.RandomElement();
                    if (targetTown != null && Find.WorldGrid.TraversalDistanceBetween(rwdTown.Tile, targetTown.Tile) <= targetRange)
                    {
                        //Log.Message("" + rwdTown.RimWorld_Settlement.Name + " with " + rwdTown.RimWarPoints + " evaluating " + targetTown.RimWorld_Settlement.Name + " with " + targetTown.RimWarPoints);
                        int pts = WorldUtility.CalculateWarbandPointsForRaid(targetTown);                        
                        if (rwd.behavior == RimWarBehavior.Cautious)
                        {
                            pts = Mathf.RoundToInt(pts * 1.1f);
                        }
                        else if (rwd.behavior == RimWarBehavior.Warmonger)
                        {
                            pts = Mathf.RoundToInt(pts * 1.25f);
                        }

                        if (rwd.IsAtWarWith(targetTown.Faction))
                        {
                            pts = Mathf.RoundToInt(pts * 1.2f);
                            if (rwdTown.RimWarPoints * .85f >= pts)
                            {
                                WorldUtility.CreateWarband(pts, rwd, rwdTown, rwdTown.Tile, targetTown.Tile, WorldObjectDefOf.Settlement);
                                rwdTown.RimWarPoints = rwdTown.RimWarPoints - pts;
                            }
                        }
                        else if (rwdTown.RimWarPoints * .75f >= pts)
                        {
                            //Log.Message("sending warband from " + rwdTown.RimWorld_Settlement.Name);
                            WorldUtility.CreateWarband(pts, rwd, rwdTown, rwdTown.Tile, targetTown.Tile, WorldObjectDefOf.Settlement);
                            rwdTown.RimWarPoints = rwdTown.RimWarPoints - pts;
                        }

                    }
                }
            }
            else
            {
                Log.Warning("Found null when attempting to generate a warband: rwd " + rwd + " rwdTown " + rwdTown);
            }
        }

        private void AttemptLaunchedWarbandAgainstTown(RimWarData rwd, Settlement rwdTown)
        {
            //Log.Message("attempting launched warband action");
            if (rwd != null && rwdTown != null)
            {
                if (rwdTown.RimWarPoints >= 1000)
                {
                    Options.SettingsRef settingsRef = new Options.SettingsRef();
                    int targetRange = Mathf.RoundToInt(rwdTown.RimWarPoints / (.5f * settingsRef.settlementScanRangeDivider));
                    if (rwd.behavior == RimWarBehavior.Warmonger)
                    {
                        targetRange = Mathf.RoundToInt(targetRange * 1.25f);
                    }
                    else if (rwd.behavior == RimWarBehavior.Cautious)
                    {
                        targetRange = Mathf.RoundToInt(targetRange * .8f);
                    }
                    List<Settlement> tmpSettlements = new List<Settlement>();
                    if (settingsRef.forceRandomObject)
                    {
                        tmpSettlements = rwd.HostileSettlements;
                    }
                    else
                    {
                        tmpSettlements = rwdTown.NearbyHostileSettlements;
                    }
                    if(rwd.IsAtWar)
                    {
                        Settlement warSettlement = WorldUtility.GetClosestRimWarSettlementOfFaction(rwd.WarFactions.RandomElement(), rwdTown.Tile, Mathf.Min(Mathf.RoundToInt((rwdTown.RimWarPoints * 1.5f) / (settingsRef.settlementScanRangeDivider)), (int)settingsRef.maxSettelementScanRange));
                        if(warSettlement != null)
                        {
                            tmpSettlements.Add(warSettlement);
                        }
                        targetRange = Mathf.RoundToInt(targetRange * 1.5f);
                    }
                    if (tmpSettlements != null && tmpSettlements.Count > 0)
                    {
                        Settlement targetTown = tmpSettlements.RandomElement();
                        if (targetTown != null && Find.WorldGrid.ApproxDistanceInTiles(rwdTown.Tile, targetTown.Tile) <= targetRange)
                        {
                            //Log.Message("" + rwdTown.RimWorld_Settlement.Name + " with " + rwdTown.RimWarPoints + " evaluating " + targetTown.RimWorld_Settlement.Name + " with " + targetTown.RimWarPoints);
                            int pts = WorldUtility.CalculateWarbandPointsForRaid(targetTown);
                            if (rwd.behavior == RimWarBehavior.Cautious)
                            {
                                pts = Mathf.RoundToInt(pts * 1.1f);
                            }
                            else if (rwd.behavior == RimWarBehavior.Warmonger)
                            {
                                pts = Mathf.RoundToInt(pts * 1.25f);
                            }

                            if(rwd.IsAtWarWith(targetTown.Faction))
                            {
                                pts = Mathf.RoundToInt(pts * 1.2f);
                                if(rwdTown.RimWarPoints * .8f >= pts)
                                {
                                    WorldUtility.CreateLaunchedWarband(pts, rwd, rwdTown, rwdTown.Tile, targetTown.Tile, WorldObjectDefOf.Settlement);
                                    rwdTown.RimWarPoints = rwdTown.RimWarPoints - pts;
                                }
                            }
                            else if (rwdTown.RimWarPoints * .6f >= pts)
                            {
                                //Log.Message("launching warband from " + rwdTown.RimWorld_Settlement.Name);
                                WorldUtility.CreateLaunchedWarband(pts, rwd, rwdTown, rwdTown.Tile, targetTown.Tile, WorldObjectDefOf.Settlement);
                                rwdTown.RimWarPoints = rwdTown.RimWarPoints - pts;
                            }
                        }
                    }
                }
            }
            else
            {
                Log.Warning("Found null when attempting to generate a warband: rwd " + rwd + " rwdTown " + rwdTown);
            }
        }

        private void AttemptScoutMission(RimWarData rwd, Settlement rwdTown)
        {
            if (rwd != null && rwdTown != null)
            {
                Options.SettingsRef settingsRef = new Options.SettingsRef();
                int targetRange = Mathf.RoundToInt(rwdTown.RimWarPoints / settingsRef.settlementScanRangeDivider);
                if(rwd.behavior == RimWarBehavior.Expansionist)
                {
                    targetRange = Mathf.RoundToInt(targetRange * 1.5f);
                }
                else if(rwd.behavior == RimWarBehavior.Warmonger)
                {
                    targetRange = Mathf.RoundToInt(targetRange * 1.25f);
                }
                else if(rwd.behavior == RimWarBehavior.Aggressive)
                {
                    targetRange = Mathf.RoundToInt(targetRange * 1.15f);
                }
                List<WorldObject> worldObjects = new List<WorldObject>();
                if (settingsRef.forceRandomObject)
                {
                    worldObjects.Add(Find.WorldObjects.AllWorldObjects.RandomElement());
                }
                else
                {
                    worldObjects = WorldUtility.GetWorldObjectsInRange(rwdTown.Tile, targetRange);
                }
                if (worldObjects != null && worldObjects.Count > 0)
                {
                    for (int i = 0; i < worldObjects.Count; i++)
                    {
                        WorldObject wo = worldObjects[i];
                        if (wo.Faction != null && wo.Faction.HostileTo(rwd.RimWarFaction) && Find.WorldGrid.ApproxDistanceInTiles(rwdTown.Tile, wo.Tile) <= targetRange)
                        {
                            if (wo is Caravan)
                            {
                                Caravan playerCaravan = wo as Caravan;
                                //Log.Message("evaluating scouting player caravan with " + playerCaravan.PlayerWealthForStoryteller + " wealth against town points of " + rwdTown.RimWarPoints);
                                //Log.Message("caravan is " + Find.WorldGrid.TraversalDistanceBetween(wo.Tile, rwdTown.Tile) + " tiles away, with a town range of " + targetRange + " visibility reduced to a range of " + Mathf.RoundToInt(targetRange * playerCaravan.Visibility));
                                if ((playerCaravan.PlayerWealthForStoryteller / 200) <= (rwdTown.RimWarPoints * .5f) && (Find.WorldGrid.TraversalDistanceBetween(wo.Tile, rwdTown.Tile) <= Mathf.RoundToInt(targetRange * playerCaravan.Visibility)))
                                {
                                    int pts = WorldUtility.CalculateScoutMissionPoints(rwd, Mathf.RoundToInt(playerCaravan.PlayerWealthForStoryteller / 200));                                    
                                    WorldUtility.CreateScout(pts, rwd, rwdTown, rwdTown.Tile, wo.Tile, WorldObjectDefOf.Caravan);
                                    rwdTown.RimWarPoints = rwdTown.RimWarPoints - pts;
                                    break;
                                }
                            }
                            else if (wo is WarObject)
                            {
                                WarObject warObject = wo as WarObject;
                                if (warObject.RimWarPoints <= (rwdTown.RimWarPoints * .5f))
                                {
                                    int pts = WorldUtility.CalculateScoutMissionPoints(rwd, warObject.RimWarPoints);
                                    if(rwd.IsAtWarWith(warObject.Faction))
                                    {
                                        pts = Mathf.RoundToInt(pts * 1.2f);
                                    }
                                    WorldUtility.CreateScout(pts, rwd, rwdTown, rwdTown.Tile, wo.Tile, RimWarDefOf.RW_WarObject);
                                    rwdTown.RimWarPoints = rwdTown.RimWarPoints - pts;
                                    break;
                                }
                            }
                            else if (wo is RimWorld.Planet.Settlement)
                            {
                                Settlement settlement = WorldUtility.GetRimWarSettlementAtTile(wo.Tile);
                                if(settlement != null && settlement.RimWarPoints <= (rwdTown.RimWarPoints * .5f))
                                {
                                    int pts = WorldUtility.CalculateScoutMissionPoints(rwd, settlement.RimWarPoints);
                                    if (rwd.IsAtWarWith(settlement.Faction))
                                    {
                                        pts = Mathf.RoundToInt(pts * 1.2f);
                                    }
                                    WorldUtility.CreateScout(pts, rwd, rwdTown, rwdTown.Tile, wo.Tile, WorldObjectDefOf.Settlement);
                                    rwdTown.RimWarPoints = rwdTown.RimWarPoints - pts;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                Log.Warning("Found null when attempting to generate a scout: rwd " + rwd + " rwdTown " + rwdTown);
            }
        }

        private void AttemptSettlerMission(RimWarData rwd, Settlement rwdTown)
        {
            if (rwd != null && rwdTown != null)
            {
                Options.SettingsRef settingsRef = new Options.SettingsRef();
                //Log.Message("attempting settler mission");
                if (rwdTown.RimWarPoints > 2000)
                {
                    int targetRange = Mathf.Clamp(Mathf.RoundToInt(rwdTown.RimWarPoints / settingsRef.settlementScanRangeDivider), 11, Mathf.Max((int)settingsRef.maxSettelementScanRange, 12));
                    if (rwd.behavior == RimWarBehavior.Expansionist)
                    {
                        targetRange = Mathf.RoundToInt(targetRange * 1.2f);
                    }
                    else if (rwd.behavior == RimWarBehavior.Warmonger)
                    {
                        targetRange = Mathf.RoundToInt(targetRange * .8f);
                    }
                    List<int> tmpTiles = new List<int>();
                    tmpTiles.Clear();
                    for (int i = 0; i < 5; i++)
                    {
                        int tile = -1;
                        //TileFinder.TryFindNewSiteTile(out tile, 10, targetRange, true, true, rwdTown.Tile);
                        TileFinder.TryFindPassableTileWithTraversalDistance(rwdTown.Tile, 10, targetRange, out tile);
                        if (tile != -1)
                        {
                            tmpTiles.Add(tile);
                        }
                    }
                    if (tmpTiles != null && tmpTiles.Count > 0)
                    {
                        for (int i = 0; i < tmpTiles.Count; i++)
                        {
                            int destinationTile = tmpTiles[i];                                
                            if (destinationTile > 0 && Find.WorldGrid.TraversalDistanceBetween(rwdTown.Tile, destinationTile) <= targetRange)
                            {
                                //Log.Message("Settler: " + rwdTown.RimWorld_Settlement.Name + " with " + rwdTown.RimWarPoints + " evaluating " + destinationTile + " for settlement");
                                List<WorldObject> worldObjects = WorldUtility.GetWorldObjectsInRange(destinationTile, 10);
                                bool nearbySettlement = false;
                                for (int j = 0; j < worldObjects.Count; j++)
                                {
                                    if(worldObjects[j].def == WorldObjectDefOf.Settlement)
                                    {
                                        nearbySettlement = true;
                                    }
                                }
                                if (!nearbySettlement)
                                {
                                    int pts = Mathf.RoundToInt(Rand.Range(.4f, .6f) * 2000);
                                    //Log.Message("sending settler from " + rwdTown.RimWorld_Settlement.Name);
                                    WorldUtility.CreateSettler(pts, rwd, rwdTown, rwdTown.Tile, destinationTile, null);
                                    rwdTown.RimWarPoints = rwdTown.RimWarPoints - pts;
                                    break;
                                }
                            }
                        }
                        //Log.Message("completed search for nearby tile");
                    }
                }
            }
            else
            {
                Log.Warning("Found null when attempting to generate a settler: rwd " + rwd + " rwdTown " + rwdTown);
            }
        }

        private void AttemptTradeMission(RimWarData rwd, Settlement rwdTown)
        {
            if (rwd != null && rwdTown != null)
            {
                Options.SettingsRef settingsRef = new Options.SettingsRef();
                if (rwdTown.RimWarPoints > 1000)
                {
                    int targetRange = Mathf.RoundToInt(rwdTown.RimWarPoints / settingsRef.settlementScanRangeDivider);
                    if (rwd.behavior == RimWarBehavior.Expansionist)
                    {
                        targetRange = Mathf.RoundToInt(targetRange * 1.25f);
                    }
                    else if (rwd.behavior == RimWarBehavior.Warmonger)
                    {
                        targetRange = Mathf.RoundToInt(targetRange * .8f);
                    }
                    else if (rwd.behavior == RimWarBehavior.Merchant)
                    {
                        targetRange = Mathf.RoundToInt(targetRange * 1.5f);
                    }
                    List<Settlement> tmpSettlements = new List<Settlement>();
                    if (settingsRef.forceRandomObject)
                    {
                        tmpSettlements.Add(rwd.NonHostileSettlements.RandomElement());
                    }
                    else
                    {
                        tmpSettlements = rwdTown.NearbyFriendlySettlements.ToList();
                    }
                    if (tmpSettlements != null && tmpSettlements.Count > 0)
                    {
                        Settlement targetTown = tmpSettlements.RandomElement();
                        if (targetTown != null && Find.WorldGrid.TraversalDistanceBetween(rwdTown.Tile, targetTown.Tile) <= targetRange)
                        {
                            //Log.Message("Trader: " + rwdTown.RimWorld_Settlement.Name + " with " + rwdTown.RimWarPoints + " evaluating " + targetTown.RimWorld_Settlement.Name + " with " + targetTown.RimWarPoints);
                            int pts = WorldUtility.CalculateWarbandPointsForRaid(targetTown);
                            if (rwd.behavior == RimWarBehavior.Cautious)
                            {
                                pts = Mathf.RoundToInt(pts * 1.1f);
                            }
                            else if (rwd.behavior == RimWarBehavior.Warmonger)
                            {
                                pts = Mathf.RoundToInt(pts * .8f);
                            }
                            else if (rwd.behavior == RimWarBehavior.Merchant)
                            {
                                pts = Mathf.RoundToInt(pts * 1.3f);
                            }
                            int maxPts = Mathf.RoundToInt(rwdTown.RimWarPoints * .5f);
                            if (maxPts >= pts)
                            {
                                //Log.Message("sending warband from " + rwdTown.RimWorld_Settlement.Name);
                                WorldUtility.CreateTrader(pts, rwd, rwdTown, rwdTown.Tile, targetTown.Tile, WorldObjectDefOf.Settlement);
                                rwdTown.RimWarPoints = rwdTown.RimWarPoints - pts;
                            }
                            else
                            {
                                WorldUtility.CreateTrader(maxPts, rwd, rwdTown, rwdTown.Tile, targetTown.Tile, WorldObjectDefOf.Settlement);
                                rwdTown.RimWarPoints = rwdTown.RimWarPoints - maxPts;
                            }
                        }
                    }
                }
            }
            else
            {
                Log.Warning("Found null when attempting to generate a trader: rwd " + rwd + " rwdTown " + rwdTown);
            }
        }

        private void AttemptDiplomatMission(RimWarData rwd, Settlement rwdTown)
        {
            if (rwd != null && rwdTown != null)
            {
                Options.SettingsRef settingsRef = new Options.SettingsRef();
                if (rwdTown.RimWarPoints > 1000)
                {
                    int targetRange = Mathf.RoundToInt(rwdTown.RimWarPoints / settingsRef.settlementScanRangeDivider);
                    if (rwd.behavior == RimWarBehavior.Merchant || rwd.behavior == RimWarBehavior.Expansionist)
                    {
                        targetRange = Mathf.RoundToInt(targetRange * 1.25f);
                    }
                    List<Settlement> tmpSettlements = new List<Settlement>();
                    if (settingsRef.forceRandomObject)
                    {
                        tmpSettlements.Add(rwd.NonHostileSettlements.RandomElement());
                        tmpSettlements.Add(rwd.HostileSettlements.RandomElement());
                    }
                    else
                    {
                        tmpSettlements = WorldUtility.GetRimWarSettlementsInRange(rwdTown.Tile, targetRange, this.RimWarData, rwd);
                    }
                    if (tmpSettlements != null && tmpSettlements.Count > 0)
                    {
                        Settlement targetTown = tmpSettlements.RandomElement();
                        if (targetTown != null && Find.WorldGrid.TraversalDistanceBetween(rwdTown.Tile, targetTown.Tile) <= targetRange)
                        {
                            //Log.Message("Diplomat: " + rwdTown.RimWorld_Settlement.Name + " with " + rwdTown.RimWarPoints + " evaluating " + targetTown.RimWorld_Settlement.Name + " with " + targetTown.RimWarPoints);
                            int pts = WorldUtility.CalculateDiplomatPoints(rwdTown);
                            if (rwd.behavior == RimWarBehavior.Cautious)
                            {
                                pts = Mathf.RoundToInt(pts * 1.1f);
                            }
                            else if (rwd.behavior == RimWarBehavior.Warmonger)
                            {
                                pts = Mathf.RoundToInt(pts * .8f);
                            }
                            else if (rwd.behavior == RimWarBehavior.Merchant)
                            {
                                pts = Mathf.RoundToInt(pts * 1.3f);
                            }
                            float maxPts = rwdTown.RimWarPoints * .5f;
                            if (maxPts >= pts)
                            {
                                //Log.Message("sending warband from " + rwdTown.RimWorld_Settlement.Name);
                                WorldUtility.CreateDiplomat(pts, rwd, rwdTown, rwdTown.Tile, targetTown.Tile, WorldObjectDefOf.Settlement);
                                rwdTown.RimWarPoints = rwdTown.RimWarPoints - pts;
                            }
                        }
                    }
                }
            }
            else
            {
                Log.Warning("Found null when attempting to generate a diplomat: rwd " + rwd + " rwdTown " + rwdTown);
            }
        }

        public bool SettlementHasUniqueID(int id)
        {
            bool isUnique = true;
            for(int i = 0; i < this.RimWarData.Count; i++)
            {
                for(int j = 0; j < this.RimWarData[i].FactionSettlements.Count; j++)
                {
                    if(this.RimWarData[i].FactionSettlements[j] != null && this.RimWarData[i].FactionSettlements[j].GetUniqueLoadID() == id.ToString())
                    {
                        isUnique = false;
                    }
                }
            }
            return isUnique;
        }
    }
}
