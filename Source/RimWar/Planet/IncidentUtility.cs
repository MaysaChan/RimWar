﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using Verse.AI.Group;
using RimWar.History;

namespace RimWar.Planet
{
    public class IncidentUtility
    {
        public IncidentParms parms = new IncidentParms();

        public static void ResolveWorldEngagement(WarObject warObject, WorldObject wo)
        {
            if (warObject != null && wo != null)
            {
                if (wo.Faction != null && wo.Faction.HostileTo(warObject.Faction))
                {
                    if (wo is Caravan)
                    {
                        DoCaravanAttackWithPoints(warObject, wo as Caravan, warObject.rimwarData, PawnsArrivalModeOrRandom(PawnsArrivalModeDefOf.EdgeWalkIn));
                    }
                    else if (wo is WarObject)
                    {
                        ResolveRimWarBattle(warObject, wo as WarObject);
                    }
                }
                else
                {
                    if(wo is Caravan)
                    {
                        if(warObject is Trader)
                        {
                            Trader trader = warObject as Trader;
                            if(!trader.tradedWithTrader) //!trader.TradedWith.Contains(wo))
                            {
                                //attempt to trade with player
                                DoCaravanTradeWithPoints(warObject, wo as Caravan, warObject.rimwarData, PawnsArrivalModeOrRandom(PawnsArrivalModeDefOf.EdgeWalkIn));
                            }
                        }
                        else if(warObject is Diplomat)
                        {
                            DoPeaceTalks_Caravan(warObject, wo as Caravan, warObject.rimwarData, PawnsArrivalModeOrRandom(PawnsArrivalModeDefOf.EdgeWalkIn));
                        }
                        else
                        {
                            //do nothing
                        }
                    }
                }
            }
        }

