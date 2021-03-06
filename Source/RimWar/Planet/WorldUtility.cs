﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using RimWar.History;
using Verse;
using UnityEngine;
using HarmonyLib;

namespace RimWar.Planet
{
    public class WorldUtility
    {
        public IncidentParms parms = new IncidentParms();

        private static List<Pawn> tmpPawns = new List<Pawn>();

        private static WorldComponent_PowerTracker wcpt = null;
        public static WorldComponent_PowerTracker Get_WCPT()
        {
            if (wcpt == null)
            {
                List<WorldComponent> components = Find.World.components;
                for (int i = 0; i < components.Count; i++)
                {
                    WorldComponent_PowerTracker cp = components[i] as WorldComponent_PowerTracker;
                    if (cp != null)
                    {
                        wcpt = cp;
                    }
                }
            }
            return wcpt;
        }

        public static RimWarData GetRimWarDataForFaction(Faction faction)
        {
            if(faction != null)
            {
                WorldComponent_PowerTracker wcpt = Get_WCPT();
                if (wcpt != null)
                {
                    for(int j = 0; j < wcpt.RimWarData.Count; j++)
                    {
                        if(wcpt.RimWarData[j].RimWarFaction == faction)
                        {
                            return wcpt.RimWarData[j];                                
                        }
                    }
                }                
            }            
            return null;
        }

        public static List<RimWarData> GetRimWarData()
        {
            WorldComponent_PowerTracker wcpt = Get_WCPT();
            if (wcpt != null)
            {
                return wcpt.RimWarData;
            }            
            return null;
        }

        public static void CreateRimWarSettlement(RimWarData rwd, WorldObject wo)
        {
            if (rwd != null && Find.FactionManager.AllFactions.Contains(rwd.RimWarFaction) && !rwd.RimWarFaction.defeated)
            {
                RimWar.Planet.Settlement rimwarSettlement = new Settlement(rwd.RimWarFaction);
                rimwarSettlement.RimWarPoints = Mathf.RoundToInt(WorldUtility.CalculateSettlementPoints(wo, wo.Faction) * Rand.Range(.5f, 1.5f));
                rwd.FactionSettlements.Add(rimwarSettlement);
                rimwarSettlement.Tile = wo.Tile;
                //Log.Message("settlement: " + wo.Label + " contributes " + rimwarSettlement.RimWarPoints + " points");

                if (Find.TickManager.TicksGame > 20)
                {
                    RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_SettlementEvent);
                    let.label = "RW_LetterSettlementEvent".Translate();
                    let.text = "RW_LetterSettlementEventText".Translate(rwd.RimWarFaction, rimwarSettlement.RimWarPoints);
                    let.lookTargets = wo;
                    let.relatedFaction = rwd.RimWarFaction;
                    RW_LetterMaker.Archive_RWLetter(let);
                }
            }
        }

