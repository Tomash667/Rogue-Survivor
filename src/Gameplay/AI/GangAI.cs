﻿using RogueSurvivor.Data;
using RogueSurvivor.Engine;
using RogueSurvivor.Engine.Actions;
using RogueSurvivor.Engine.AI;
using RogueSurvivor.Gameplay.AI.Sensors;
using RogueSurvivor.Gameplay.AI.Tools;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace RogueSurvivor.Gameplay.AI
{
    [Serializable]
    /// <summary>
    /// Gang AI : Bikers, Gangstas...
    /// </summary>
    class GangAI : OrderableAI
    {
        const int FOLLOW_NPCLEADER_MAXDIST = 1;
        const int FOLLOW_PLAYERLEADER_MAXDIST = 1;
        const int LOS_MEMORY = 10;

        const int EXPLORATION_LOCATIONS = 30;
        const int EXPLORATION_ZONES = 3;

        const int DONT_LEAVE_BEHIND_EMOTE_CHANCE = 50;

        static string[] FIGHT_EMOTES =
        {
            "Fuck you",
            "Fuck it I'm trapped!",
            "Come on"
        };

        const string CANT_GET_ITEM_EMOTE = "Fuck can't get that shit!";

        LOSSensor m_LOSSensor;
        MemorizedSensor m_MemorizedSensor;

        ExplorationData m_Exploration;

        // needed as ref param to a new behavior but unused
        Percept m_DummyPerceptLastItemsSaw = null;

        public override void TakeControl(Actor actor)
        {
            base.TakeControl(actor);

            m_Exploration = new ExplorationData(EXPLORATION_LOCATIONS, EXPLORATION_ZONES);
        }

        protected override void CreateSensors()
        {
            m_LOSSensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS | LOSSensor.SensingFilter.ITEMS);
            m_MemorizedSensor = new MemorizedSensor(m_LOSSensor, LOS_MEMORY);
        }

        protected override List<Percept> UpdateSensors(RogueGame game)
        {
            return m_MemorizedSensor.Sense(game, m_Actor);
        }

        protected override ActorAction SelectAction(RogueGame game, List<Percept> percepts)
        {
            HashSet<Point> FOV = m_LOSSensor.FOV;
            List<Percept> mapPercepts = FilterSameMap(game, percepts);

            // don't run by default.
            m_Actor.IsRunning = false;

            // 0. Equip best item
            ActorAction bestEquip = BehaviorEquipBestItems(game, true, true);
            if (bestEquip != null)
            {
                return bestEquip;
            }

            // 1. Follow order
            if (this.Order != null)
            {
                ActorAction orderAction = ExecuteOrder(game, this.Order, mapPercepts, m_Exploration);
                if (orderAction == null)
                    SetOrder(null);
                else
                {
                    m_Actor.Activity = Activity.FOLLOWING_ORDER;
                    return orderAction;
                }
            }

            //////////////////////////////////////////////////////////////////////
            // partial copy of Civilian AI 8) but always courageous and gets into fights.
            // BEHAVIOR
            // - FLAGS
            // "courageous" : always if not tired.
            // - RULES
            // 1 fire at nearest.
            // 2 shout, fight or flee.
            // 3 use medecine
            // 4 rest if tired
            // 5 eat when hungry (also eat corpses)
            // 6 sleep.
            // 7 drop light/tracker with no batteries
            // 8 get nearby item (not if seeing enemy)
            // 9 steal item from someone.
            // 10 tear down barricade
            // 11 follow leader
            // 12 take lead (if leadership)
            // 13 (leader) don't leave follower behind.
            // 14 explore
            // 15 wander
            //////////////////////////////////////////////////////////////////////

            // get data.
            List<Percept> allEnemies = FilterEnemies(game, mapPercepts);
            List<Percept> currentEnemies = FilterCurrent(game, allEnemies);
            bool hasCurrentEnemies = currentEnemies != null;
            bool hasAnyEnemies = allEnemies != null;
            bool checkOurLeader = m_Actor.HasLeader && !DontFollowLeader;
            bool seeLeader = checkOurLeader && FOV.Contains(m_Actor.Leader.Location.Position);
            bool isLeaderFighting = checkOurLeader && IsAdjacentToEnemy(game, m_Actor.Leader);
            bool isCourageous = !game.Rules.IsActorTired(m_Actor);

            // exploration.
            m_Exploration.Update(m_Actor.Location);

            // needed due to uggraded get item behavior
            // clear taboo tiles : periodically or when changing maps.
            if (m_Actor.Location.Map.LocalTime.TurnCounter % WorldTime.TURNS_PER_HOUR == 0 ||
                (PrevLocation != null && PrevLocation.Map != m_Actor.Location.Map))
            {
                ClearTabooTiles();
            }

            // 1 fire at nearest enemy (always if has leader, half of the time if not)
            if (hasCurrentEnemies && (checkOurLeader || game.Rules.RollChance(50)))
            {
                List<Percept> fireTargets = FilterFireTargets(game, currentEnemies);
                if (fireTargets != null)
                {
                    Percept nearestTarget = FilterNearest(game, fireTargets);
                    ActorAction fireAction = BehaviorRangedAttack(game, nearestTarget);
                    if (fireAction != null)
                    {
                        m_Actor.Activity = Activity.FIGHTING;
                        m_Actor.TargetActor = nearestTarget.Percepted as Actor;
                        return fireAction;
                    }
                }
            }

            // 2 shout, fight or flee
            if (hasCurrentEnemies)
            {
                // shout?
                if (game.Rules.RollChance(50))
                {
                    List<Percept> friends = FilterNonEnemies(game, mapPercepts);
                    if (friends != null)
                    {
                        ActorAction shoutAction = BehaviorWarnFriends(game, friends, FilterNearest(game, currentEnemies).Percepted as Actor);
                        if (shoutAction != null)
                        {
                            m_Actor.Activity = Activity.IDLE;
                            return shoutAction;
                        }
                    }
                }

                // fight or flee.
                RouteFinder.SpecialActions allowedChargeActions = RouteFinder.SpecialActions.JUMP | RouteFinder.SpecialActions.DOORS;
                // gangs are allowed to make a mess :)
                allowedChargeActions |= RouteFinder.SpecialActions.BREAK | RouteFinder.SpecialActions.PUSH;
                ActorAction fightOrFlee = BehaviorFightOrFlee(game, currentEnemies, seeLeader, isLeaderFighting, ActorCourage.COURAGEOUS, FIGHT_EMOTES, allowedChargeActions);
                if (fightOrFlee != null)
                {
                    return fightOrFlee;
                }
            }

            // 3 use medecine
            ActorAction useMedAction = BehaviorUseMedecine(game, 2, 1, 2, 4, 2);
            if (useMedAction != null)
            {
                m_Actor.Activity = Activity.IDLE;
                return useMedAction;
            }

            // 4 rest if tired
            ActorAction restAction = BehaviorRestIfTired(game);
            if (restAction != null)
            {
                m_Actor.Activity = Activity.IDLE;
                return new ActionWait(m_Actor, game);
            }

            // 5 eat when hungry (also eat corpses)
            if (game.Rules.IsActorHungry(m_Actor))
            {
                ActorAction eatAction = BehaviorEat(game);
                if (eatAction != null)
                {
                    m_Actor.Activity = Activity.IDLE;
                    return eatAction;
                }
                if (game.Rules.IsActorStarving(m_Actor) || game.Rules.IsActorInsane(m_Actor))
                {
                    eatAction = BehaviorGoEatCorpse(game, FilterCorpses(game, mapPercepts));
                    if (eatAction != null)
                    {
                        m_Actor.Activity = Activity.IDLE;
                        return eatAction;
                    }
                }
            }

            // 6 sleep.
            if (!hasAnyEnemies && WouldLikeToSleep(game, m_Actor) && IsInside(m_Actor) && game.Rules.CanActorSleep(m_Actor))
            {
                // secure sleep?
                ActorAction secureSleepAction = BehaviorSecurePerimeter(game, m_LOSSensor.FOV);
                if (secureSleepAction != null)
                {
                    m_Actor.Activity = Activity.IDLE;
                    return secureSleepAction;
                }

                // sleep.
                ActorAction sleepAction = BehaviorSleep(game, m_LOSSensor.FOV);
                if (sleepAction != null)
                {
                    if (sleepAction is ActionSleep)
                        m_Actor.Activity = Activity.SLEEPING;
                    return sleepAction;
                }
            }

            // 7 drop light/tracker with no batteries
            ActorAction dropOutOfBatteries = BehaviorDropUselessItem(game);
            if (dropOutOfBatteries != null)
            {
                m_Actor.Activity = Activity.IDLE;
                return dropOutOfBatteries;
            }

            // 8 get nearby item (not if seeing enemy)
            // ignore not currently visible items & blocked items.
            // upgraded rule to use the same new core behavior as CivilianAI with custom params
            if (!hasCurrentEnemies)
            {
                // new common behaviour code, also used by CivilianAI, but Gangs can break and push
                ActorAction getItemAction = BehaviorGoGetInterestingItems(game, mapPercepts,
                     true, true, CANT_GET_ITEM_EMOTE, false, ref m_DummyPerceptLastItemsSaw);

                if (getItemAction != null)
                    return getItemAction;
            }

            // 9 steal item from someone.
            if (!hasCurrentEnemies)
            {
                Map map = m_Actor.Location.Map;
                List<Percept> mayStealFrom = FilterActors(game, FilterCurrent(game, mapPercepts),
                    (a) =>
                    {
                        if (a.Inventory == null || a.Inventory.CountItems == 0 || IsFriendOf(game, a))
                            return false;
                        if (game.Rules.RollChance(game.Rules.ActorUnsuspicousChance(m_Actor, a)))
                        {
                            // emote.
                            game.DoEmote(a, string.Format("moves unnoticed by {0}.", m_Actor.Name));
                            // unnoticed.
                            return false;
                        }
                        return HasAnyInterestingItem(game, a.Inventory, ItemSource.ANOTHER_ACTOR);
                    });

                if (mayStealFrom != null)
                {
                    // make sure to consider only reachable victims
                    RouteFinder.SpecialActions allowedActions;
                    allowedActions = RouteFinder.SpecialActions.ADJ_TO_DEST_IS_GOAL | RouteFinder.SpecialActions.JUMP | RouteFinder.SpecialActions.DOORS;
                    // gangs can break & push stuff
                    allowedActions |= RouteFinder.SpecialActions.BREAK | RouteFinder.SpecialActions.PUSH;
                    FilterOutUnreachablePercepts(game, ref mayStealFrom, allowedActions);

                    if (mayStealFrom.Count > 0)
                    {
                        // get data.
                        Percept nearest = FilterNearest(game, mayStealFrom);
                        Actor victim = nearest.Percepted as Actor;
                        Item wantIt = FirstInterestingItem(game, victim.Inventory, ItemSource.ANOTHER_ACTOR);

                        // make an enemy of him.
                        game.DoMakeAggression(m_Actor, victim);

                        // declare my evil intentions.
                        m_Actor.Activity = Activity.CHASING;
                        m_Actor.TargetActor = victim;
                        return new ActionSay(m_Actor, game, victim,
                            string.Format("Hey! That's some nice {0} you have here!", wantIt.Model.SingleName), RogueGame.Sayflags.IS_IMPORTANT | RogueGame.Sayflags.IS_DANGER);
                    }
                }
            }

            // 10 tear down barricade
            ActorAction attackBarricadeAction = BehaviorAttackBarricade(game);
            if (attackBarricadeAction != null)
            {
                m_Actor.Activity = Activity.IDLE;
                return attackBarricadeAction;
            }

            // 11 follow leader
            if (checkOurLeader)
            {
                Point lastKnownLeaderPosition = m_Actor.Leader.Location.Position;
                bool isLeaderVisible = FOV.Contains(m_Actor.Leader.Location.Position);
                int maxDist = m_Actor.Leader.IsPlayer ? FOLLOW_PLAYERLEADER_MAXDIST : FOLLOW_NPCLEADER_MAXDIST;
                ActorAction followAction = BehaviorFollowActor(game, m_Actor.Leader, lastKnownLeaderPosition, isLeaderVisible, maxDist);
                if (followAction != null)
                {
                    m_Actor.Activity = Activity.FOLLOWING;
                    m_Actor.TargetActor = m_Actor.Leader;
                    return followAction;
                }
            }

            // 12 take lead (if leadership)
            bool isLeader = m_Actor.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.LEADERSHIP) >= 1;
            bool canLead = !checkOurLeader && isLeader && m_Actor.CountFollowers < game.Rules.ActorMaxFollowers(m_Actor);
            if (canLead)
            {
                Percept nearestFriend = FilterNearest(game, FilterNonEnemies(game, mapPercepts));
                if (nearestFriend != null)
                {
                    ActorAction leadAction = BehaviorLeadActor(game, nearestFriend);
                    if (leadAction != null)
                    {
                        m_Actor.Activity = Activity.IDLE;
                        m_Actor.TargetActor = nearestFriend.Percepted as Actor;
                        return leadAction;
                    }
                }
            }

            // 13 (leader) don't leave followers behind.
            if (m_Actor.CountFollowers > 0)
            {
                Actor target;
                ActorAction stickTogether = BehaviorDontLeaveFollowersBehind(game, 3, out target);
                if (stickTogether != null)
                {
                    // emote?
                    if (game.Rules.RollChance(DONT_LEAVE_BEHIND_EMOTE_CHANCE))
                    {
                        if (target.IsSleeping)
                            game.DoEmote(m_Actor, string.Format("patiently waits for {0} to wake up.", target.Name));
                        else
                        {
                            if (m_LOSSensor.FOV.Contains(target.Location.Position))
                                game.DoEmote(m_Actor, string.Format("Hey {0}! Fucking move!", target.Name));
                            else
                                game.DoEmote(m_Actor, string.Format("Where is that {0} retard?", target.Name));
                        }
                    }

                    // go!
                    m_Actor.Activity = Activity.IDLE;
                    return stickTogether;
                }
            }

            // 14 explore
            ActorAction exploreAction = BehaviorExplore(game, m_Exploration);
            if (exploreAction != null)
            {
                m_Actor.Activity = Activity.IDLE;
                return exploreAction;
            }

            // 15 wander
            m_Actor.Activity = Activity.IDLE;
            return BehaviorWander(game, m_Exploration);
        }
    }
}
