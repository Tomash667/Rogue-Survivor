﻿using RogueSurvivor.Data;
using RogueSurvivor.Engine.MapObjects;
using System;

namespace RogueSurvivor.Engine.Actions
{
    class ActionBarricadeDoor : ActorAction
    {
        DoorWindow m_Door;

        public ActionBarricadeDoor(Actor actor, RogueGame game, DoorWindow door)
            : base(actor, game)
        {
            if (door == null)
                throw new ArgumentNullException("door");

            m_Door = door;
        }

        public override bool IsLegal()
        {
            return m_Game.Rules.CanActorBarricadeDoor(m_Actor, m_Door, out m_FailReason);
        }

        public override void Perform()
        {
            m_Game.DoBarricadeDoor(m_Actor, m_Door);
        }
    }
}