        public static void CreateRimWarSettlementWithPoints(RimWarData rwd, WorldObject wo, int points, bool displayLetter = false)
        {
            if (rwd != null && Find.FactionManager.AllFactions.Contains(rwd.RimWarFaction) && !rwd.RimWarFaction.defeated)
            {
                RimWar.Planet.Settlement rimwarSettlement = new Settlement(rwd.RimWarFaction);
                rimwarSettlement.RimWarPoints = points;
                rwd.FactionSettlements.Add(rimwarSettlement);
                rimwarSettlement.Tile = wo.Tile;
                //Log.Message("settlement: " + wo.Label + " contributes " + rimwarSettlement.RimWarPoints + " points");

                if (Find.TickManager.TicksGame > 20 && displayLetter)
                {
                    RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_SettlementEvent);
                    let.label = "RW_LetterSettlementEvent".Translate();
                    let.text = "RW_LetterSettlementEventText".Translate(rwd.RimWarFaction, rimwarSettlement.RimWarPoints);
                    let.lookTargets = wo;
                    let.relatedFaction = rwd.RimWarFaction;
                    RW_LetterMaker.Archive_RWLetter(let);
                }
            }
        }

        public static void CreateSettlement(WarObject warObject, List<WorldObject> objectsHere, RimWarData rwd, int tile, Faction faction)
        {
            //Log.Message("creating settlement");     
            if (ValidForSettlement(warObject, objectsHere, rwd, tile, faction))
            {
                RimWorld.Planet.Settlement worldSettlement = SettleUtility.AddNewHome(tile, faction);
                if (warObject != null)
                {
                    CreateRimWarSettlement(rwd, worldSettlement);
                }
            }
            else
            {
                Settler settler = warObject as Settler;
                if(settler != null && settler.ParentSettlement != null && settler.ParentSettlement.Tile != tile)
                {
                    ConsolidatePoints reconstitute = new ConsolidatePoints(Mathf.RoundToInt(.6f * settler.RimWarPoints), Mathf.RoundToInt(Find.WorldGrid.TraversalDistanceBetween(settler.Tile, settler.ParentSettlement.Tile) * settler.TicksPerMove) + Find.TickManager.TicksGame);
                    settler.ParentSettlement.SettlementPointGains.Add(reconstitute);
                }
            }
        }

        public static bool ValidForSettlement(WarObject warObject, List<WorldObject> objectsHere, RimWarData rwd, int tile, Faction faction)
        {
            if(warObject != null && !warObject.Destroyed && objectsHere != null && rwd != null && rwd.FactionSettlements != null && rwd.FactionSettlements.Count > 0 && faction != null && !faction.defeated)
            {
                for(int i = 0; i < objectsHere.Count; i++)
                {                    
                    if(objectsHere[i].Tile == tile && !(objectsHere[i] is WarObject))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        public static void ConvertSettlement(RimWorld.Planet.Settlement worldSettlement, RimWar.Planet.Settlement rimwarSettlement, RimWarData rwdFrom, RimWarData rwdTo, int points)
        {
            int tile = worldSettlement.Tile;
            if(worldSettlement != null && rwdFrom != null && rwdTo != null)
            {
                worldSettlement.Destroy();
                rwdFrom.FactionSettlements.Remove(rimwarSettlement);
                Find.World.WorldUpdate();
                RimWorld.Planet.Settlement newSettlement = SettleUtility.AddNewHome(tile, rwdTo.RimWarFaction);
                CreateRimWarSettlementWithPoints(rwdTo, newSettlement, points, false);
                Find.World.WorldUpdate();
                if(rwdFrom.FactionSettlements.Count <= 0)
                {
                    RemoveRWDFaction(rwdFrom);
                }
            }
        }

        public static void RemoveRWDFaction(RimWarData rwdFrom)
        {
            for (int i = 0; i < Find.WorldObjects.AllWorldObjects.Count; i++)
            {
                WorldObject wo = Find.WorldObjects.AllWorldObjects[i];
                if (wo.Faction == rwdFrom.RimWarFaction)
                {
                    Find.WorldObjects.Remove(wo);
                }
            }
            WorldUtility.GetRimWarData().Remove(rwdFrom);
        }

        public static void CreateWarObject(int points, Faction faction, int startingTile, int destinationTile, WorldObjectDef type)
        {
            //Log.Message("creating war object");
            WarObject warObject = new WarObject();
            warObject = MakeWarObject(10, faction, startingTile, destinationTile, type, true);
            if (!warObject.pather.Moving && warObject.Tile != destinationTile)
            {
                warObject.pather.StartPath(destinationTile, true, true);
                warObject.pather.nextTileCostLeft /= 2f;
                warObject.tweener.ResetTweenedPosToRoot();
            }

        }

        private static WarObject MakeWarObject(int points, Faction faction, int startingTile, int destinationTile, WorldObjectDef type, bool addToWorldPawnsIfNotAlready)
        {
            //Log.Message("making world object");
            WarObject warObject = (WarObject)WorldObjectMaker.MakeWorldObject(RimWarDefOf.RW_WarObject);
            if (startingTile >= 0)
            {
                warObject.Tile = startingTile;
            }
            warObject.SetFaction(faction);
            if (startingTile >= 0)
            {
                Find.WorldObjects.Add(warObject);
            }            
            warObject.Name = "default war object";
            warObject.SetUniqueId(Find.UniqueIDsManager.GetNextCaravanID());
            
            return warObject;
        }

        public static void CreateWarObjectOfType(WarObject warObject, int power, RimWarData rwd, Settlement parentSettlement, int startingTile, int destinationTile, WorldObjectDef worldDef)
        {
            if(warObject is Warband)
            {
                CreateWarband(power, rwd, parentSettlement, startingTile, destinationTile, worldDef);
            }
            else if(warObject is Scout)
            {
                CreateScout(power, rwd, parentSettlement, startingTile, destinationTile, worldDef);
            }
            else if(warObject is Trader)
            {
                CreateTrader(power, rwd, parentSettlement, startingTile, destinationTile, worldDef);
            }
            else if(warObject is Diplomat)
            {
                CreateDiplomat(power, rwd, parentSettlement, startingTile, destinationTile, worldDef);
            }
            else if(warObject is Settler)
            {
                CreateSettler(power, rwd, parentSettlement, startingTile, destinationTile, worldDef);
            }
            else
            {
                Log.Warning("Attempted to create WarObject but object is not a known type.");
            }
        }

        private static void CreateWarband_Caravan(int power, Faction faction, int startingTile, int destinationTile, IIncidentTarget target)
        {
            //Log.Message("generating warband");
            //Log.Message("storyteller threat points of target is " + StorytellerUtility.DefaultThreatPointsNow(target));
            Warband_Caravan warband = new Warband_Caravan();
            IncidentParms parms = new IncidentParms();
            PawnGroupKindDef combat = PawnGroupKindDefOf.Combat;
            parms.faction = faction;
            parms.generateFightersOnly = true;
            parms.raidArrivalMode = IncidentUtility.PawnsArrivalModeOrRandom(PawnsArrivalModeDefOf.EdgeWalkIn);
            parms.target = target;
            //parms = ResolveRaidStrategy(parms, combat);
            //parms.points = AdjustedRaidPoints((float)power, parms.raidArrivalMode, parms.raidStrategy, faction, combat);
            //Log.Message("adjusted points " + parms.points);
            PawnGroupMakerParms warbandPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(combat, parms);
            List<Pawn> warbandPawnList = PawnGroupMakerUtility.GeneratePawns(warbandPawnGroupMakerParms).ToList();
            if(warbandPawnList.Count == 0)
            {
                Log.Error("Tried to create a warband without points");
                return;
            }
            //for(int i = 0; i < warbandPawnList.Count; i++)
            //{                
            //    warband.AddPawn(warbandPawnList[i], true);
            //    warbandPawnList[i].Notify_PassedToWorld();                
            //}
            warband = MakeWarband_Caravan(warbandPawnList, faction, startingTile, true);
            warband.parms = parms;
            if(!warband.pather.Moving && warband.Tile != destinationTile)
            {
                warband.pather.StartPath(destinationTile, null, repathImmediately: true);
                warband.pather.nextTileCostLeft /= 2f;
                warband.tweener.ResetTweenedPosToRoot();
            }
            //Log.Message("end create warband");
        }

        private static Warband_Caravan MakeWarband_Caravan(List<Pawn> pawns, Faction faction, int startingTile, bool addToWorldPawnsIfNotAlready)
        {
            //Log.Message("making world object warband");
            tmpPawns.Clear();
            tmpPawns.AddRange(pawns);
            //Caravan warband = (Caravan)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Caravan);
            Warband_Caravan warband = (Warband_Caravan)WorldObjectMaker.MakeWorldObject(RimWarDefOf.RW_Warband);
            if (startingTile >= 0)
            {
                warband.Tile = startingTile;
            }
            warband.SetFaction(faction);
            if (startingTile >= 0)
            {
                Find.WorldObjects.Add(warband);
            }
            for (int i = 0; i < tmpPawns.Count; i++)
            {
                Pawn pawn = tmpPawns[i];
                if (pawn.Dead)
                {
                    Log.Warning("Tried to form a caravan with a dead pawn " + pawn);
                }
                else
                {
                    //Log.Message("adding pawn " + pawn.LabelShort);
                    warband.AddPawn(pawn, addToWorldPawnsIfNotAlready);
                    if (addToWorldPawnsIfNotAlready && !pawn.IsWorldPawn())
                    {
                        Find.WorldPawns.PassToWorld(pawn);
                    }
                }
            }
            warband.Name = CaravanNameGenerator.GenerateCaravanName(warband);
            tmpPawns.Clear();
            warband.SetUniqueId(Find.UniqueIDsManager.GetNextCaravanID());
            
            return warband;
        }        

        public static void CreateWarband(int power, RimWarData rwd, Settlement parentSettlement, int startingTile, int destinationTile, WorldObjectDef worldDef, bool launched = false)
        {
            //Log.Message("generating warband for " + rwd.RimWarFaction.Name + " from " + startingTile + " to " + destinationTile);
            try
            {
                Options.SettingsRef settingsRef = new Options.SettingsRef();
                Warband warband = new Warband();
                warband = MakeWarband(rwd.RimWarFaction, startingTile);
                warband.ParentSettlement = parentSettlement;
                warband.MovesAtNight = rwd.movesAtNight;
                warband.RimWarPoints = power;
                warband.launched = launched;
                warband.TicksPerMove = (int)(warband.TicksPerMove / settingsRef.objectMovementMultiplier);
                warband.DestinationTarget = Find.WorldObjects.WorldObjectOfDefAt(worldDef, destinationTile);                
                if (rwd.behavior == RimWarBehavior.Warmonger)
                {
                    warband.TicksPerMove = (int)(warband.TicksPerMove * .9f);
                }
                if (rwd.behavior == RimWarBehavior.Merchant)
                {
                    warband.TicksPerMove = (int)(warband.TicksPerMove * 1.1f);
                }
                if (launched)
                {
                    warband.ArrivalAction();
                }
                else if (!warband.pather.Moving && warband.Tile != destinationTile)
                {
                    warband.pather.StartPath(destinationTile, true);
                    warband.pather.nextTileCostLeft /= 2f;
                    warband.tweener.ResetTweenedPosToRoot();
                }
            }
            catch (NullReferenceException ex)
            {
                Log.Message("failed to create warband\n rwd: " + rwd + " parent " + parentSettlement + " start " + startingTile + " end " + destinationTile + " def " + worldDef + "\n" + ex);
            }
            
            //Log.Message("end create warband");
        }

        private static Warband MakeWarband(Faction faction, int startingTile)
        {
            Warband warband = (Warband)WorldObjectMaker.MakeWorldObject(RimWarDefOf.RW_Warband);
            if (startingTile >= 0)
            {
                warband.Tile = startingTile;
            }
            warband.SetFaction(faction);
            if (startingTile >= 0)
            {
                Find.WorldObjects.Add(warband);
            }
            warband.Name = "RW_WarbandName".Translate(faction.Name);            
            warband.SetUniqueId(Find.UniqueIDsManager.GetNextCaravanID());
            return warband;
        }

        public static void CreateLaunchedWarband(int power, RimWarData rwd, Settlement parentSettlement, int startingTile, int destinationTile, WorldObjectDef worldDef)
        {
            //Log.Message("generating warband for " + rwd.RimWarFaction.Name + " from " + startingTile + " to " + destinationTile);
            try
            {
                LaunchedWarband warband = new LaunchedWarband();
                warband = MakeLaunchedWarband(rwd.RimWarFaction, startingTile);
                warband.ParentSettlement = parentSettlement;
                warband.RimWarPoints = power;

                if (warband.Tile != destinationTile)
                {
                    warband.DestinationTarget = Find.World.worldObjects.WorldObjectOfDefAt(worldDef, destinationTile);
                    warband.destinationTile = destinationTile;
                }
            }
            catch (NullReferenceException ex)
            {
                Log.Message("failed to create warband\n rwd: " + rwd + " parent " + parentSettlement + " start " + startingTile + " end " + destinationTile + " def " + worldDef + "\n" + ex);
            }
            //Log.Message("end create warband");
        }

        private static LaunchedWarband MakeLaunchedWarband(Faction faction, int startingTile)
        {
            LaunchedWarband warband = (LaunchedWarband)WorldObjectMaker.MakeWorldObject(RimWarDefOf.RW_LaunchedWarband);
            if (startingTile >= 0)
            {
                warband.Tile = startingTile;
            }
            warband.SetFaction(faction);
            if (startingTile >= 0)
            {
                Find.WorldObjects.Add(warband);
            }
            warband.Name = "RW_WarbandName".Translate(faction.Name);
            warband.SetUniqueId(Find.UniqueIDsManager.GetNextCaravanID());
            return warband;
        }

        public static void CreateScout(int power, RimWarData rwd, Settlement parentSettlement, int startingTile, int destinationTile, WorldObjectDef worldDef)
        {
            //Log.Message("generating scout for " + rwd.RimWarFaction.Name + " from " + startingTile + " to " + destinationTile);
            try
            { 
                Options.SettingsRef settingsRef = new Options.SettingsRef();
                Scout scout = new Scout();
                scout = MakeScout(rwd.RimWarFaction, startingTile);
                scout.ParentSettlement = parentSettlement;
                scout.MovesAtNight = rwd.movesAtNight;
                scout.RimWarPoints = power;
                scout.TicksPerMove = (int)(scout.TicksPerMove / settingsRef.objectMovementMultiplier);
                if (rwd.behavior == RimWarBehavior.Expansionist)
                {
                    scout.TicksPerMove = (int)(scout.TicksPerMove * .8f);
                }
                else if (rwd.behavior == RimWarBehavior.Aggressive)
                {
                    scout.TicksPerMove = (int)(scout.TicksPerMove * .9f);
                }
                else if(rwd.behavior == RimWarBehavior.Warmonger)
                {
                    scout.TicksPerMove = (int)(scout.TicksPerMove * .9f);
                }
                if (!scout.pather.Moving && scout.Tile != destinationTile)
                {
                    scout.pather.StartPath(destinationTile, true);
                    scout.pather.nextTileCostLeft /= 2f;
                    scout.tweener.ResetTweenedPosToRoot();
                    scout.DestinationTarget = Find.World.worldObjects.WorldObjectOfDefAt(worldDef, destinationTile);
                }
            }
            catch (NullReferenceException ex)
            {
                Log.Message("failed to create warband\n rwd: " + rwd + " parent " + parentSettlement + " start " + startingTile + " end " + destinationTile + " def " + worldDef + "\n" + ex);
            }
            //Log.Message("end create scout");
        }

        private static Scout MakeScout(Faction faction, int startingTile)
        {
            Scout scout = (Scout)WorldObjectMaker.MakeWorldObject(RimWarDefOf.RW_Scout);
            if (startingTile >= 0)
            {
                scout.Tile = startingTile;
            }
            scout.SetFaction(faction);
            if (startingTile >= 0)
            {
                Find.WorldObjects.Add(scout);
            }
            scout.Name = "RW_ScoutName".Translate(faction.Name);
            scout.SetUniqueId(Find.UniqueIDsManager.GetNextCaravanID());
            return scout;
        }

        public static Trader CreateTrader(int power, RimWarData rwd, Settlement parentSettlement, int startingTile, int destinationTile, WorldObjectDef worldDef)
        {
            //Log.Message("generating trader for " + rwd.RimWarFaction.Name + " from " + startingTile + " to " + destinationTile);
            try
            {
                Options.SettingsRef settingsRef = new Options.SettingsRef();
                Trader trader = new Trader();
                trader = MakeTrader(rwd.RimWarFaction, startingTile);
                trader.ParentSettlement = parentSettlement;
                trader.MovesAtNight = rwd.movesAtNight;
                trader.RimWarPoints = power;
                trader.TicksPerMove = (int)(trader.TicksPerMove / settingsRef.objectMovementMultiplier);
                if (rwd.behavior == RimWarBehavior.Expansionist)
                {
                    trader.TicksPerMove = (int)(trader.TicksPerMove * .9f);
                }
                else if (rwd.behavior == RimWarBehavior.Merchant)
                {
                    trader.TicksPerMove = (int)(trader.TicksPerMove * .8f);
                }
                else if (rwd.behavior == RimWarBehavior.Warmonger)
                {
                    trader.TicksPerMove = (int)(trader.TicksPerMove * 1.2f);
                }
                if (!trader.pather.Moving && trader.Tile != destinationTile)
                {
                    trader.pather.StartPath(destinationTile, true);
                    trader.pather.nextTileCostLeft /= 2f;
                    trader.tweener.ResetTweenedPosToRoot();
                    trader.DestinationTarget = Find.World.worldObjects.WorldObjectOfDefAt(worldDef, destinationTile);
                }
                return trader;
            }
            catch (NullReferenceException ex)
            {
                Log.Message("failed to create trader\n rwd: " + rwd + " parent " + parentSettlement + " start " + startingTile + " end " + destinationTile + " def " + worldDef + "\n" + ex);
            }
            return null;
            //Log.Message("end create trader");
        }

        private static Trader MakeTrader(Faction faction, int startingTile)
        {
            Trader trader = (Trader)WorldObjectMaker.MakeWorldObject(RimWarDefOf.RW_Trader);
            if (startingTile >= 0)
            {
                trader.Tile = startingTile;
            }
            trader.SetFaction(faction);
            if (startingTile >= 0)
            {
                Find.WorldObjects.Add(trader);
            }
            trader.Name = "RW_TraderName".Translate(faction.Name);
            trader.SetUniqueId(Find.UniqueIDsManager.GetNextCaravanID());
            return trader;
        }

        public static void CreateDiplomat(int power, RimWarData rwd, Settlement parentSettlement, int startingTile, int destinationTile, WorldObjectDef worldDef)
        {
            //Log.Message("generating diplomat for " + rwd.RimWarFaction.Name + " from " + startingTile + " to " + destinationTile);
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            Diplomat diplomat = new Diplomat();
            diplomat = MakeDiplomat(rwd.RimWarFaction, startingTile);
            diplomat.ParentSettlement = parentSettlement;
            diplomat.MovesAtNight = rwd.movesAtNight;
            diplomat.RimWarPoints = power;
            diplomat.TicksPerMove = (int)(diplomat.TicksPerMove / settingsRef.objectMovementMultiplier);
            if (rwd.behavior == RimWarBehavior.Expansionist)
            {
                diplomat.TicksPerMove = (int)(diplomat.TicksPerMove * .8f);
            }
            else if (rwd.behavior == RimWarBehavior.Merchant)
            {
                diplomat.TicksPerMove = (int)(diplomat.TicksPerMove * .9f);
            }
            else if (rwd.behavior == RimWarBehavior.Warmonger)
            {
                diplomat.TicksPerMove = (int)(diplomat.TicksPerMove * 1.2f);
            }
            if (!diplomat.pather.Moving && diplomat.Tile != destinationTile)
            {
                diplomat.pather.StartPath(destinationTile, true);
                diplomat.pather.nextTileCostLeft /= 2f;
                diplomat.tweener.ResetTweenedPosToRoot();
                diplomat.DestinationTarget = Find.World.worldObjects.WorldObjectOfDefAt(worldDef, destinationTile);
            }
            //Log.Message("end create diplomat");
        }

        private static Diplomat MakeDiplomat(Faction faction, int startingTile)
        {
            Diplomat diplomat = (Diplomat)WorldObjectMaker.MakeWorldObject(RimWarDefOf.RW_Diplomat);
            if (startingTile >= 0)
            {
                diplomat.Tile = startingTile;
            }
            diplomat.SetFaction(faction);
            if (startingTile >= 0)
            {
                Find.WorldObjects.Add(diplomat);
            }
            diplomat.Name = "RW_DiplomatName".Translate(faction.Name);
            diplomat.SetUniqueId(Find.UniqueIDsManager.GetNextCaravanID());
            return diplomat;
        }

        public static void CreateSettler(int power, RimWarData rwd, Settlement parentSettlement, int startingTile, int destinationTile, WorldObjectDef worldDef)
        {
            //Log.Message("generating Settler for " + rwd.RimWarFaction.Name + " from " + startingTile + " to " + destinationTile); 
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            Settler settler = new Settler();
            settler = MakeSettler(rwd.RimWarFaction, startingTile);
            settler.ParentSettlement = parentSettlement;
            settler.MovesAtNight = rwd.movesAtNight;
            settler.RimWarPoints = power;
            settler.DestinationTile = destinationTile;
            settler.TicksPerMove = (int)(settler.TicksPerMove / settingsRef.objectMovementMultiplier);
            if (rwd.behavior == RimWarBehavior.Expansionist)
            {
                settler.TicksPerMove = (int)(settler.TicksPerMove * .8f);
            }
            else if (rwd.behavior == RimWarBehavior.Merchant)
            {
                settler.TicksPerMove = (int)(settler.TicksPerMove * .9f);
            }
            else if (rwd.behavior == RimWarBehavior.Warmonger)
            {
                settler.TicksPerMove = (int)(settler.TicksPerMove * 1.2f);
            }
            if (!settler.pather.Moving && settler.Tile != destinationTile)
            {
                settler.pather.StartPath(destinationTile, true);
                settler.pather.nextTileCostLeft /= 2f;
                settler.tweener.ResetTweenedPosToRoot();
                
            }
            //Log.Message("end create settler");
        }

        private static Settler MakeSettler(Faction faction, int startingTile)
        {
            Settler settler = (Settler)WorldObjectMaker.MakeWorldObject(RimWarDefOf.RW_Settler);
            if (startingTile >= 0)
            {
                settler.Tile = startingTile;
            }
            settler.SetFaction(faction);
            if (startingTile >= 0)
            {
                Find.WorldObjects.Add(settler);
            }
            settler.Name = "RW_SettlerName".Translate(faction.Name);
            settler.SetUniqueId(Find.UniqueIDsManager.GetNextCaravanID());
            return settler;
        }

        public static int CalculateSettlementPoints(WorldObject worldObject, Faction faction)
        {
            if (faction != null)
            {
                int sum = 500;
                float techMultiplier = GetFactionTechLevelMultiplier(faction);
                sum = Mathf.RoundToInt(sum / techMultiplier);
                float biomeMultiplier = GetBiomeMultiplier(worldObject.Biome);
                sum = Mathf.RoundToInt(sum * biomeMultiplier);
                return sum;
            }
            else
            {
                Log.Warning("Tried to calculate settlement points without a valid faction");
                return 0;
            }            
        }

        public static int CalculateWarbandPointsForRaid(Settlement targetTown)
        {
            int pointsNeeded = 0;
            if (targetTown.Faction == Faction.OfPlayerSilentFail)
            {
                pointsNeeded = Mathf.RoundToInt(targetTown.RimWarPoints * 1.25f);
            }
            else
            {
                pointsNeeded = Mathf.RoundToInt(targetTown.RimWarPoints);
            }
            if(Rand.Value >= .8f)
            {
                //crushing attack
                pointsNeeded = Mathf.RoundToInt(Rand.Range(1.5f, 2.5f) * pointsNeeded);
            }
            else
            {
                pointsNeeded = Mathf.RoundToInt(Rand.Range(1f, 1.5f) * pointsNeeded);
            }
            return Mathf.Clamp(pointsNeeded, 50, 2000000);
        }

        public static int CalculateTraderPoints(Settlement targetTown)
        {
            int pointsNeeded = 0;
            if (targetTown.Faction == Faction.OfPlayerSilentFail)
            {
                pointsNeeded = targetTown.RimWarPoints;
            }
            else
            {
                pointsNeeded = Mathf.RoundToInt(targetTown.RimWarPoints * .5f);
            }

            pointsNeeded = Mathf.RoundToInt(Rand.Range(.9f, 1.5f) * pointsNeeded);
            
            return Mathf.Clamp(pointsNeeded, 500, 1000000);
        }

        public static int CalculateSettlerPoints(Settlement originTown)
        {
            int pointsNeeded = Mathf.RoundToInt(originTown.RimWarPoints * .5f);
            pointsNeeded = Mathf.RoundToInt(Rand.Range(.6f, 1.2f) * pointsNeeded);
            return Mathf.Clamp(pointsNeeded, 1000, 1000000);
        }

        public static int CalculateDiplomatPoints(Settlement originTown)
        {
            int pointsNeeded = Rand.Range(100, 200);
            return Mathf.Clamp(pointsNeeded, 100, 1000000);
        }

        public static int CalculateScoutMissionPoints(RimWarData rwd, int targetPoints)
        {
            float pointsNeeded = 0;
            pointsNeeded = Rand.Range(.9f, 1.35f) * targetPoints;
            if (rwd.behavior == RimWarBehavior.Expansionist)
            {
                pointsNeeded *= 1.15f;
            }
            else if(rwd.behavior == RimWarBehavior.Aggressive)
            {
                pointsNeeded *= 1.15f;
            }
            return Mathf.Clamp(Mathf.RoundToInt(pointsNeeded), 50, 1000000);
        }

        public static float GetFactionTechLevelMultiplier(Faction faction)
        {
            if(faction != null)
            {
                if(faction.def.techLevel != null && faction.def.techLevel != TechLevel.Animal && faction.def.techLevel != TechLevel.Undefined)
                {
                    if(faction.def.techLevel == TechLevel.Neolithic)
                    {
                        return .8f;
                    }
                    else if(faction.def.techLevel == TechLevel.Medieval)
                    {
                        return .9f;
                    }
                    else if(faction.def.techLevel == TechLevel.Industrial)
                    {
                        return 1f;
                    }
                    else if(faction.def.techLevel == TechLevel.Spacer)
                    {
                        return 1.1f;
                    }
                    else if(faction.def.techLevel == TechLevel.Ultra)
                    {
                        return 1.2f;
                    }
                    else if(faction.def.techLevel == TechLevel.Archotech)
                    {
                        return 1.25f;
                    }
                    else
                    {
                        return 1f;
                    }
                }
                else
                {
                    return 1f;
                }
            }
            else
            {
                return 0f;
            }
        }

        public static float GetDifficultyMultiplierFromStoryteller()
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            if(settingsRef.storytellerBasedDifficulty)
            {
                return Mathf.Clamp(Find.Storyteller.difficulty.threatScale, .25f, 1.5f);                
            }
            return settingsRef.rimwarDifficulty;
        }

        public static void CalculateFactionBehaviorWeights(RimWarData rimwarObject)
        {
            float settlerChance = 0;
            float warbandChance = 1;
            float scoutChance = 0;
            float warbandLaunchChance = 0;
            float diplomatChance = 0;
            float caravanChance = 0;
            float totalChance = 1f;

            Options.SettingsRef settingsRef = new Options.SettingsRef();

            if (rimwarObject.behavior == RimWarBehavior.Random)
            {
                if (rimwarObject.createsSettlements)
                {
                    settlerChance = 1f;
                }
                warbandChance = 3f;
                scoutChance = 4f;
                if (rimwarObject.CanLaunch)
                {
                    warbandLaunchChance = 2f;
                }
                if(settingsRef.createDiplomats)
                {
                    diplomatChance = 1f;
                }
                else
                {
                    diplomatChance = 0f;
                }
                caravanChance = 3f;
            }
            if (rimwarObject.behavior == RimWarBehavior.Aggressive)
            {
                if (rimwarObject.createsSettlements)
                {
                    settlerChance = 2f;
                }
                warbandChance = 6f;
                scoutChance = 4f;
                if (rimwarObject.CanLaunch)
                {
                    warbandLaunchChance = 4f;
                }
                if (!(rimwarObject.behavior == RimWarBehavior.Warmonger))
                {
                    if (settingsRef.createDiplomats)
                    {
                        diplomatChance = 1f;
                    }
                    else
                    {
                        diplomatChance = 0f;
                    }
                }
                caravanChance = 3f;
            }
            if (rimwarObject.behavior == RimWarBehavior.Cautious)
            {
                if (rimwarObject.createsSettlements)
                {
                    settlerChance = 2f;
                }
                warbandChance = 2f;
                scoutChance = 4f;
                if (rimwarObject.CanLaunch)
                {
                    warbandLaunchChance = 3f;
                }
                if (!(rimwarObject.behavior == RimWarBehavior.Warmonger))
                {
                    if (settingsRef.createDiplomats)
                    {
                        diplomatChance = 2f;
                    }
                    else
                    {
                        diplomatChance = 0f;
                    }
                }
                caravanChance = 5f;
            }
            if (rimwarObject.behavior == RimWarBehavior.Expansionist)
            {
                if (rimwarObject.createsSettlements)
                {
                    settlerChance = 3f;
                }
                warbandChance = 2f;
                scoutChance = 3f;
                if (rimwarObject.CanLaunch)
                {
                    warbandLaunchChance = 1f;
                }
                if (!(rimwarObject.behavior == RimWarBehavior.Warmonger))
                {
                    if (settingsRef.createDiplomats)
                    {
                        diplomatChance = 2f;
                    }
                    else
                    {
                        diplomatChance = 0f;
                    }
                }
                caravanChance = 4f;
            }
            if (rimwarObject.behavior == RimWarBehavior.Merchant)
            {
                if (rimwarObject.createsSettlements)
                {
                    settlerChance = 2f;
                }
                warbandChance = 2f;
                scoutChance = 3f;
                if (rimwarObject.CanLaunch)
                {
                    warbandLaunchChance = 1f;
                }
                if (!(rimwarObject.behavior == RimWarBehavior.Warmonger))
                {
                    if (settingsRef.createDiplomats)
                    {
                        diplomatChance = 2f;
                    }
                    else
                    {
                        diplomatChance = 0f;
                    }
                }
                caravanChance = 6f;
            }
            if (rimwarObject.behavior == RimWarBehavior.Warmonger)
            {
                if (rimwarObject.createsSettlements)
                {
                    settlerChance = 3f;
                }
                warbandChance = 8f;
                scoutChance = 4f;
                if (rimwarObject.CanLaunch)
                {
                    warbandLaunchChance = 5f;
                }
                if (!(rimwarObject.behavior == RimWarBehavior.Warmonger))
                {
                    if (settingsRef.createDiplomats)
                    {
                        diplomatChance = 2f;
                    }
                    else
                    {
                        diplomatChance = 0f;
                    }
                }
                caravanChance = 3f;
            }
            totalChance = settlerChance + warbandChance + scoutChance + warbandLaunchChance + diplomatChance + caravanChance;
            rimwarObject.settlerChance = settlerChance / totalChance;
            rimwarObject.warbandChance = (settlerChance + warbandChance) / totalChance;
            rimwarObject.scoutChance = (settlerChance + warbandChance + scoutChance) / totalChance;
            rimwarObject.warbandLaunchChance = (settlerChance + warbandChance + scoutChance + warbandLaunchChance) / totalChance;
            rimwarObject.diplomatChance = (settlerChance + warbandChance + scoutChance + warbandLaunchChance + diplomatChance) / totalChance;
            rimwarObject.caravanChance = (settlerChance + warbandChance + scoutChance + warbandLaunchChance + diplomatChance + caravanChance) / totalChance;
        }

        public static float GetBiomeMultiplier(BiomeDef biome)
        {
            float plantDensity = biome.plantDensity * 2f;
            float animalDensity = biome.animalDensity;
            float movementDifficulty = biome.movementDifficulty;
            if(movementDifficulty < .1f)
            {
                movementDifficulty = .1f;
            }
            float forageability = 2 * biome.forageability;
            float crops = forageability / movementDifficulty;
            float disease = biome.diseaseMtbDays / 100f;

            float mult = (plantDensity + animalDensity + crops) * disease;
            //Temperate Forest = 3.5
            //Boreal Forest = 3.012
            //Temperate Swamp = 2.51
            //Tropical Rainforest = 2.87
            //Tropical Swamp = 2.66
            //Cold Bog = 2.14
            //Arid Shrubland = 2.132
            //Tundra = 1.984
            //Desert = 0.9
            //Ice Sheet = 0.18
            //Extreme Desert  = 0.1
            
            return mult;
        }

        public static List<RimWar.Planet.Settlement> GetRimWarSettlements(List<RimWarData> rwdList)
        {
            List<RimWar.Planet.Settlement> tmpSettlements = new List<RimWar.Planet.Settlement>();
            tmpSettlements.Clear();
            for (int j = 0; j < rwdList.Count; j++)
            {
                RimWarData rwd = rwdList[j];
                for (int i = 0; i < rwd.FactionSettlements.Count; i++)
                {
                    tmpSettlements.Add(rwd.FactionSettlements[i]);
                }
            }
            return tmpSettlements;
        }

        public static void UpdateRWDSettlementLists(RimWarData rwd)
        {
            List<RimWar.Planet.Settlement> tmpSettlements = new List<RimWar.Planet.Settlement>();
            tmpSettlements.Clear();
            List<RimWar.Planet.Settlement> settlementList = GetRimWarSettlements(GetRimWarData());
            rwd.HostileSettlements.Clear();
            rwd.NonHostileSettlements.Clear();
            for (int i = 0; i < settlementList.Count; i++)
            {                
                RimWar.Planet.Settlement st = settlementList[i];
                if(st.Faction != null)
                {
                    if(st.Faction.HostileTo(rwd.RimWarFaction))
                    {
                        rwd.HostileSettlements.Add(st);
                    }
                    else
                    {
                        rwd.NonHostileSettlements.Add(st);
                    }
                }
            }
        }

        public static List<RimWorld.Planet.Settlement> GetRimWorldSettlementsInRange(int from, int range)
        {
            List<RimWorld.Planet.Settlement> tmpSettlements = new List<RimWorld.Planet.Settlement>();
            tmpSettlements.Clear();
            List<WorldObject> worldObjects = GetWorldObjectsInRange(from, range);
            for(int i =0; i < worldObjects.Count; i++)
            {
                if(worldObjects[i] is RimWorld.Planet.Settlement)
                {
                    tmpSettlements.Add(worldObjects[i] as RimWorld.Planet.Settlement);
                }
            }
            return tmpSettlements;
        }

        public static List<Settlement> GetRimWarSettlementsInRange(int from, int range, List<RimWarData> warObjects, RimWarData rwd)
        {
            List<Settlement> tmpSettlements = new List<Settlement>();
            tmpSettlements.Clear();
            if (warObjects != null && warObjects.Count > 0)
            {
                List<RimWarData> rwdInst = warObjects.InRandomOrder().ToList();
                for (int i = 0; i < warObjects.Count; i++)
                {                    
                    if (warObjects[i] != rwd && warObjects[i].FactionSettlements != null && warObjects[i].FactionSettlements.Count > 0)
                    {
                        List<Settlement> fs = warObjects[i].FactionSettlements.InRandomOrder().ToList();
                        for(int j = 0; j < warObjects[i].FactionSettlements.Count; j++)
                        {
                            Settlement settlement = warObjects[i].FactionSettlements[j];
                            int to = settlement.Tile;
                            //int ticksToArrive = Utility.ArrivalTimeEstimator.EstimatedTicksToArrive(from, to, 1);
                            //int tileDistance = Find.WorldGrid.TraversalDistanceBetween(from, to, false, range);
                            int tileDistance = (int)Find.WorldGrid.ApproxDistanceInTiles(from, to);
                            if (tileDistance != 0 && tileDistance <= range)
                            {
                                tileDistance = Find.WorldGrid.TraversalDistanceBetween(from, to, false, range);
                                if (tileDistance <= range)
                                {
                                    tmpSettlements.Add(settlement);
                                    if(tmpSettlements.Count >= 3)
                                    {
                                        return tmpSettlements;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Get_WCPT().settlementSearches++;
            return tmpSettlements;
        }

        public static Settlement GetClosestRimWarSettlementOfFaction(Faction faction, int tile, int maxRange)
        {
            List<Settlement> settlementsInRange = GetRimWarSettlementsInRange(tile, maxRange, GetRimWarData(), GetRimWarDataForFaction(faction));
            Settlement closestSettlement = null;
            float closestDist = 0f;
            if(settlementsInRange != null && settlementsInRange.Count > 0)
            {
                for(int i = 0; i < settlementsInRange.Count; i++)
                {
                    if(closestSettlement != null)
                    {
                        //if(Find.WorldGrid.TraversalDistanceBetween(tile, settlementsInRange[i].Tile, false, maxRange) < Find.WorldGrid.TraversalDistanceBetween(tile, closestSettlement.Tile, false, maxRange))
                        //{
                        //    closestSettlement = settlementsInRange[i];
                        //}

                        float otherDist = Find.WorldGrid.ApproxDistanceInTiles(tile, settlementsInRange[i].Tile);
                        if(otherDist < closestDist)
                        {
                            closestDist = otherDist;
                            closestSettlement = settlementsInRange[i];
                        }
                        
                    }
                    else
                    {                        
                        closestSettlement = settlementsInRange[i];
                        closestDist = Find.WorldGrid.ApproxDistanceInTiles(tile, closestSettlement.Tile);
                    }
                }
            }
            return closestSettlement;
        }

        public static Settlement GetClosestRimWarSettlementInRWDTo(RimWarData rwd, int tile, int maxEvalRange = 100)
        {
            WorldUtility.Get_WCPT().UpdateFactionSettlements(rwd);
            if (rwd != null && rwd.FactionSettlements != null && rwd.FactionSettlements.Count > 0)
            {
                List<Settlement> settlements = rwd.FactionSettlements;
                Settlement closestSettlement = null;
                int distance = 0;
                for (int i = 0; i < settlements.Count; i++)
                {
                    if (closestSettlement == null)
                    {
                        closestSettlement = settlements[i];
                        distance = Find.WorldGrid.TraversalDistanceBetween(tile, closestSettlement.Tile, false, 500);
                    }
                    else
                    {
                        int dist = Find.WorldGrid.TraversalDistanceBetween(tile, settlements[i].Tile, false, maxEvalRange);
                        if (dist < distance)
                        {
                            closestSettlement = settlements[i];
                            distance = dist;
                        }
                    }
                }                
                return closestSettlement;
            }
            return null;
        }

        public static List<Settlement> GetHostileRimWarSettlementsInRange(int from, int range, Faction faction, List<RimWarData> rwdList, RimWarData rwd)
        {
            List<Settlement> settlementsInRange = GetRimWarSettlementsInRange(from, range, rwdList, rwd);
            List<Settlement> tmpSettlements = new List<Settlement>();
            tmpSettlements.Clear();
            for(int i = 0; i < settlementsInRange.Count; i++)
            {
                if(settlementsInRange[i].Faction.HostileTo(faction))
                {
                    tmpSettlements.Add(settlementsInRange[i]);
                }
            }
            return tmpSettlements;
        }

        public static List<Settlement> GetNonHostileRimWarSettlementsInRange(int from, int range, Faction faction, List<RimWarData> rwdList, RimWarData rwd)
        {
            List<Settlement> settlementsInRange = GetRimWarSettlementsInRange(from, range, rwdList, rwd);
            List<Settlement> tmpSettlements = settlementsInRange.Except(GetHostileRimWarSettlementsInRange(from, range, faction, rwdList, rwd)).ToList();
            return tmpSettlements;
        }

        public static List<Settlement> GetFriendlyRimWarSettlementsInRange(int from, int range, Faction thisFaction, List<RimWarData> rwdList, RimWarData rwd)
        {
            List<Settlement> settlementsInRange = GetRimWarSettlementsInRange(from, range, rwdList, rwd);
            List<Settlement> tmpSettlements = new List<Settlement>();
            tmpSettlements.Clear();
            if (settlementsInRange != null && settlementsInRange.Count > 0)
            {
                for (int i = 0; i < settlementsInRange.Count; i++)
                {
                    if (settlementsInRange[i].Faction == thisFaction)
                    {
                        tmpSettlements.Add(settlementsInRange[i]);
                    }
                }
            }            
            return tmpSettlements;
        }

        public static Settlement GetRimWarSettlementAtTile(int tile)
        {
            List<RimWarData> rwd = GetRimWarData();
            if(rwd != null && rwd.Count > 0)
            {
                for(int i =0; i < rwd.Count; i++)
                {
                    if(rwd[i].FactionSettlements != null && rwd[i].FactionSettlements.Count > 0)
                    {
                        List<Settlement> rwdSettlements = rwd[i].FactionSettlements;
                        for(int j =0; j< rwdSettlements.Count; j++)
                        {
                            Settlement settlement = rwdSettlements[j];
                            if(settlement.Tile == tile)
                            {
                                return settlement;
                            }
                        }
                    }
                }
            }
            return null;
        }

        public static List<Settlement> GetHostileSettlementsToRWD(RimWarData rwd)
        {
            List<Settlement> allSettlements = Get_WCPT().AllRimWarSettlements;
            List<Settlement> tmpList = new List<Settlement>();
            tmpList.Clear();
            for(int i = 0; i < allSettlements.Count; i++)
            {
                if(allSettlements[i].Faction.HostileTo(rwd.RimWarFaction))
                {
                    tmpList.Add(allSettlements[i]);
                }
            }
            return tmpList;
        }

        public static List<WarObject> GetRimWarObjectsAt(int tile)
        {
            List<WarObject> warObjects = new List<WarObject>();
            warObjects.Clear();
            List<WorldObject> worldObjects = Find.World.worldObjects.AllWorldObjects.ToList();
            if(worldObjects != null && worldObjects.Count > 0)
            {
                for(int i = 0; i < worldObjects.Count; i++)
                {
                    WorldObject wo = worldObjects[i];
                    if(wo.Tile == tile && wo is WarObject)
                    {
                        warObjects.Add(wo as WarObject);
                    }
                }
            }
            return warObjects;
        }

        public static List<WorldObject> GetWorldObjectsInRange(int from, int range)
        {
            List<WorldObject> tmpObjects = new List<WorldObject>();
            tmpObjects.Clear();
            List<WorldObject> worldObjects = Find.World.worldObjects.AllWorldObjects.ToList();
            for (int i = 0; i < worldObjects.Count; i++)
            {
                int to = worldObjects[i].Tile;     
                if(from == to)
                {
                    tmpObjects.Add(worldObjects[i]);
                    continue;
                }
                //int distance = Find.WorldGrid.TraversalDistanceBetween(from, to, false, range);
                int distance = (int)Find.WorldGrid.ApproxDistanceInTiles(from, to);
                //Log.Message("getting tile in range is an approx distance of " + Find.WorldGrid.ApproxDistanceInTiles(from, to) + " travel distance is " + distance + " and has a range cap of " + range);
                if (distance <= range)
                {
                    distance = Find.WorldGrid.TraversalDistanceBetween(from, to, false, range);
                    if (distance <= range)
                    {
                        tmpObjects.Add(worldObjects[i]);
                    }
                }
            }
            return tmpObjects;
        }

        public static List<WorldObject> GetAllWorldObjectsAt(int tile)
        {
            List<WorldObject> tmpObjects = new List<WorldObject>();
            tmpObjects.Clear();
            List<WorldObject> worldObjects = Find.WorldObjects.AllWorldObjects.ToList();
            for (int i = 0; i < worldObjects.Count; i++)
            {                 
                if (tile == worldObjects[i].Tile)
                {
                    tmpObjects.Add(worldObjects[i]);
                    continue;
                }
            }
            return tmpObjects;
        }

        public static List<WorldObject> GetAllWorldObjectsAtExcept(int tile, WorldObject woThis)
        {
            List<WorldObject> tmpObjects = new List<WorldObject>();
            tmpObjects.Clear();
            List<WorldObject> worldObjects = Find.WorldObjects.AllWorldObjects.ToList();
            for (int i = 0; i < worldObjects.Count; i++)
            {
                if (tile == worldObjects[i].Tile && woThis != worldObjects[i])
                {
                    tmpObjects.Add(worldObjects[i]);
                    continue;
                }
            }
            return tmpObjects;
        }

        public static List<Warband> GetHostileWarbandsInRange(int from, int range, Faction faction)
        {
            List<Warband> tmpWarbands = new List<Warband>();
            tmpWarbands.Clear();
            List<WorldObject> tmpObjects = GetWorldObjectsInRange(from, range);
            if(tmpObjects != null && tmpObjects.Count > 0)
            {
                for(int i =0; i < tmpObjects.Count; i++)
                {
                    if(tmpObjects[i] is Warband && tmpObjects[i].Faction.HostileTo(faction))
                    {
                        tmpWarbands.Add(tmpObjects[i] as Warband);
                    }
                }
            }
            return tmpWarbands;
        }

        public static List<WarObject> GetHostileWarObjectsInRange(int from, int range, Faction faction)
        {
            List<WarObject> tmpWarObjects = new List<WarObject>();
            tmpWarObjects.Clear();
            List<WorldObject> tmpObjects = GetWorldObjectsInRange(from, range);
            if (tmpObjects != null && tmpObjects.Count > 0)
            {
                for (int i = 0; i < tmpObjects.Count; i++)
                {
                    if (tmpObjects[i] is WarObject && tmpObjects[i].Faction.HostileTo(faction))
                    {
                        tmpWarObjects.Add(tmpObjects[i] as WarObject);
                    }
                }
            }
            return tmpWarObjects;
        }

        public static List<WarObject> GetWarObjectsInFaction(Faction faction)
        {
            List<WarObject> tmpWarObjects = new List<WarObject>();
            tmpWarObjects.Clear();
            List<WorldObject> tmpObjects = Find.WorldObjects.AllWorldObjects;
            if (tmpObjects != null && tmpObjects.Count > 0)
            {
                for (int i = 0; i < tmpObjects.Count; i++)
                {
                    if (tmpObjects[i] is WarObject && tmpObjects[i].Faction == faction)
                    {
                        tmpWarObjects.Add(tmpObjects[i] as WarObject);
                    }
                }
            }
            return tmpWarObjects;
        }

        private static int factionCount = 0;
        public static void ValidateFactions(bool forced = false)
        {            
            List<Faction> factions = Find.FactionManager.AllFactionsVisible.ToList();
            if (factionCount != factions.Count || forced)
            {
                factionCount = factions.Count;
                for (int i = 0; i < factions.Count; i++)
                {
                    List<FactionRelation> fr = Traverse.Create(root: factions[i]).Field(name: "relations").GetValue<List<FactionRelation>>();
                    if (fr == null || fr.Count <= 0)
                    {
                        fr = new List<FactionRelation>();
                        fr.Clear();
                        for (int j = 0; j < factions.Count; j++)
                        {
                            CreateFactionRelation(factions[i], factions[j]);
                        }
                    }
                    WorldUtility.Get_WCPT().AddRimWarFaction(factions[i]);                    
                }

                List<RimWarData> rwdList = WorldUtility.GetRimWarData();
                bool hasRelation = false;
                for (int k = 0; k < rwdList.Count; k++)
                {
                    for (int i = 0; i < factions.Count; i++)
                    {
                        List<FactionRelation> fr = Traverse.Create(root: factions[i]).Field(name: "relations").GetValue<List<FactionRelation>>();
                        for (int j = 0; j < fr.Count; j++)
                        {
                            if (fr[j].other == rwdList[k].RimWarFaction)
                            {
                                hasRelation = true;
                                break;
                            }
                        }
                        if (!hasRelation)
                        {
                            CreateFactionRelation(factions[i], rwdList[k].RimWarFaction);
                        }
                    }
                }
            }
        }

        public static void CreateFactionRelation(Faction thisFaction, Faction otherFaction)
        {
            List<FactionRelation> fr = Traverse.Create(root: thisFaction).Field(name: "relations").GetValue<List<FactionRelation>>();
            if (fr == null || fr.Count <= 0)
            {
                fr = new List<FactionRelation>();
                fr.Clear();
            }
            if (thisFaction != otherFaction)
            {
                FactionRelation _fr = new FactionRelation();
                _fr.other = otherFaction;
                _fr.goodwill = Rand.Range(-100, 100);
                _fr.kind = FactionRelationKind.Neutral;
                bool sentLetter = false;
                _fr.CheckKindThresholds(thisFaction, false, "", GlobalTargetInfo.Invalid, out sentLetter);
                fr.Add(_fr);
                Traverse.Create(root: thisFaction).Field(name: "relations").SetValue(fr);
            }
        }
    }
}