        public static void ResolveRimWarBattle(WarObject attacker, WarObject defender)
        {            
            float combinedPoints = attacker.RimWarPoints + defender.RimWarPoints;
            float attackerRoll = Rand.Value;
            float defenderRoll = Rand.Value;
            float attackerResult = attackerRoll * attacker.RimWarPoints;
            float defenderResult = defenderRoll * defender.RimWarPoints;
            float endPointsAttacker = 0f;
            float endPointsDefender = 0f;
            RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_NeutralEvent);
            let.label = "RW_LetterBattle".Translate();            
            if (attackerResult > defenderResult)
            {
                //Log.Message("attacker " + attacker.Label + " wins agaisnt warband " + defender.Label);
                endPointsAttacker = (attacker.RimWarPoints * (1 - ((Rand.Range(.2f, .3f) * defender.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
                endPointsDefender = (defender.RimWarPoints * (1 - ((Rand.Range(.2f, .3f) * attacker.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
                //Attacker wins
                if (attackerResult > 2 * defenderResult) //routed
                {
                    endPointsAttacker += endPointsDefender * (Rand.Range(.35f, .5f)); //gain up to half the points of the defender warband in combat power
                }
                else if (attackerResult > 1.5f * defenderResult) //solid win
                {
                    endPointsAttacker += endPointsDefender * (Rand.Range(.2f, .3f));
                    if (defender.ParentSettlement != null)
                    {
                        ConsolidatePoints reconstitute = new ConsolidatePoints(Mathf.RoundToInt(Mathf.Min(Rand.Range(.3f, .5f) * endPointsDefender, defender.RimWarPoints)), Mathf.RoundToInt(Find.WorldGrid.TraversalDistanceBetween(defender.Tile, defender.ParentSettlement.Tile) * defender.TicksPerMove) + Find.TickManager.TicksGame);
                        defender.ParentSettlement.SettlementPointGains.Add(reconstitute);
                    }
                }
                else
                {
                    endPointsAttacker += endPointsDefender * Rand.Range(.1f, .2f);
                    if (defender.ParentSettlement != null)
                    {
                        ConsolidatePoints reconstitute = new ConsolidatePoints(Mathf.RoundToInt(Mathf.Min(Rand.Range(.45f, .6f) * endPointsDefender, defender.RimWarPoints)), Mathf.RoundToInt(Find.WorldGrid.TraversalDistanceBetween(defender.Tile, defender.ParentSettlement.Tile) * defender.TicksPerMove) + Find.TickManager.TicksGame);
                        defender.ParentSettlement.SettlementPointGains.Add(reconstitute);
                    }
                }
                let.text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "defeated", defender.Label, defender.RimWarPoints);
                WorldUtility.CreateWarObjectOfType(attacker, Mathf.RoundToInt(Mathf.Clamp(endPointsAttacker, 50, 2*attacker.RimWarPoints)), WorldUtility.GetRimWarDataForFaction(attacker.Faction), attacker.ParentSettlement, attacker.Tile, attacker.ParentSettlement.Tile, WorldObjectDefOf.Settlement);
                let.lookTargets = attacker;
                let.relatedFaction = attacker.Faction;
            }
            else
            {
                //Log.Message("defender " + defender.Label + " wins against warband " + defender.Label);
                //Defender wins
                endPointsAttacker = (attacker.RimWarPoints * (1 - ((Rand.Range(.2f, .3f) * defender.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
                endPointsDefender = (defender.RimWarPoints * (1 - ((Rand.Range(.2f, .3f) * attacker.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
                if (defenderResult > 2 * attackerResult) //routed
                {
                    endPointsDefender += endPointsAttacker * (Rand.Range(.35f, .5f)); //gain up to half the points of the defender warband in combat power
                }
                else if (attackerResult > 1.5f * defenderResult) //solid win
                {
                    endPointsDefender += endPointsAttacker * (Rand.Range(.2f, .3f));
                    if (attacker.ParentSettlement != null)
                    {
                        ConsolidatePoints reconstitute = new ConsolidatePoints(Mathf.RoundToInt(Mathf.Min(Rand.Range(.3f, .5f) * endPointsAttacker, attacker.RimWarPoints)), Mathf.RoundToInt(Find.WorldGrid.TraversalDistanceBetween(attacker.Tile, attacker.ParentSettlement.Tile) * attacker.TicksPerMove) + Find.TickManager.TicksGame);
                        attacker.ParentSettlement.SettlementPointGains.Add(reconstitute);
                    }
                }
                else
                {
                    endPointsDefender += endPointsAttacker * Rand.Range(.1f, .2f);
                    if (defender.ParentSettlement != null)
                    {
                        ConsolidatePoints reconstitute = new ConsolidatePoints(Mathf.RoundToInt(Mathf.Min(Rand.Range(.45f, .6f) * endPointsAttacker, attacker.RimWarPoints)), Mathf.RoundToInt(Find.WorldGrid.TraversalDistanceBetween(attacker.Tile, attacker.ParentSettlement.Tile) * attacker.TicksPerMove) + Find.TickManager.TicksGame);
                        attacker.ParentSettlement.SettlementPointGains.Add(reconstitute);
                    }
                }
                let.text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "was defeated by", defender.Label, defender.RimWarPoints);
                WorldUtility.CreateWarObjectOfType(defender, Mathf.RoundToInt(Mathf.Clamp(endPointsDefender, 50, 2*defender.RimWarPoints)), WorldUtility.GetRimWarDataForFaction(defender.Faction), defender.ParentSettlement, defender.Tile, defender.ParentSettlement.Tile, WorldObjectDefOf.Settlement);
                let.lookTargets = defender;
                let.relatedFaction = defender.Faction;
            }
            RW_LetterMaker.Archive_RWLetter(let);
            defender.Faction.TryAffectGoodwillWith(attacker.Faction, -2, false, false, null, null);
            attacker.Faction.TryAffectGoodwillWith(defender.Faction, -2, false, false, null, null);
            defender.ImmediateAction(null); //force removal of the non-initiating warband
        }

        public static void ResolveWarObjectAttackOnSettlement(WarObject attacker, Settlement parentSettlement, Settlement defender, RimWarData rwd)
        {
            float combinedPoints = attacker.RimWarPoints + defender.RimWarPoints;
            float attackerRoll = Rand.Value;
            float defenderRoll = Rand.Value;
            float attackerResult = attackerRoll * attacker.RimWarPoints;
            float defenderResult = defenderRoll * defender.RimWarPoints;
            float endPointsAttacker = 0f;
            float endPointsDefender = 0f;

            RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_NeutralEvent);
            let.label = "RW_LetterSettlementBattle".Translate();

            //determine attacker/defender win
            //if attacker wins ->
            // determine points assigned to attacker (routed or solid can capture, routed can raze (city loses additional pts), solid or win can weaken)
            // determine if the city is destroyed (less than x points always or chance when routed)
            // determine if the city is captured (no additional points to attacker but settlement faction change) or remains (razed (loses more points) or weakened)
            //if defender wins ->
            // determine points assigned to defender (routed can capture the warband (lots of points, nothing returned to attacker)
            // determine points to assign to attacker and defender and send attack back to parent settlement

            if (attackerResult > defenderResult)
            {
                //Log.Message("attacker " + attacker.Label + " wins against settlement " + defender.RimWorld_Settlement.Name);
                endPointsAttacker = (attacker.RimWarPoints * (1 - ((Rand.Range(.2f, .3f) * defender.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
                endPointsDefender = (defender.RimWarPoints * (1 - ((Rand.Range(.2f, .3f) * attacker.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
                //Attacker wins
                if (attackerResult > 1.75 * defenderResult) //routed
                {
                    float rndCapture = Rand.Value;
                    if(attacker.rimwarData.behavior == RimWarBehavior.Expansionist)
                    {
                        rndCapture *= 1.1f;
                    }
                    else if(attacker.rimwarData.behavior == RimWarBehavior.Warmonger)
                    {
                        rndCapture *= 1.5f;
                    }
                    
                    if(rndCapture >= .35f)
                    {
                        //Log.Message("attacker is capturing " + defender.RimWorld_Settlement.Name);
                        let.text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "captured", defender.RimWorld_Settlement.Name, defender.RimWarPoints);
                        let.lookTargets = attacker;
                        let.relatedFaction = attacker.Faction;
                        WorldUtility.ConvertSettlement(Find.World.worldObjects.SettlementAt(defender.Tile), defender, WorldUtility.GetRimWarDataForFaction(defender.Faction), WorldUtility.GetRimWarDataForFaction(attacker.Faction), Mathf.RoundToInt(Mathf.Max(endPointsDefender, 0)));
                        //RimWorld.Planet.Settlement rws = Find.World.worldObjects.SettlementAt(defender.Tile);
                        //rws.SetFaction(attacker.Faction);
                        //WorldUtility.GetRimWarDataForFaction(defender.Faction).FactionSettlements.Remove(defender);
                        //defender.Faction = attacker.Faction;
                        //WorldUtility.GetRimWarDataForFaction(attacker.Faction).FactionSettlements.Add(defender);
                        //Find.World.WorldUpdate();                        
                    }
                    else
                    {
                        float pointsAdjustment = endPointsDefender * (Rand.Range(.35f, .5f));
                        endPointsAttacker += pointsAdjustment; 
                        endPointsDefender -= pointsAdjustment;
                        let.text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "defeated", defender.RimWorld_Settlement.Name, defender.RimWarPoints);
                        let.lookTargets = attacker;
                        let.relatedFaction = defender.Faction;
                        if (endPointsDefender <= 1000)
                        {
                            let.text += "\nThe pathetic hamlet was burned to the ground.";
                            //Find.WorldObjects.Remove(Find.World.worldObjects.SettlementAt(defender.Tile));
                            Find.World.worldObjects.SettlementAt(defender.Tile).Destroy();
                            WorldUtility.GetRimWarDataForFaction(defender.Faction).FactionSettlements.Remove(defender);
                            if(WorldUtility.GetRimWarDataForFaction(defender.Faction).FactionSettlements.Count <=0)
                            {
                                WorldUtility.RemoveRWDFaction(WorldUtility.GetRimWarDataForFaction(defender.Faction));
                            }
                        }
                        else
                        {
                            float rndRaze = Rand.Value;
                            if (attacker.rimwarData.behavior == RimWarBehavior.Expansionist)
                            {
                                rndRaze *= .8f;
                            }
                            else if (attacker.rimwarData.behavior == RimWarBehavior.Warmonger)
                            {
                                rndRaze *= 1.2f;
                            }

                            if (rndRaze >= .9f)
                            {
                                let.text += "\nThe city was brutally destroyed.";
                                endPointsAttacker += (Rand.Range(.4f, .7f) * endPointsDefender);
                                Find.World.worldObjects.SettlementAt(defender.Tile).Destroy();
                                //Find.WorldObjects.Remove(Find.World.worldObjects.SettlementAt(defender.Tile));
                                WorldUtility.GetRimWarDataForFaction(defender.Faction).FactionSettlements.Remove(defender);
                                if (WorldUtility.GetRimWarDataForFaction(defender.Faction).FactionSettlements.Count <= 0)
                                {
                                    WorldUtility.RemoveRWDFaction(WorldUtility.GetRimWarDataForFaction(defender.Faction));
                                }
                            }
                            else
                            {
                                defender.RimWarPoints = Mathf.RoundToInt(Mathf.Clamp(endPointsDefender, 100, defender.RimWarPoints));
                            }
                        }
                    }                                     
                }
                else if (attackerResult > 1.35f * defenderResult) //solid win
                {
                    float rndCapture = Rand.Value;
                    if (attacker.rimwarData.behavior == RimWarBehavior.Expansionist)
                    {
                        rndCapture *= 1.1f;
                    }
                    else if (attacker.rimwarData.behavior == RimWarBehavior.Warmonger)
                    {
                        rndCapture *= 1.5f;
                    }

                    if (rndCapture >= .6f)
                    {
                        //Log.Message("attacker is capturing " + defender.RimWorld_Settlement.Name);
                        let.text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "captured", defender.RimWorld_Settlement.Name, defender.RimWarPoints);
                        let.lookTargets = attacker;
                        let.relatedFaction = attacker.Faction;
                        WorldUtility.ConvertSettlement(Find.World.worldObjects.SettlementAt(defender.Tile), defender, WorldUtility.GetRimWarDataForFaction(defender.Faction), WorldUtility.GetRimWarDataForFaction(attacker.Faction), Mathf.RoundToInt(Mathf.Max(endPointsDefender, 0)));

                        //Find.World.worldObjects.SettlementAt(defender.Tile).SetFaction(attacker.Faction);
                        //WorldUtility.GetRimWarDataForFaction(defender.Faction).FactionSettlements.Remove(defender);
                        //defender.Faction = attacker.Faction;
                        //WorldUtility.GetRimWarDataForFaction(attacker.Faction).FactionSettlements.Add(defender);
                        //Find.World.WorldUpdate();
                    }
                    else
                    {
                        float pointsAdjustment = endPointsDefender * (Rand.Range(.2f, .35f));
                        endPointsAttacker += pointsAdjustment;
                        endPointsDefender -= pointsAdjustment;
                        let.text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "defeated", defender.RimWorld_Settlement.Name, defender.RimWarPoints);
                        let.lookTargets = attacker;
                        let.relatedFaction = defender.Faction;
                        if (endPointsDefender <= 1000)
                        {
                            let.text += "\nThe pathetic hamlet was burned to the ground.";
                            Find.World.worldObjects.SettlementAt(defender.Tile).Destroy();
                            //Find.WorldObjects.Remove(Find.World.worldObjects.SettlementAt(defender.Tile));
                            WorldUtility.GetRimWarDataForFaction(defender.Faction).FactionSettlements.Remove(defender);
                            if (WorldUtility.GetRimWarDataForFaction(defender.Faction).FactionSettlements.Count <= 0)
                            {
                                WorldUtility.RemoveRWDFaction(WorldUtility.GetRimWarDataForFaction(defender.Faction));
                            }
                        }
                        else
                        {
                            defender.RimWarPoints = Mathf.RoundToInt(Mathf.Clamp(endPointsDefender, 100, defender.RimWarPoints));
                        }
                    }
                }
                else
                {
                    float rndCapture = Rand.Value;
                    if (attacker.rimwarData.behavior == RimWarBehavior.Expansionist)
                    {
                        rndCapture *= 1.1f;
                    }
                    else if (attacker.rimwarData.behavior == RimWarBehavior.Warmonger)
                    {
                        rndCapture *= 1.5f;
                    }

                    if (rndCapture >= .9f)
                    {
                        //Log.Message("attacker is capturing " + defender.RimWorld_Settlement.Name);
                        let.text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "captured", defender.RimWorld_Settlement.Name, defender.RimWarPoints);
                        let.lookTargets = attacker;
                        let.relatedFaction = attacker.Faction;
                        WorldUtility.ConvertSettlement(Find.World.worldObjects.SettlementAt(defender.Tile), defender, WorldUtility.GetRimWarDataForFaction(defender.Faction), WorldUtility.GetRimWarDataForFaction(attacker.Faction), Mathf.RoundToInt(Mathf.Max(endPointsDefender, 0)));

                        //Find.World.worldObjects.SettlementAt(defender.Tile).SetFaction(attacker.Faction);
                        //WorldUtility.GetRimWarDataForFaction(defender.Faction).FactionSettlements.Remove(defender);
                        //defender.Faction = attacker.Faction;
                        //WorldUtility.GetRimWarDataForFaction(attacker.Faction).FactionSettlements.Add(defender);
                        //Find.World.WorldUpdate();
                    }
                    else
                    {
                        float pointsAdjustment = endPointsDefender * (Rand.Range(.1f, .25f));
                        endPointsAttacker += pointsAdjustment;
                        endPointsDefender -= pointsAdjustment;
                        let.text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "defeated", defender.RimWorld_Settlement.Name, defender.RimWarPoints);
                        let.lookTargets = attacker;
                        let.relatedFaction = defender.Faction;
                        if (endPointsDefender <= 1000)
                        {
                            let.text += "\nThe pathetic hamlet was burned to the ground.";
                            Find.World.worldObjects.SettlementAt(defender.Tile).Destroy();
                            //Find.WorldObjects.Remove(Find.World.worldObjects.SettlementAt(defender.Tile));
                            WorldUtility.GetRimWarDataForFaction(defender.Faction).FactionSettlements.Remove(defender);
                            if (WorldUtility.GetRimWarDataForFaction(defender.Faction).FactionSettlements.Count <= 0)
                            {
                                WorldUtility.RemoveRWDFaction(WorldUtility.GetRimWarDataForFaction(defender.Faction));
                            }
                        }
                        else
                        {
                            defender.RimWarPoints = Mathf.RoundToInt(Mathf.Clamp(endPointsDefender, 100, defender.RimWarPoints));
                        }
                    }
                }                
                WorldUtility.CreateWarObjectOfType(attacker, Mathf.RoundToInt(Mathf.Clamp(endPointsAttacker, 50, 2*attacker.RimWarPoints)), WorldUtility.GetRimWarDataForFaction(attacker.Faction), attacker.ParentSettlement, attacker.Tile, attacker.ParentSettlement.Tile, WorldObjectDefOf.Settlement);
                
            }
            else
            {
                //Log.Message("attacker " + attacker.Label + " loses against settlement " + defender.RimWorld_Settlement.Name);
                //Defender wins
                endPointsAttacker = (attacker.RimWarPoints * (1 - ((Rand.Range(.2f, .3f) * defender.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
                endPointsDefender = (defender.RimWarPoints * (1 - ((Rand.Range(.2f, .3f) * attacker.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
                let.text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "failed in their assault against", defender.RimWorld_Settlement.Name, defender.RimWarPoints);
                let.lookTargets = defender.RimWorld_Settlement;
                let.relatedFaction = defender.Faction;
                if (defenderResult > 1.75 * attackerResult) //routed
                {
                    endPointsDefender += endPointsAttacker * (Rand.Range(.35f, .55f)); //gain up to half the points of the attacker warband in combat power and disperse the warband
                    if (attacker.ParentSettlement != null)
                    {
                        ConsolidatePoints reconstitute = new ConsolidatePoints(Mathf.RoundToInt(Mathf.Min(Rand.Range(.3f, .5f) * endPointsAttacker, attacker.RimWarPoints)), Mathf.RoundToInt(Find.WorldGrid.TraversalDistanceBetween(attacker.Tile, attacker.ParentSettlement.Tile) * attacker.TicksPerMove) + Find.TickManager.TicksGame);
                        attacker.ParentSettlement.SettlementPointGains.Add(reconstitute);
                    }
                }
                else if (defenderResult > 1.35f * attackerResult) //solid win; warband retreats back to parent settlement
                {
                    float pointsAdjustment = endPointsAttacker * (Rand.Range(.3f, .4f));
                    endPointsAttacker -= pointsAdjustment;
                    endPointsDefender += pointsAdjustment;
                    if (attacker.ParentSettlement != null)
                    {
                        WorldUtility.CreateWarObjectOfType(attacker, Mathf.RoundToInt(Mathf.Clamp(endPointsAttacker, 50, attacker.RimWarPoints)), WorldUtility.GetRimWarDataForFaction(attacker.Faction), attacker.ParentSettlement, attacker.Tile, attacker.ParentSettlement.Tile, WorldObjectDefOf.Settlement);
                    }
                }
                else
                {
                    float pointsAdjustment = endPointsAttacker * (Rand.Range(.15f, .25f));
                    endPointsAttacker -= pointsAdjustment;
                    endPointsDefender += pointsAdjustment;
                    if (attacker.ParentSettlement != null)
                    {
                        WorldUtility.CreateWarObjectOfType(attacker, Mathf.RoundToInt(Mathf.Clamp(endPointsAttacker, 50, attacker.RimWarPoints)), WorldUtility.GetRimWarDataForFaction(attacker.Faction), attacker.ParentSettlement, attacker.Tile, attacker.ParentSettlement.Tile, WorldObjectDefOf.Settlement);
                    }
                }   
                defender.RimWarPoints = Mathf.RoundToInt(Mathf.Clamp(endPointsDefender, 100, defender.RimWarPoints + attacker.RimWarPoints));                
            }
            RW_LetterMaker.Archive_RWLetter(let);
            defender.Faction.TryAffectGoodwillWith(attacker.Faction, -5, false, false, null, null);
            attacker.Faction.TryAffectGoodwillWith(defender.Faction, -2, false, false, null, null);
        }

        public static void ResolveRimWarTrade(Trader attacker, Trader defender)
        {
            float combinedPoints = attacker.RimWarPoints + defender.RimWarPoints;
            float attackerRoll = Rand.Value;
            float defenderRoll = Rand.Value;
            float attackerResult = attackerRoll * attacker.RimWarPoints;
            float defenderResult = defenderRoll * defender.RimWarPoints;
            float endPointsAttacker = 0f;
            float endPointsDefender = 0f;

            RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_TradeEvent);
            let.label = "RW_LetterTradeEvent".Translate();

            if (attackerResult > defenderResult)
            {
                //Log.Message("attacking trader " + attacker.Label + " wins agaisnt defending trader " + defender.Label);
                let.text = "RW_LetterTradeEventText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "swindled", defender.Label, defender.RimWarPoints);
                endPointsAttacker = (attacker.RimWarPoints + (Rand.Range(.1f, .2f) * defender.RimWarPoints)); //winner always gains points
                endPointsDefender = (defender.RimWarPoints + (Rand.Range(-.1f, .1f) * attacker.RimWarPoints)); //loser may lose or gain points
                //Attacker wins                
            }
            else
            {
                //Log.Message("defending trader " + defender.Label + " wins against attacking trader " + attacker.Label);
                //Defender wins
                let.text = "RW_LetterTradeEventText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "was taken advantage of by", defender.Label, defender.RimWarPoints);
                endPointsAttacker = (attacker.RimWarPoints + (Rand.Range(-.1f, .1f) * defender.RimWarPoints)); //loser may lose or gain points
                endPointsDefender = (defender.RimWarPoints + (Rand.Range(.1f, .2f) * attacker.RimWarPoints)); //winner always gains points      
            }
            //attacker.TradedWith.Add(defender);            
            //defender.TradedWith.Add(attacker);
            attacker.tradedWithTrader = true;
            defender.tradedWithTrader = true;
            attacker.RimWarPoints = Mathf.RoundToInt(Mathf.Clamp(endPointsAttacker, 0, attacker.RimWarPoints * 1.5f));
            defender.RimWarPoints = Mathf.RoundToInt(Mathf.Clamp(endPointsDefender, 0, defender.RimWarPoints * 1.5f));
            defender.Faction.TryAffectGoodwillWith(attacker.Faction, 2, false, false, null, null);
            attacker.Faction.TryAffectGoodwillWith(defender.Faction, 2, false, false, null, null);

            let.lookTargets = attacker;
            let.relatedFaction = attacker.Faction;
            RW_LetterMaker.Archive_RWLetter(let);
        }

        public static void ResolveSettlementTrade(Trader attacker, Settlement defenderTown)
        {
            float combinedPoints = attacker.RimWarPoints + defenderTown.RimWarPoints;
            float attackerRoll = Rand.Value;
            float defenderRoll = Rand.Value;
            float attackerResult = attackerRoll * attacker.RimWarPoints;
            float defenderResult = defenderRoll * defenderTown.RimWarPoints;
            float endPointsAttacker = 0f;
            float endPointsDefender = 0f;

            RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_TradeEvent);
            let.label = "RW_LetterTradeEvent".Translate();

            if (attackerResult > defenderResult)
            {
                //Log.Message("attacking trader " + attacker.Label + " wins agaisnt defending settlement " + defenderTown.RimWorld_Settlement.Name);
                //Attacker wins 
                let.text = "RW_LetterTradeEventText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "made significant gains in a trade with", defenderTown.RimWorld_Settlement.Name, defenderTown.RimWarPoints);
                endPointsAttacker = (attacker.RimWarPoints + Mathf.Clamp(Rand.Range(.15f, .3f) * attacker.RimWarPoints, 0, 1000));  //always based on trader total points
                endPointsDefender = (defenderTown.RimWarPoints + Mathf.Clamp(Rand.Range(.1f, .2f) * attacker.RimWarPoints, 0, 1000));
            }
            else
            {
                //Log.Message("defending settlement " + defenderTown.RimWorld_Settlement.Name + " wins against attacking trader " + attacker.Label);
                //Defender wins
                let.text = "RW_LetterTradeEventText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "made minor gains in a trade with", defenderTown.RimWorld_Settlement.Name, defenderTown.RimWarPoints);
                endPointsAttacker = (attacker.RimWarPoints + Mathf.Clamp(Rand.Range(.1f, .2f) * attacker.RimWarPoints, 0, 1000)); 
                endPointsDefender = (defenderTown.RimWarPoints + Mathf.Clamp(Rand.Range(.15f, .3f) * attacker.RimWarPoints, 0, 1000));
            }
            defenderTown.RimWarPoints = Mathf.RoundToInt(endPointsDefender);
            Trader newTrader = WorldUtility.CreateTrader(Mathf.RoundToInt(endPointsAttacker), attacker.rimwarData, attacker.ParentSettlement, defenderTown.Tile, attacker.ParentSettlement.Tile, WorldObjectDefOf.Settlement);
            newTrader.tradedWithSettlement = true;
            defenderTown.Faction.TryAffectGoodwillWith(attacker.Faction, 1, false, false, null, null);
            attacker.Faction.TryAffectGoodwillWith(defenderTown.Faction, 1, false, false, null, null);

            let.lookTargets = newTrader;
            let.relatedFaction = attacker.Faction;
            RW_LetterMaker.Archive_RWLetter(let);
        }

        public static void DoRaidWithPoints(int points, RimWorld.Planet.Settlement playerSettlement, RimWarData rwd, PawnsArrivalModeDef arrivalMode)
        {
            IncidentParms parms = new IncidentParms();
            PawnGroupKindDef combat = PawnGroupKindDefOf.Combat;
            
            parms.faction = rwd.RimWarFaction;
            parms.generateFightersOnly = true;
            parms.raidArrivalMode = arrivalMode;
            parms.target = playerSettlement.Map;
            parms.points = points;
            parms = ResolveRaidStrategy(parms, combat);
            parms.points = AdjustedRaidPoints((float)points, parms.raidArrivalMode, parms.raidStrategy, rwd.RimWarFaction, combat);
            //Log.Message("adjusted points " + parms.points);
            //PawnGroupMakerParms defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(combat, parms);
            //List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(defaultPawnGroupMakerParms).ToList();
            //if (list.Count == 0)
            //{
            //    Log.Error("Got no pawns spawning raid from parms " + parms);
            //    //return false;
            //}
            //parms.raidArrivalMode.Worker.Arrive(list, parms);
            IncidentWorker_RaidEnemy raid = new IncidentWorker_RaidEnemy();
            raid.TryExecute(parms);

            RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_HostileEvent);
            let.label = "RW_LetterPlayerSettlementBattle".Translate();
            let.text = "RW_RaidedPlayer".Translate(rwd.RimWarFaction, playerSettlement.Label, points);
            let.relatedFaction = rwd.RimWarFaction;
            let.lookTargets = playerSettlement;
            RW_LetterMaker.Archive_RWLetter(let);
        }

        public static void DoReinforcementWithPoints(int points, RimWorld.Planet.Settlement playerSettlement, RimWarData rwd, PawnsArrivalModeDef arrivalMode)
        {
            IncidentParms parms = new IncidentParms();
            PawnGroupKindDef combat = PawnGroupKindDefOf.Combat;

            parms.faction = rwd.RimWarFaction;
            parms.generateFightersOnly = true;
            parms.raidArrivalMode = arrivalMode;
            parms.target = playerSettlement.Map;
            parms.points = points;
            parms.raidStrategy = RaidStrategyOrRandom(RaidStrategyDefOf.ImmediateAttack);
            parms.points = AdjustedRaidPoints((float)points, parms.raidArrivalMode, parms.raidStrategy, rwd.RimWarFaction, combat);
            //Log.Message("adjusted points " + parms.points);
            //PawnGroupMakerParms defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(combat, parms);
            //List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(defaultPawnGroupMakerParms).ToList();
            //if (list.Count == 0)
            //{
            //    Log.Error("Got no pawns spawning raid from parms " + parms);
            //    //return false;
            //}
            //parms.raidArrivalMode.Worker.Arrive(list, parms);
            IncidentWorker_RaidFriendly raid = new IncidentWorker_RaidFriendly();
            raid.TryExecute(parms);

            RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_FriendlyEvent);
            let.label = "RW_LetterPlayerSettlementReinforcement".Translate();
            let.text = "RW_ReinforcedPlayer".Translate(rwd.RimWarFaction, playerSettlement.Label, points);
            let.relatedFaction = rwd.RimWarFaction;
            let.lookTargets = playerSettlement;
            RW_LetterMaker.Archive_RWLetter(let);
        }

        public static void DoCaravanAttackWithPoints(WarObject warObject, Caravan playerCaravan, RimWarData rwd, PawnsArrivalModeDef arrivalMode)
        {
            IncidentParms parms = new IncidentParms();
            PawnGroupKindDef kindDef = PawnGroupKindDefOf.Combat;               
            parms.faction = rwd.RimWarFaction;
            parms.raidArrivalMode = arrivalMode;
            parms.points = warObject.RimWarPoints;
            parms.target = playerCaravan;
            parms.raidStrategy = RaidStrategyOrRandom(RaidStrategyDefOf.ImmediateAttack);
            if (warObject is Trader)
            {
                kindDef = PawnGroupKindDefOf.Trader;
                parms.generateFightersOnly = false;
                Faction enemyFaction = rwd.RimWarFaction;
                PawnGroupMakerParms defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(kindDef, parms);
                defaultPawnGroupMakerParms.generateFightersOnly = false;
                defaultPawnGroupMakerParms.dontUseSingleUseRocketLaunchers = false;
                List<Pawn> attackers = PawnGroupMakerUtility.GeneratePawns(defaultPawnGroupMakerParms).ToList();
                if (attackers.Count == 0)
                {
                    Log.Error("Caravan demand incident couldn't generate any enemies even though min points have been checked. faction=" + defaultPawnGroupMakerParms.faction + "(" + ((defaultPawnGroupMakerParms.faction == null) ? "null" : defaultPawnGroupMakerParms.faction.def.ToString()) + ") parms=" + parms);
                }
                else
                {
                    Map map = CaravanIncidentUtility.SetupCaravanAttackMap(playerCaravan, attackers, sendLetterIfRelatedPawns: false);
                    parms.target = map;
                    parms = ResolveRaidStrategy(parms, kindDef);
                    parms.points = AdjustedRaidPoints((float)warObject.RimWarPoints, parms.raidArrivalMode, parms.raidStrategy, rwd.RimWarFaction, kindDef);
                    CameraJumper.TryJumpAndSelect(playerCaravan);
                    //TaleRecorder.RecordTale(TaleDefOf.CaravanAmbushedByHumanlike, playerCaravan.RandomOwner());
                    LongEventHandler.QueueLongEvent(delegate
                    {
                        LordJob_AssaultColony lordJob_AssaultColony = new LordJob_AssaultColony(enemyFaction, canKidnap: true, canTimeoutOrFlee: false);
                        if (lordJob_AssaultColony != null)
                        {
                            LordMaker.MakeNewLord(enemyFaction, lordJob_AssaultColony, map, attackers);
                        }
                        Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                        CameraJumper.TryJump(attackers[0]);
                    }, "GeneratingMapForNewEncounter", false, null);
                }
            }
            else
            {
                IncidentWorker_CaravanDemand iw_caravanDemand = new IncidentWorker_CaravanDemand();
                iw_caravanDemand.TryExecute(parms);
            }
            RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_HostileEvent);
            let.label = "RW_CaravanAmbush".Translate(playerCaravan.Label);
            let.text = "RW_CaravanAmbushedText".Translate(playerCaravan.Label, warObject.Label, warObject.RimWarPoints);
            let.lookTargets = playerCaravan;
            let.relatedFaction = warObject.Faction;
            RW_LetterMaker.Archive_RWLetter(let);

        }

        public static void DoCaravanTradeWithPoints(WarObject warObject, Caravan playerCaravan, RimWarData rwd, PawnsArrivalModeDef arrivalMode)
        {
            IncidentParms parms = new IncidentParms();
            PawnGroupKindDef kindDef = PawnGroupKindDefOf.Trader;
            parms.faction = rwd.RimWarFaction;
            parms.raidArrivalMode = arrivalMode;
            parms.points = warObject.RimWarPoints;
            parms.target = playerCaravan;
            parms.raidStrategy = RaidStrategyOrRandom(RaidStrategyDefOf.ImmediateAttack);            
            IncidentWorker_CaravanMeeting iw_caravanMeeting = new IncidentWorker_CaravanMeeting();
            iw_caravanMeeting.TryExecute(parms);

            RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_FriendlyEvent);
            let.label = "RW_CaravanTrade".Translate(playerCaravan.Label);
            let.text = "RW_CaravanTradeText".Translate(playerCaravan.Label, warObject.Label);
            let.lookTargets = playerCaravan;
            let.relatedFaction = warObject.Faction;
            RW_LetterMaker.Archive_RWLetter(let);
        }

        public static void DoSettlementTradeWithPoints(WarObject warObject, RimWorld.Planet.Settlement playerSettlement, RimWarData rwd, PawnsArrivalModeDef arrivalMode)
        {
            IncidentParms parms = new IncidentParms();
            PawnGroupKindDef kindDef = PawnGroupKindDefOf.Trader;
            parms.faction = rwd.RimWarFaction;
            parms.raidArrivalMode = arrivalMode;
            parms.points = warObject.RimWarPoints;
            parms.target = playerSettlement.Map;
            IncidentWorker_TraderCaravanArrival iw_tca = new IncidentWorker_TraderCaravanArrival();
            iw_tca.TryExecute(parms);

            RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_FriendlyEvent);
            let.label = "RW_SettlementTrade".Translate(playerSettlement.Label);
            let.text = "RW_SettlementTradeText".Translate(playerSettlement.Label, warObject.Label);
            let.lookTargets = playerSettlement;
            let.relatedFaction = warObject.Faction;
            RW_LetterMaker.Archive_RWLetter(let);
        }

        public static void DoPeaceTalks_Caravan(WarObject warObject, Caravan playerCaravan, RimWarData rwd, PawnsArrivalModeDef arrivalMode)
        {
            IncidentParms parms = new IncidentParms();
            PawnGroupKindDef kindDef = PawnGroupKindDefOf.Peaceful;
            parms.faction = rwd.RimWarFaction;
            parms.raidArrivalMode = arrivalMode;
            parms.points = warObject.RimWarPoints;
            parms.target = playerCaravan;
            parms.raidStrategy = RaidStrategyOrRandom(RaidStrategyDefOf.ImmediateAttack);
            IncidentDef def = new IncidentDef();
            def = IncidentDef.Named("Quest_PeaceTalks");
            PeaceTalks peaceTalks = (PeaceTalks)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.PeaceTalks);
            peaceTalks.Tile = playerCaravan.Tile;
            peaceTalks.SetFaction(warObject.Faction);
            int randomInRange = SiteTuning.QuestSiteTimeoutDaysRange.RandomInRange;
            peaceTalks.GetComponent<TimeoutComp>().StartTimeout(randomInRange * 60000);
            Find.WorldObjects.Add(peaceTalks);
            string text = def.letterText.Formatted(warObject.Faction.def.leaderTitle, warObject.Faction.Name, randomInRange, warObject.Faction.leader.Named("PAWN")).AdjustedFor(warObject.Faction.leader).CapitalizeFirst();
            Find.LetterStack.ReceiveLetter(def.letterLabel, text, def.letterDef, peaceTalks, warObject.Faction);
            //IncidentWorker_QuestPeaceTalks iw_peaceTalkQuest = new IncidentWorker_QuestPeaceTalks();
            //iw_peaceTalkQuest.TryExecute(parms);
        }

        //public static void DoPeaceTalks_Settlement(WarObject warObject, RimWorld.Planet.Settlement playerSettlement, RimWarData rwd, PawnsArrivalModeDef arrivalMode)
        //{
        //    IncidentParms parms = new IncidentParms();
        //    PawnGroupKindDef kindDef = PawnGroupKindDefOf.Peaceful;
        //    parms.faction = rwd.RimWarFaction;
        //    parms.raidArrivalMode = arrivalMode;
        //    parms.points = warObject.RimWarPoints;
        //    parms.target = playerSettlement.Map;
        //    parms.raidStrategy = RaidStrategyOrRandom(RaidStrategyDefOf.ImmediateAttack);
        //    IncidentWorker_QuestPeaceTalks iw_peaceTalkQuest = new IncidentWorker_QuestPeaceTalks();
        //    iw_peaceTalkQuest.TryExecute(parms);
        //}

        public static float AdjustedRaidPoints(float points, PawnsArrivalModeDef raidArrivalMode, RaidStrategyDef raidStrategy, Faction faction, PawnGroupKindDef groupKind)
        {
            if (raidArrivalMode.pointsFactorCurve != null)
            {
                points *= raidArrivalMode.pointsFactorCurve.Evaluate(points);
            }
            if (raidStrategy.pointsFactorCurve != null)
            {
                points *= raidStrategy.pointsFactorCurve.Evaluate(points);
            }
            points = Mathf.Max(points, raidStrategy.Worker.MinimumPoints(faction, groupKind) * 1.05f);
            return points;
        }

        public static IncidentParms ResolveRaidStrategy(IncidentParms parms, PawnGroupKindDef groupKind)
        {
            if (parms.raidStrategy == null)
            {
                Map map = (Map)parms.target;
                DefDatabase<RaidStrategyDef>.AllDefs.Where(delegate (RaidStrategyDef d)
                {
                    if (d.Worker.CanUseWith(parms, groupKind))
                    {
                        if (parms.raidArrivalMode == null)
                        {
                            if (d.arriveModes != null)
                            {
                                return d.arriveModes.Any((PawnsArrivalModeDef x) => x.Worker.CanUseWith(parms));
                            }
                            return false;
                        }
                        return true;
                    }
                    return false;
                }).TryRandomElementByWeight((RaidStrategyDef d) => d.Worker.SelectionWeight(map, parms.points), out RaidStrategyDef result);
                parms.raidStrategy = result;
                if (parms.raidStrategy == null)
                {
                    Log.Error("No raid stategy found, defaulting to ImmediateAttack. Faction=" + parms.faction.def.defName + ", points=" + parms.points + ", groupKind=" + groupKind + ", parms=" + parms);
                    parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                }
            }
            return parms;
        }

        public static PawnsArrivalModeDef PawnsArrivalModeOrRandom(PawnsArrivalModeDef arrivalMode)
        {
            try
            {
                foreach (PawnsArrivalModeDef allDefs in DefDatabase<PawnsArrivalModeDef>.AllDefs)
                {
                    if (allDefs.defName == arrivalMode.defName)
                    {
                        return arrivalMode;
                    }
                }
            }
            catch
            {

            }

            return DefDatabase<PawnsArrivalModeDef>.AllDefs.RandomElement();
        }

        public static RaidStrategyDef RaidStrategyOrRandom(RaidStrategyDef raidStrategy)
        {
            try
            {
                foreach (RaidStrategyDef allDefs in DefDatabase<RaidStrategyDef>.AllDefs)
                {
                    if (allDefs.defName == raidStrategy.defName)
                    {
                        return raidStrategy;
                    }
                }
            }
            catch
            {

            }

            return DefDatabase<RaidStrategyDef>.AllDefs.RandomElement();
            
        }
    }
}
